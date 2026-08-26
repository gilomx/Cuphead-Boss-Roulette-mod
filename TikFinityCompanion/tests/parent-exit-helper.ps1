param(
    [Parameter(Mandatory = $true)]
    [string]$Executable
)

$ErrorActionPreference = "Stop"
$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = (Resolve-Path -LiteralPath $Executable).Path
$startInfo.Arguments = "--parent-pid $PID"
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true

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
    "$($companionProcess.Id)`t$startedAtTicks`t$starting`t$connecting")
[Console]::Out.Flush()

# Returning from this helper ends the process whose PID was passed to the
# companion. The outer smoke test verifies that the companion follows it.
