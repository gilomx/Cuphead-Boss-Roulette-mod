param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,
    [switch]$TwitchControl,
    [switch]$LegacyPreamble
)

$ErrorActionPreference = "Stop"
$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = (Resolve-Path -LiteralPath $Executable).Path
$startInfo.Arguments = "--parent-pid $PID"
if ($TwitchControl) { $startInfo.Arguments += ' --twitch-control' }
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.RedirectStandardInput = [bool]$TwitchControl

$companionProcess = [System.Diagnostics.Process]::Start($startInfo)
if ($null -eq $companionProcess) {
    throw "Could not start the published companion."
}

function Read-ProtocolLine {
    param([System.IO.StreamReader]$Reader)

    $readTask = $Reader.ReadLineAsync()
    if (-not $readTask.Wait([TimeSpan]::FromSeconds(5))) {
        throw "Timed out waiting for a companion protocol message."
    }
    return $readTask.Result
}

try {
    $starting = Read-ProtocolLine -Reader $companionProcess.StandardOutput
    $connecting = Read-ProtocolLine -Reader $companionProcess.StandardOutput
    $controlAcknowledged = $false
    if ($TwitchControl) {
        # Match the legacy host: ASCII, no BOM, one flushed line per command.
        $commandBytes = [Text.Encoding]::ASCII.GetBytes("twitch:cancel:1`n")
        if ($LegacyPreamble) { $commandBytes = [Text.Encoding]::UTF8.GetPreamble() + $commandBytes }
        $companionProcess.StandardInput.BaseStream.Write($commandBytes, 0, $commandBytes.Length)
        $companionProcess.StandardInput.BaseStream.Flush()
        $controlDeadline = [DateTime]::UtcNow.AddSeconds(15)
        do {
            $controlStatus = (Read-ProtocolLine -Reader $companionProcess.StandardOutput) | ConvertFrom-Json
            if ($controlStatus.kind -eq 'status' -and $controlStatus.connectionId -eq 'twitch' -and
                $controlStatus.controlRevision -eq 1) { $controlAcknowledged = $true; break }
        } while ([DateTime]::UtcNow -lt $controlDeadline)
        if (!$controlAcknowledged) { throw 'Published companion did not acknowledge its control pipe.' }
    }
}
catch {
    if (-not $companionProcess.HasExited) {
        $companionProcess.Kill()
    }
    throw
}
if ([string]::IsNullOrWhiteSpace($starting) -or
    [string]::IsNullOrWhiteSpace($connecting)) {
    if (-not $companionProcess.HasExited) {
        $companionProcess.Kill()
    }
    throw "The companion did not emit its initial NDJSON statuses."
}

$startedAtTicks = $companionProcess.StartTime.ToUniversalTime().Ticks
[Console]::Out.WriteLine(
    "$($companionProcess.Id)`t$startedAtTicks`t$starting`t$connecting`t$controlAcknowledged")
[Console]::Out.Flush()

# Returning from this helper ends the process whose PID was passed to the
# companion. The outer smoke test verifies that the companion follows it.
