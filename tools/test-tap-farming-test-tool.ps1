#requires -Version 7.2
[CmdletBinding()]
param()

# Runs only the actual CLI against an ephemeral recording fixture. Never use
# the game's fixed port, an account, a settings file or a live session here.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$testCache = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $PSScriptRoot) '.deployment-cache'))
$testRoot = Join-Path $testCache ('tap-tool-tests-' + [guid]::NewGuid().ToString('N'))
$testTool = Join-Path $PSScriptRoot 'send-tap-farming-test.ps1'
$testPwsh = (Get-Command pwsh -ErrorAction Stop).Source
if (!('Gilomx.TapToolTests.Server' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace Gilomx.TapToolTests {
    public sealed class Server : IDisposable {
        private readonly TcpListener listener;
        private readonly Thread worker;
        private readonly string mode;
        private readonly object gate = new object();
        private readonly List<string> requests = new List<string>();
        private readonly List<double> times = new List<double>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private volatile bool stopping;
        private int attempted, accepted, farmReads;
        public int Port { get; private set; }
        public string[] Requests { get { lock(gate) return requests.ToArray(); } }
        public double[] Times { get { lock(gate) return times.ToArray(); } }
        public Server(string mode) {
            this.mode = mode;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            if (Port == 18081) { listener.Stop(); throw new Exception("Live game port forbidden."); }
            worker = new Thread(Run); worker.IsBackground = true; worker.Start();
        }
        private void Run() {
            while (!stopping) {
                try {
                    using (var client = listener.AcceptTcpClient()) {
                        client.ReceiveTimeout = 2000;
                        var stream = client.GetStream();
                        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true)) {
                            var first = reader.ReadLine();
                            for (var line = reader.ReadLine(); !string.IsNullOrEmpty(line); line = reader.ReadLine()) { }
                            Handle(stream, first);
                        }
                    }
                } catch { if (stopping) return; }
            }
        }
        private void Handle(NetworkStream stream, string request) {
            if (request == null) return;
            lock(gate) requests.Add(request);
            var parts = request.Split(' ');
            if (parts.Length < 2) return;
            var uri = new Uri("http://127.0.0.1:" + Port + parts[1]);
            if (uri.AbsolutePath == "/api/dashboard/simulate") {
                lock(gate) times.Add(clock.Elapsed.TotalMilliseconds);
                attempted++;
                if (mode == "http-error") { Reply(stream, 500, "stub failure"); return; }
                accepted++;
                if (mode == "lost-response") return;
                if (mode == "slow-once" && attempted == 1) Thread.Sleep(250);
                Reply(stream, 202, "{\"ok\":true}"); return;
            }
            if (uri.AbsolutePath == "/api/config/interactions") {
                bool enabled = mode == "master-preflight" || (mode == "master-on" && accepted >= 3);
                Reply(stream, 200, "{\"ready\":true,\"interactionsEnabled\":" + (enabled ? "true" : "false") + "}"); return;
            }
            if (uri.AbsolutePath == "/api/config/tap-farming") {
                farmReads++;
                string phase = mode == "off-preflight" || (mode == "farm-off" && accepted >= 3) ? "off" :
                    mode == "completed" && accepted >= 3 ? "completed" :
                    mode == "retry" && farmReads == 2 ? "collecting" : "active";
                string level = mode == "wrong-level" || (mode == "retry" && phase == "collecting") ? "Other" : "Robot";
                int session = mode == "session-change" && accepted >= 3 ? 2 : 1;
                int attempt = mode == "retry" && farmReads >= 3 ? 2 : 1;
                int conversion = mode == "conversion-change" && accepted >= 3 ? 4 : 2;
                int spent = accepted >= 4 ? 7 : 0;
                int converted = 5000 + accepted / 2;
                bool enabled = phase == "active" || phase == "collecting" || phase == "transition";
                string metadata = mode == "slow-once" ? ",\"effectiveHealth\":{\"available\":true,\"scope\":\"boss\"," +
                    "\"current\":" + (2000 + converted - spent) + ",\"total\":2000,\"ratio\":" +
                    ((2000d + converted - spent) / 2000d).ToString(CultureInfo.InvariantCulture) +
                    "},\"phases\":[{\"index\":1,\"reserveHealth\":1250,\"progress\":0.25},{\"index\":2,\"progress\":0}]" : "";
                Reply(stream, 200, "{\"ready\":true,\"enabled\":" + (enabled ? "true" : "false") +
                    ",\"phase\":\"" + phase + "\",\"sessionId\":" + session + ",\"attempt\":" + attempt +
                    ",\"levelId\":\"" + level + "\",\"bossName\":\"Robot\",\"phaseIndex\":2," +
                    "\"conversion\":{\"tapsPerConversion\":" + conversion + ",\"healthPointsPerConversion\":1}," +
                    "\"counters\":{\"totalTaps\":" + (10000 + accepted) + ",\"unconvertedTaps\":" + accepted % 2 +
                    ",\"convertedHealth\":" + converted + ",\"reserveHealth\":" + (converted - spent) +
                    ",\"spentHealth\":" + spent + "}" + metadata + "}"); return;
            }
            Reply(stream, 404, "unexpected route");
        }
        private static void Reply(NetworkStream stream, int status, string body) {
            var bytes = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Stub\r\nContent-Type: application/json\r\nContent-Length: " +
                bytes.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(header, 0, header.Length); stream.Write(bytes, 0, bytes.Length); stream.Flush();
        }
        public void Dispose() { stopping = true; listener.Stop(); worker.Join(3000); }
    }
}
'@
}
function Check($Value, [string]$Message) { if (!$Value) { throw $Message } }
function Assert-TestPath([string]$Path) {
    $target = [IO.Path]::GetFullPath($Path)
    if (!$target.StartsWith($testCache + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Tap tool fixture escaped the workspace cache.'
    }
    $current = $target
    while ($current.Length -ge $testCache.Length) {
        if ((Test-Path -LiteralPath $current) -and
            ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Tap tool fixture paths must not contain reparse points.'
        }
        if ($current.Equals($testCache, [StringComparison]::OrdinalIgnoreCase)) { break }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}
function Run-Case([string]$Name, [string]$Mode, [int]$Duration, [int]$Rate, [scriptblock]$Assert, [switch]$StopFile) {
    $server = [Gilomx.TapToolTests.Server]::new($Mode)
    $reportPath = Join-Path $testRoot ($Name + '.json')
    Assert-TestPath $reportPath
    try {
        $arguments = @('-NoProfile', '-NonInteractive', '-File', $testTool, '-DurationSeconds', [string]$Duration,
            '-Rate', [string]$Rate, '-Port', [string]$server.Port, '-ReportPath', $reportPath)
        if ($StopFile) {
            $stopPath = Join-Path $testRoot ($Name + '.stop')
            Assert-TestPath $stopPath
            Set-Content -LiteralPath $stopPath -Value 'stop'
            $arguments += @('-StopFilePath', $stopPath)
        }
        $output = @(& $testPwsh @arguments 2>&1)
        $exitCode = $LASTEXITCODE
        Check (Test-Path -LiteralPath $reportPath) "No report for $Name`: $($output -join ' ')"
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $requests = @($server.Requests)
        $mutations = @($requests | Where-Object { $_.StartsWith('GET /api/dashboard/simulate?') })
        foreach ($request in $requests) {
            $parts = $request.Split(' ')
            $uri = [uri]('http://127.0.0.1' + $parts[1])
            Check ($parts[0] -eq 'GET' -and $uri.AbsolutePath -in @('/api/config/tap-farming',
                '/api/config/interactions', '/api/dashboard/simulate')) 'Tool attempted a configuration/session/reserve mutation.'
        }
        foreach ($request in $mutations) {
            $values = @{}
            foreach ($pair in ([uri]('http://127.0.0.1' + $request.Split(' ')[1])).Query.TrimStart('?').Split('&')) {
                $parts = $pair.Split('=', 2)
                $values[[uri]::UnescapeDataString($parts[0])] = [uri]::UnescapeDataString($parts[1])
            }
            Check ($values.platform -eq 'tiktok' -and $values.type -eq 'like' -and $values.count -eq '1' -and
                $values.user -eq 'TAPS-PRUEBA' -and $values.userId -eq $report.runId) 'A request was bulk, misidentified or used the wrong event type.'
        }
        Check ($report.attempted -eq $mutations.Count -and $report.attempted -eq
            $report.accepted + $report.uncertain + $report.rejected429 + $report.httpErrors) 'Request accounting or one-attempt contract failed.'
        Check ($report.before.reserveHealth -eq 5000 -and $report.conversion.tapsPerConversion -eq 2 -and
            $report.conversion.healthPointsPerConversion -eq 1) 'Initial reserve/conversion was not preserved in the report.'
        Check (@($report.samples).Count -le 3601 -and @($report.samples).Count -le $Duration + 1) 'Samples grew beyond one per second.'
        if ($Mode -ne 'slow-once') {
            Check ($null -eq $report.before.effectiveHealth -and @($report.before.phases).Count -eq 0) 'Old runtime responses without optional health/phase metadata were not supported.'
        }
        try { & $Assert ([pscustomobject]@{ Report=$report; ExitCode=$exitCode; Requests=$mutations; Times=@($server.Times) }) }
        catch { throw ($_.Exception.Message + ' Report: ' + ($report | ConvertTo-Json -Compress -Depth 9) + ' CLI: ' + ($output -join ' ')) }
        Write-Output "PASS isolated continuous taps: $Name"
    }
    finally { $server.Dispose() }
}
try {
    Assert-TestPath $testRoot
    $null = [IO.Directory]::CreateDirectory($testRoot)
    Run-Case 'count-one-rate-no-catchup' 'slow-once' 2 20 {
        param($r)
        Check ($r.ExitCode -eq 0 -and $r.Report.stopReason -eq 'duration_elapsed' -and
            $r.Report.accepted -ge 20 -and $r.Report.accepted -le 40) 'Duration/rate run failed.'
        for ($index = 1; $index -lt $r.Times.Count; $index++) {
            Check ($r.Times[$index] - $r.Times[$index - 1] -ge 30) 'Late sends were repaid as a catch-up burst.'
        }
        Check ($r.Report.sharedCounterDelta.totalTaps -eq $r.Report.accepted -and
            $r.Report.sharedCounterDelta.reserveAccountingBalanced -and
            $r.Report.sharedCounterDelta.spentHealth -eq 7) 'Observed conversion/reserve/spending relationship failed.'
        Check ($r.Report.before.effectiveHealth.current -eq 7000 -and $r.Report.before.effectiveHealth.total -eq 2000 -and
            $r.Report.before.effectiveHealth.ratio -eq 3.5 -and $r.Report.before.effectiveHealth.scope -eq 'boss') 'Effective health metadata was altered or capped.'
        Check ($r.Report.before.phases[0].index -eq 1 -and $r.Report.before.phases[0].reserveHealth -eq 1250 -and
            $r.Report.before.phases[0].progress -eq 0.25 -and $null -eq $r.Report.before.phases[1].reserveHealth) 'Phase metadata or optional reserve fields were not retained.'
    }
    Run-Case 'death-collecting-retry' 'retry' 3 10 {
        param($r)
        Check ($r.ExitCode -eq 0 -and $r.Report.accepted -ge 20 -and $r.Report.stopReason -eq 'duration_elapsed') 'Death stopped the same farming session.'
        Check (@($r.Report.samples | Where-Object phase -eq 'collecting').Count -gt 0 -and
            @($r.Report.samples | Where-Object attempt -eq 2).Count -gt 0) 'Retry samples did not retain collecting and the next attempt.'
    }
    foreach ($mode in @('off-preflight', 'master-preflight', 'wrong-level')) {
        Run-Case $mode $mode 1 20 {
            param($r)
            Check ($r.ExitCode -ne 0 -and $r.Report.attempted -eq 0) 'Invalid preflight sent a tap.'
        }
    }
    foreach ($pair in @(@('farm-off', 'farming_off'), @('completed', 'farming_completed'),
        @('master-on', 'interactions_enabled'), @('session-change', 'session_changed'), @('conversion-change', 'conversion_changed'))) {
        $expectedReason = $pair[1]
        Run-Case $pair[0] $pair[0] 3 20 {
            param($r)
            Check ($r.ExitCode -eq 0 -and $r.Report.stopReason -eq $expectedReason -and
                $r.Report.sendingSeconds -lt 2.5 -and $r.Report.accepted -le 25) 'A changed guard failed to stop new sends.'
            if ($expectedReason -eq 'session_changed') {
                Check (!$r.Report.countersComparable -and $null -eq $r.Report.sharedCounterDelta) 'A new session produced a misleading counter delta.'
            }
        }
    }
    Run-Case 'uncertain-no-retry' 'lost-response' 2 20 {
        param($r)
        Check ($r.ExitCode -ne 0 -and $r.Report.uncertain -eq 1 -and $r.Report.attempted -eq 1 -and
            $r.Requests.Count -eq 1 -and $r.Report.accepted -eq 0) 'An accepted request with a lost response was retried or miscounted.'
    }
    Run-Case 'http-error-no-retry' 'http-error' 2 20 {
        param($r)
        Check ($r.ExitCode -ne 0 -and $r.Report.httpErrors -eq 1 -and $r.Report.attempted -eq 1 -and
            $r.Requests.Count -eq 1) 'A rejected HTTP request was retried.'
    }
    Run-Case 'stop-file-reserve-preserved' 'success' 2 20 {
        param($r)
        Check ($r.ExitCode -eq 0 -and $r.Report.stopReason -eq 'stop_file' -and $r.Report.attempted -eq 0 -and
            $r.Report.after.reserveHealth -eq 5000 -and $r.Report.after.totalTaps -eq 10000) 'Stop file changed existing reserve or emitted a tap.'
    } -StopFile
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Assert-TestPath $testRoot
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
Write-Output 'PASS 13 isolated continuous-tap groups; no game requests, settings changes or session/reserve reset.'
