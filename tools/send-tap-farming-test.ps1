#requires -Version 7.2
<#
.SYNOPSIS
Envia taps simulados individuales mientras Farmeada sigue activa.
.DESCRIPTION
Conserva reserva, conversion y ajustes. Requiere Interacciones desactivadas para
no activar reglas de ataques. Un like count=1 por solicitud; nunca reintenta.
Continua entre derrota y reintento de la misma sesion. Ctrl+C o StopFilePath
detienen nuevos envios; lo aceptado puede terminar de procesarse despues.
.EXAMPLE
pwsh -NoProfile -File ./tools/send-tap-farming-test.ps1 -DurationSeconds 120 -Rate 20 -ExpectedLevel Robot
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 3600)][int]$DurationSeconds = 120,
    [ValidateRange(1, 200)][int]$Rate = 20,
    [ValidateLength(1, 160)][string]$ExpectedLevel = 'Robot',
    [ValidateRange(1, 65535)][int]$Port = 18081,
    [string]$ReportPath,
    [string]$StopFilePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'StreamSimulationHttp.psm1') -ErrorAction Stop
$runId = [guid]::NewGuid().ToString('N')
if (!$ReportPath) {
    $ReportPath = Join-Path (Split-Path -Parent $PSScriptRoot) ".deployment-cache/tap-farming-test-$runId.json"
}
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($ReportPath))
if ($StopFilePath) { $StopFilePath = [IO.Path]::GetFullPath($StopFilePath) }
$handler = [Net.Http.HttpClientHandler]::new()
$handler.UseProxy = $false
$handler.AllowAutoRedirect = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.BaseAddress = [uri]"http://127.0.0.1:$Port/"
$client.Timeout = [Threading.Timeout]::InfiniteTimeSpan

function Read-TapTestJson([string]$Path, [int]$TimeoutMilliseconds = 5000) {
    $deadline = [Threading.CancellationTokenSource]::new($TimeoutMilliseconds)
    $response = $null
    try {
        $response = $client.GetAsync($Path, $deadline.Token).GetAwaiter().GetResult()
        $null = $response.EnsureSuccessStatusCode()
        return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    }
    finally {
        if ($null -ne $response) { $response.Dispose() }
        $deadline.Dispose()
    }
}
function Get-TapTestStopReason($Farm, $Interaction, [bool]$Initial) {
    if (!$Farm.ready -or !$Interaction.ready) { return 'state_not_ready' }
    if ($Interaction.interactionsEnabled -ne $false) { return 'interactions_enabled' }
    if (!$Initial -and $Farm.sessionId -ne $before.sessionId) { return 'session_changed' }
    if ($Farm.phase -eq 'completed') { return 'farming_completed' }
    if (!$Farm.enabled -or $Farm.phase -notin @('collecting', 'active', 'transition')) { return 'farming_off' }
    if ($Farm.phase -in @('active', 'transition') -and
        ![string]::Equals([string]$Farm.levelId, $ExpectedLevel, [StringComparison]::OrdinalIgnoreCase)) {
        return 'unexpected_level'
    }
    if ($Farm.sessionId -le 0 -or $Farm.conversion.tapsPerConversion -le 0 -or
        $Farm.conversion.healthPointsPerConversion -le 0) { return 'invalid_farming_state' }
    if (!$Initial -and ($Farm.conversion.tapsPerConversion -ne $before.conversion.tapsPerConversion -or
        $Farm.conversion.healthPointsPerConversion -ne $before.conversion.healthPointsPerConversion)) {
        return 'conversion_changed'
    }
    return ''
}
function Get-TapTestSnapshot($Farm, [double]$ElapsedSeconds = -1d) {
    if ($ElapsedSeconds -lt 0d) { $ElapsedSeconds = $sendClock.Elapsed.TotalSeconds }
    $effectiveHealth = $null
    if ($Farm.PSObject.Properties['effectiveHealth'] -and $null -ne $Farm.effectiveHealth) {
        $effectiveHealth = [ordered]@{}
        foreach ($name in @('available', 'current', 'total', 'ratio', 'scope')) {
            $effectiveHealth[$name] = if ($Farm.effectiveHealth.PSObject.Properties[$name]) {
                $Farm.effectiveHealth.$name
            } else { $null }
        }
    }
    $phaseSnapshots = [Collections.Generic.List[object]]::new()
    if ($Farm.PSObject.Properties['phases'] -and $null -ne $Farm.phases) {
        foreach ($phase in $Farm.phases) {
            if ($null -eq $phase) { continue }
            $snapshot = [ordered]@{}
            foreach ($name in @('index', 'reserveHealth', 'progress')) {
                $snapshot[$name] = if ($phase.PSObject.Properties[$name]) { $phase.$name } else { $null }
            }
            $phaseSnapshots.Add($snapshot)
        }
    }
    return [ordered]@{
        elapsedSeconds = [math]::Round($ElapsedSeconds, 3)
        sessionId = $Farm.sessionId; totalTaps = $Farm.counters.totalTaps
        convertedHealth = $Farm.counters.convertedHealth; reserveHealth = $Farm.counters.reserveHealth
        spentHealth = $Farm.counters.spentHealth; unconvertedTaps = $Farm.counters.unconvertedTaps
        attempt = $Farm.attempt; phaseIndex = $Farm.phaseIndex
        levelId = $Farm.levelId; bossName = $Farm.bossName; phase = $Farm.phase
        effectiveHealth = $effectiveHealth; phases = $phaseSnapshots
    }
}

$before = $null; $after = $null; $interaction = $null
$attempted = 0; $accepted = 0; $uncertain = 0; $httpErrors = 0; $rejected429 = 0
$stopReason = ''; $failure = $null; $settleError = $null
$samples = [Collections.Generic.List[object]]::new()
$sendClock = [Diagnostics.Stopwatch]::new()
$startedUtc = [DateTime]::UtcNow.ToString('o')
$query = 'api/dashboard/simulate?platform=tiktok&type=like&count=1&delaySeconds=0' +
    '&user=TAPS-PRUEBA&userDisplayName=TAPS-PRUEBA&userId=' + $runId
try {
    # Request the panel projection once, then allow a Unity update before its
    # observation read. These GETs never change the master or clear its queue.
    $null = Read-TapTestJson 'api/config/interactions'
    Start-Sleep -Milliseconds 100
    $interaction = Read-TapTestJson 'api/config/interactions'
    $before = Read-TapTestJson 'api/config/tap-farming'
    $stopReason = Get-TapTestStopReason $before $interaction $true
    if ($stopReason) { throw "Preflight: $stopReason. No se enviaron taps." }
    Write-Host "Taps individuales: hasta $Rate/s durante $DurationSeconds s; sesion $($before.sessionId), reserva inicial $($before.counters.reserveHealth)."
    $sendClock.Start()
    $samples.Add((Get-TapTestSnapshot $before))
    $nextSendAt = 0d; $nextPollAt = 1000d
    while ($sendClock.Elapsed.TotalSeconds -lt $DurationSeconds) {
        if ($StopFilePath -and [IO.File]::Exists($StopFilePath)) { $stopReason = 'stop_file'; break }
        if ($sendClock.Elapsed.TotalMilliseconds -ge $nextPollAt) {
            $interaction = Read-TapTestJson 'api/config/interactions'
            $current = Read-TapTestJson 'api/config/tap-farming'
            if ($samples.Count -lt 3601) { $samples.Add((Get-TapTestSnapshot $current)) }
            $stopReason = Get-TapTestStopReason $current $interaction $false
            if ($stopReason) { break }
            $nextPollAt = $sendClock.Elapsed.TotalMilliseconds + 1000d
        }
        if ($sendClock.Elapsed.TotalSeconds -ge $DurationSeconds) { break }
        if ($sendClock.Elapsed.TotalMilliseconds -lt $nextSendAt) {
            Start-Sleep -Milliseconds ([int][math]::Max(1, [math]::Min(25,
                $nextSendAt - $sendClock.Elapsed.TotalMilliseconds)))
            continue
        }
        $attempted++
        $requestStartedAt = $sendClock.Elapsed.TotalMilliseconds
        try { $response = Send-StreamSimulationOnce -Port $Port -Path $query }
        catch { $uncertain++; $stopReason = 'uncertain_response'; throw }
        if ($response.StatusCode -eq 202) {
            try {
                $confirmation = $response.Body | ConvertFrom-Json
                if ($null -eq $confirmation -or !$confirmation.PSObject.Properties['ok'] -or !$confirmation.ok) {
                    throw 'Confirmacion invalida.'
                }
            }
            catch {
                $uncertain++; $stopReason = 'uncertain_response'
                throw 'HTTP 202 sin confirmacion valida; no se reintentara.'
            }
            $accepted++
        }
        else {
            if ($response.StatusCode -eq 429) { $rejected429++ } else { $httpErrors++ }
            $stopReason = 'http_error'
            throw "Simulador rechazo HTTP $($response.StatusCode); no se reintentara."
        }
        # Schedule from this request's start; never repay missed slots in a burst.
        $nextSendAt = $requestStartedAt + 1000d / $Rate
    }
    if (!$stopReason) { $stopReason = 'duration_elapsed' }
}
catch { $failure = $_.Exception.Message; if (!$stopReason) { $stopReason = 'read_error' } }
finally {
    $sendClock.Stop()
    if ($null -ne $before) {
        $settleClock = [Diagnostics.Stopwatch]::StartNew()
        try {
            do {
                $remaining = 2000 - [int]$settleClock.ElapsedMilliseconds
                if ($remaining -le 0) { break }
                $after = Read-TapTestJson 'api/config/tap-farming' $remaining
                if ($after.sessionId -ne $before.sessionId -or
                    $after.phase -notin @('collecting', 'active', 'transition') -or
                    ($after.counters.totalTaps - $before.counters.totalTaps) -ge $accepted) { break }
                $remaining = 2000 - [int]$settleClock.ElapsedMilliseconds
                if ($remaining -gt 0) { Start-Sleep -Milliseconds ([math]::Min(100, $remaining)) }
            } while ($settleClock.ElapsedMilliseconds -lt 2000)
        }
        catch { $settleError = $_.Exception.Message }
    }
    $comparable = $null -ne $before -and $null -ne $after -and $before.sessionId -eq $after.sessionId
    $deltas = $null
    if ($comparable) {
        $tapDelta = $after.counters.totalTaps - $before.counters.totalTaps
        $convertedDelta = $after.counters.convertedHealth - $before.counters.convertedHealth
        $reserveDelta = $after.counters.reserveHealth - $before.counters.reserveHealth
        $spentDelta = $after.counters.spentHealth - $before.counters.spentHealth
        $residual = [math]::Round($convertedDelta - $reserveDelta - $spentDelta, 3)
        $deltas = [ordered]@{
            totalTaps = $tapDelta; convertedHealth = $convertedDelta
            reserveHealth = $reserveDelta; spentHealth = $spentDelta
            convertedMinusReserveAndSpent = $residual
            reserveAccountingBalanced = [math]::Abs($residual) -le 0.01
        }
    }
    $report = [ordered]@{
        schemaVersion = 1; mode = 'continuous_tap_farming_simulation'; runId = $runId
        startedUtc = $startedUtc; port = $Port; expectedLevel = $ExpectedLevel
        requestedDurationSeconds = $DurationSeconds; maximumEventsPerSecond = $Rate
        sendingSeconds = [math]::Round($sendClock.Elapsed.TotalSeconds, 3)
        attempted = $attempted; accepted = $accepted; uncertain = $uncertain
        rejected429 = $rejected429; httpErrors = $httpErrors; stopReason = $stopReason
        completed = $null -eq $failure; error = $failure; settleError = $settleError
        conversion = if ($null -ne $before) { $before.conversion } else { $null }
        before = if ($null -ne $before) { Get-TapTestSnapshot $before 0d } else { $null }
        after = if ($null -ne $after) { Get-TapTestSnapshot $after } else { $null }
        countersComparable = $comparable; sharedCounterDelta = $deltas; samples = $samples
        accountingScope = 'Counters include real/foreign traffic. Accepted HTTP requests confirm scheduling, not exclusive attribution of every observed tap or reserve change.'
        measurementScope = 'Count-one simulation requests, worker and farming counters; HTTP timing is not frame time or FPS. No configuration, reserve or session reset is performed.'
    }
    try {
        $report | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $ReportPath -Encoding utf8
        Write-Host "Reporte: $ReportPath"
        Write-Host "Taps aceptados: $accepted; inciertos: $uncertain; fin: $stopReason."
    }
    finally { $client.Dispose() }
}
if ($failure) { throw $failure }
