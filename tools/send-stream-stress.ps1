#requires -Version 7.2
<#
.SYNOPSIS
Envia eventos simulados al panel local de una partida de prueba abierta.
.DESCRIPTION
Cada solicitud crea un evento independiente con count=1. Usa las reglas y las
colas actuales. Ctrl+C detiene nuevos envios; lo ya aceptado puede seguir en cola.
No cambia configuracion ni inicia o detiene el juego. No mide FPS.
Las lecturas de cola solicitan su publicacion, esperan 100 ms y vuelven a leer
la cache; si Unity no actualiza, pueden conservar el ultimo estado disponible.
.EXAMPLE
pwsh -NoProfile -File ./tools/send-stream-stress.ps1 -Events 300 -Rate 10 -Type Like
.EXAMPLE
pwsh -NoProfile -File ./tools/send-stream-stress.ps1 -Events 1000 -Rate 50 -Type Mixed -GiftId 5655
.EXAMPLE
pwsh -NoProfile -File ./tools/send-stream-stress.ps1 -Events 150 -Rate 5 -Type Gift -RandomGifts -RandomSeed 20261030
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 1000000)][int]$Events = 300,
    [ValidateRange(1, 1000)][int]$Rate = 10,
    [ValidateRange(1, 1000000)][int]$Viewers = 30,
    [ValidateSet('Like', 'Gift', 'Follow', 'Mixed')][string]$Type = 'Like',
    [ValidateLength(0, 160)][string]$GiftId = '',
    [switch]$RandomGifts,
    [ValidateRange(0, 2147483647)][int]$RandomSeed = 42,
    [ValidateRange(1, 65535)][int]$Port = 18081,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($RandomGifts -and ($Type -notin @('Gift', 'Mixed') -or ![string]::IsNullOrWhiteSpace($GiftId))) {
    throw 'RandomGifts requiere Type Gift o Mixed y no se combina con GiftId.'
}
if (!$RandomGifts -and $Type -in @('Gift', 'Mixed') -and [string]::IsNullOrWhiteSpace($GiftId)) {
    throw 'Gift y Mixed requieren -GiftId con un regalo del catalogo. Para generar ataques, debe tener una regla activa.'
}
$stressRepoRoot = Split-Path -Parent $PSScriptRoot
$runId = [guid]::NewGuid().ToString('N')
if (!$ReportPath) {
    $ReportPath = Join-Path $stressRepoRoot ".deployment-cache/stress-live-$runId.json"
}
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($ReportPath))

$handler = [Net.Http.HttpClientHandler]::new()
$handler.UseProxy = $false
$handler.AllowAutoRedirect = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.BaseAddress = [uri]"http://127.0.0.1:$Port/"
$client.Timeout = [TimeSpan]::FromSeconds(5)

function Read-StressJson([string]$Path) {
    $response = $client.GetAsync($Path).GetAwaiter().GetResult()
    try {
        $null = $response.EnsureSuccessStatusCode()
        return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    }
    finally { $response.Dispose() }
}
function Read-StressQueue {
    # The first read requests a fresh main-thread publication but returns the
    # previous cache immediately. Give Update a chance to publish before the
    # observation read. Both requests are read-only; simulation sends below
    # retain their single-attempt contract.
    $null = Read-StressJson 'api/config/interactions'
    Start-Sleep -Milliseconds 100
    $state = Read-StressJson 'api/config/interactions'
    $snapshot = [ordered]@{}
    foreach ($name in @('ready', 'available', 'interactionsEnabled', 'queuePaused',
        'queueCount', 'activeCount', 'pendingCount', 'backlogCount', 'maxActive')) {
        $snapshot[$name] = if ($state.PSObject.Properties[$name]) { $state.$name } else { $null }
    }
    return $snapshot
}
function Send-StressSimulationOnce([string]$Path) {
    # HttpClient can transparently retry GET after a connection failure. This
    # GET schedules work, so use exactly one socket/write attempt instead.
    # Ordinary read-only snapshots may continue to use HttpClient.
    $socket = [Net.Sockets.TcpClient]::new()
    try {
        $socket.ReceiveTimeout = 5000
        $socket.SendTimeout = 5000
        $connecting = $socket.ConnectAsync('127.0.0.1', $Port)
        if (!$connecting.Wait(5000)) { throw 'Timeout conectando al simulador local.' }
        $null = $connecting.GetAwaiter().GetResult()
        $stream = $socket.GetStream()
        $request = "GET /$Path HTTP/1.1`r`nHost: 127.0.0.1:$Port`r`nConnection: close`r`n`r`n"
        $bytes = [Text.Encoding]::ASCII.GetBytes($request)
        $stream.Write($bytes, 0, $bytes.Length)
        $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $false, 1024, $true)
        try {
            $statusLine = $reader.ReadLine()
            if ($null -eq $statusLine -or $statusLine -notmatch '^HTTP/1\.[01] ([0-9]{3})(?: .*)?$') {
                throw 'El simulador cerro la conexion sin una respuesta HTTP valida.'
            }
            $statusCode = [int]$Matches[1]
            $headerLength = $statusLine.Length
            while ($true) {
                $line = $reader.ReadLine()
                if ($null -eq $line) { throw 'Respuesta HTTP incompleta del simulador.' }
                $headerLength += $line.Length + 2
                if ($headerLength -gt 65536) { throw 'Cabeceras del simulador demasiado grandes.' }
                if ($line.Length -eq 0) { break }
            }
            $body = [Text.StringBuilder]::new()
            $buffer = [char[]]::new(1024)
            while (($length = $reader.Read($buffer, 0, $buffer.Length)) -gt 0) {
                if ($body.Length + $length -gt 65536) { throw 'Respuesta del simulador demasiado grande.' }
                $null = $body.Append($buffer, 0, $length)
            }
            return [pscustomobject]@{ StatusCode = $statusCode; Body = $body.ToString() }
        }
        finally { $reader.Dispose() }
    }
    finally { $socket.Dispose() }
}
function Get-StressPercentile($Values, [double]$Percentile) {
    if ($Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    return [math]::Round($sorted[[math]::Max(0, [math]::Ceiling($sorted.Count * $Percentile) - 1)], 3)
}

$attempted = 0
$accepted = 0
$rejected429 = 0
$uncertain = 0
$httpErrors = 0
$failure = $null
$before = $null
$after = $null
$queueBefore = $null
$queueAfter = $null
$latencies = [Collections.Generic.List[double]]::new()
$acceptedByType = [ordered]@{ like = 0; gift = 0; follow = 0 }
$acceptedByGift = [ordered]@{}
$giftPool = @()
$giftRandom = $null
$startedUtc = [DateTime]::UtcNow.ToString('o')
$sendClock = [Diagnostics.Stopwatch]::new()
try {
    $before = Read-StressJson 'api/dashboard'
    if (!$before.ready) { throw 'El panel local todavia no esta listo.' }
    $queueBefore = Read-StressQueue
    if ($RandomGifts) {
        $rules = Read-StressJson 'api/config/interactions/rules'
        if (!$rules.ready) { throw 'Las reglas del panel todavia no estan listas.' }
        $catalog = Read-StressJson 'assets/creator-tools/gifts/catalog.json'
        $knownGiftIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($gift in $catalog.gifts) { $null = $knownGiftIds.Add([string]$gift.giftId) }
        $giftPool = @($rules.rules | Where-Object {
            $_.enabled -and $_.platform -eq 'tiktok' -and $_.eventType -eq 'gift' -and
            $knownGiftIds.Contains([string]$_.giftId)
        } | ForEach-Object { [string]$_.giftId } | Sort-Object -Unique)
        if ($giftPool.Count -eq 0) { throw 'No hay regalos de catalogo con reglas TikTok activas para esta prueba.' }
        $giftRandom = [Random]::new($RandomSeed)
        Write-Host "Regalos aleatorios: $($giftPool.Count) IDs unicos con reglas activas; semilla $RandomSeed."
    }
    Write-Host "Prueba local: $Events eventos, hasta $Rate eventos/s, tipo $Type. Ctrl+C detiene nuevos envios."
    $sendClock.Start()
    $nextSendAt = 0d
    $nextProgressAt = 0d
    for ($index = 0; $index -lt $Events; $index++) {
        while ($sendClock.Elapsed.TotalMilliseconds -lt $nextSendAt) {
            $remaining = $nextSendAt - $sendClock.Elapsed.TotalMilliseconds
            Start-Sleep -Milliseconds ([int][math]::Max(1, [math]::Min(50, $remaining)))
        }
        $eventType = $Type.ToLowerInvariant()
        if ($Type -eq 'Mixed') {
            $eventType = if ($index % 10 -eq 0) { 'gift' } elseif ($index % 10 -eq 1) { 'follow' } else { 'like' }
        }
        # Follows use new identities; likes/gifts share a viewer pool so their
        # normal per-viewer accumulation is exercised.
        $viewer = if ($eventType -eq 'follow') { "follow-$index" } else { [string]($index % $Viewers) }
        $userId = "stress-$runId-$viewer"
        $query = 'api/dashboard/simulate?platform=tiktok&type=' + $eventType +
            '&count=1&delaySeconds=0&user=' + [uri]::EscapeDataString("STRESS-$viewer") +
            '&userId=' + [uri]::EscapeDataString($userId) +
            '&userDisplayName=' + [uri]::EscapeDataString("STRESS-$viewer")
        $eventGiftId = $null
        if ($eventType -eq 'gift') {
            $eventGiftId = if ($RandomGifts) { $giftPool[$giftRandom.Next($giftPool.Count)] } else { $GiftId }
            $query += '&giftId=' + [uri]::EscapeDataString($eventGiftId)
        }
        $attempted++
        $requestStartedAt = $sendClock.Elapsed.TotalMilliseconds
        $requestClock = [Diagnostics.Stopwatch]::StartNew()
        try { $response = Send-StressSimulationOnce $query }
        catch {
            # The API creates fresh IDs server-side, so an uncertain response
            # must never be retried automatically: it could duplicate events.
            $uncertain++
            throw
        }
        try {
            $status = [int]$response.StatusCode
            if ($status -eq 202) {
                try {
                    $body = $response.Body | ConvertFrom-Json
                    if ($null -eq $body -or !$body.PSObject.Properties['ok'] -or !$body.ok) {
                        throw 'Respuesta sin confirmacion del simulador.'
                    }
                }
                catch {
                    $uncertain++
                    throw 'HTTP 202 sin confirmacion valida: el evento podria estar aceptado y no se reintentara.'
                }
                $accepted++
                $acceptedByType[$eventType]++
                if ($eventType -eq 'gift') {
                    if (!$acceptedByGift.Contains($eventGiftId)) { $acceptedByGift[$eventGiftId] = 0 }
                    $acceptedByGift[$eventGiftId]++
                }
            }
            elseif ($status -eq 429) { $rejected429++ }
            else {
                $httpErrors++
                throw "Simulador rechazo HTTP ${status}. No se reintentara el evento."
            }
        }
        finally {
            $requestClock.Stop()
            $latencies.Add($requestClock.Elapsed.TotalMilliseconds)
        }
        # Never catch up a late request with an unintended burst.
        $nextSendAt = $requestStartedAt + 1000d / $Rate
        if ($sendClock.Elapsed.TotalSeconds -ge $nextProgressAt) {
            Write-Progress -Activity 'Eventos de stress' -Status "$accepted aceptados, $rejected429 rechazados por cola llena" -PercentComplete (100d * $attempted / $Events)
            $nextProgressAt = $sendClock.Elapsed.TotalSeconds + 1d
        }
    }
}
catch { $failure = $_.Exception.Message }
finally {
    $sendClock.Stop()
    Write-Progress -Activity 'Eventos de stress' -Completed
    try {
        # HTTP 202 acknowledges scheduling. Allow the background worker to
        # finish pending simulations before taking the final shared snapshot.
        $settleClock = [Diagnostics.Stopwatch]::StartNew()
        do {
            $after = Read-StressJson 'api/dashboard'
            if ($null -eq $before -or $before.streamSessionId -ne $after.streamSessionId -or
                ($after.counters.received - $before.counters.received) -ge $accepted) { break }
            Start-Sleep -Milliseconds 100
        } while ($settleClock.Elapsed.TotalSeconds -lt 5)
        $queueAfter = Read-StressQueue
    }
    catch { if (!$failure) { $failure = $_.Exception.Message } }
    $comparable = $null -ne $before -and $null -ne $after -and
        $before.streamSessionId -eq $after.streamSessionId
    $counterDelta = $null
    if ($comparable) {
        $counterDelta = [ordered]@{}
        foreach ($name in @('received', 'matched', 'queued', 'ignored', 'gifts', 'likes', 'follows')) {
            $counterDelta[$name] = $after.counters.$name - $before.counters.$name
        }
    }
    $report = [ordered]@{
        schemaVersion = 1; mode = 'live_dashboard_simulation'; runId = $runId
        startedUtc = $startedUtc; port = $Port; eventType = $Type
        requestedEvents = $Events; maximumEventsPerSecond = $Rate; viewerPool = $Viewers
        attempted = $attempted; accepted = $accepted; acceptedByType = $acceptedByType
        randomGifts = [bool]$RandomGifts; randomSeed = if ($RandomGifts) { $RandomSeed } else { $null }
        giftPool = $giftPool; acceptedByGift = $acceptedByGift
        rejected429 = $rejected429; uncertain = $uncertain; httpErrors = $httpErrors
        sendingSeconds = [math]::Round($sendClock.Elapsed.TotalSeconds, 3)
        achievedRequestsPerSecond = if ($sendClock.Elapsed.TotalSeconds -gt 0) { [math]::Round($attempted / $sendClock.Elapsed.TotalSeconds, 2) } else { 0 }
        httpLatencyMilliseconds = @{ p50 = Get-StressPercentile $latencies 0.5; p95 = Get-StressPercentile $latencies 0.95 }
        countersComparable = $comparable; sharedCounterDelta = $counterDelta
        queueBefore = $queueBefore; queueAfter = $queueAfter; error = $failure
        completed = $null -eq $failure -and $attempted -eq $Events
        accountingScope = 'Shared dashboard counters include any real traffic; deltas cannot prove per-run losslessness.'
        measurementScope = 'Simulation API, worker, rules and gameplay queues; bypasses companion/transport normalization. HTTP latency is not frame time or FPS.'
    }
    try {
        $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding utf8
        Write-Host "Reporte: $ReportPath"
        Write-Host "Solicitudes: $attempted; aceptadas: $accepted; cola llena: $rejected429; inciertas: $uncertain."
    }
    finally { $client.Dispose() }
}
if ($failure) { throw $failure }
