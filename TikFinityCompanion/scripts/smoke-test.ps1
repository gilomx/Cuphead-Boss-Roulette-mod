[CmdletBinding()]
param(
    [string]$Executable,
    [switch]$TwitchControl,
    [switch]$LegacyPreamble
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Executable)) {
    $Executable = Join-Path $projectRoot `
        "artifacts\win-x64\companion\LaPichiRuleta.TikFinity.exe"
}
$resolvedExecutable = (Resolve-Path -LiteralPath $Executable).Path
$helper = Join-Path $projectRoot "tests\parent-exit-helper.ps1"

$helperArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $helper,
    '-Executable', $resolvedExecutable)
if ($TwitchControl) { $helperArguments += '-TwitchControl' }
if ($LegacyPreamble) { $helperArguments += '-LegacyPreamble' }
$record = & powershell.exe @helperArguments
if ($LASTEXITCODE -ne 0) {
    throw "The parent-lifetime helper failed with exit code $LASTEXITCODE"
}

$parts = @($record -split "`t", 5)
if ($parts.Count -ne 5) {
    throw "The parent-lifetime helper returned an invalid record."
}
if ($TwitchControl -and $parts[4] -ne 'True') { throw 'Twitch command pipe was not acknowledged.' }

$childProcessId = 0
if (-not [int]::TryParse($parts[0], [ref]$childProcessId) -or
    $childProcessId -le 0) {
    throw "The parent-lifetime helper returned an invalid process ID."
}

$childStartedAtTicks = 0L
if (-not [long]::TryParse($parts[1], [ref]$childStartedAtTicks) -or
    $childStartedAtTicks -le 0) {
    throw "The parent-lifetime helper returned an invalid process start time."
}

$starting = $parts[2] | ConvertFrom-Json
$connecting = $parts[3] | ConvertFrom-Json
if ($starting.protocolVersion -ne 1 -or
    $starting.kind -ne "status" -or
    $starting.state -ne "starting") {
    throw "The first protocol message was not a valid starting status."
}
if ($connecting.protocolVersion -ne 1 -or
    $connecting.kind -ne "status" -or
    $connecting.state -ne "connecting") {
    throw "The second protocol message was not a valid connecting status."
}

$deadline = [DateTime]::UtcNow.AddSeconds(5)
do {
    $running = Get-Process -Id $childProcessId -ErrorAction SilentlyContinue
    if ($null -eq $running) {
        break
    }
    Start-Sleep -Milliseconds 100
} while ([DateTime]::UtcNow -lt $deadline)

$remainingProcess = Get-Process -Id $childProcessId -ErrorAction SilentlyContinue
if ($null -ne $remainingProcess -and
    $remainingProcess.StartTime.ToUniversalTime().Ticks -eq $childStartedAtTicks) {
    $remainingProcess.Kill()
    throw "The companion stayed alive after its parent process exited."
}

[pscustomobject]@{
    Executable = $resolvedExecutable
    StartingStatus = $starting.state
    ConnectingStatus = $connecting.state
    ExitedWithParent = $true
    TwitchControl = [bool]$TwitchControl
    ControlAcknowledged = $parts[4] -eq 'True'
    LegacyPreamble = [bool]$LegacyPreamble
} | ConvertTo-Json -Compress
