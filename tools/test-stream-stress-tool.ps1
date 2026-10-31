#requires -Version 7.2
[CmdletBinding()]
param()

# Exercises the actual CLI against an isolated recording HTTP server. The
# server owns an ephemeral loopback port; no request targets the live game.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$testRepoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$testCacheRoot = Join-Path $testRepoRoot '.deployment-cache'
$testRunRoot = Join-Path $testCacheRoot ('stress-tool-tests-' + [guid]::NewGuid().ToString('N'))
$testTool = Join-Path $PSScriptRoot 'send-stream-stress.ps1'
$testPwsh = (Get-Command pwsh -ErrorAction Stop).Source

if (!('Gilomx.StressToolTests.StubServer' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace Gilomx.StressToolTests {
    public sealed class StubServer : IDisposable {
        private readonly TcpListener listener;
        private readonly Thread worker;
        private readonly object gate = new object();
        private readonly List<string> requests = new List<string>();
        private readonly List<string> allRequests = new List<string>();
        private readonly List<double> times = new List<double>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly string mode;
        private volatile bool stopping;
        private int attempted, accepted, likes, gifts, follows;
        private double queueRefreshRequestedAt = -1;
        private int cachedQueueCount = 999;
        public int Port { get; private set; }
        public string[] Requests { get { lock(gate) return requests.ToArray(); } }
        public string[] AllRequests { get { lock(gate) return allRequests.ToArray(); } }
        public double[] EventTimes { get { lock(gate) return times.ToArray(); } }
        public StubServer(string mode) {
            this.mode = mode;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            if (Port == 18081) { listener.Stop(); throw new Exception("Live game port forbidden in isolated tests."); }
            worker = new Thread(Run); worker.IsBackground = true; worker.Start();
        }
        private void Run() {
            while (!stopping) {
                try {
                    using (var client = listener.AcceptTcpClient()) {
                        client.ReceiveTimeout = 2000;
                        var stream = client.GetStream();
                        var header = new StringBuilder();
                        while (header.Length < 65536) {
                            int next = stream.ReadByte(); if (next < 0) break;
                            header.Append((char)next);
                            if (header.Length >= 4 && header.ToString().EndsWith("\r\n\r\n")) break;
                        }
                        Handle(stream, header.ToString());
                    }
                } catch { if (stopping) return; }
            }
        }
        private void Handle(NetworkStream stream, string header) {
            var firstLine = header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
            var parts = firstLine.Split(' ');
            if (parts.Length < 2) return;
            lock(gate) allRequests.Add(firstLine);
            var uri = new Uri("http://127.0.0.1:" + Port + parts[1]);
            if (uri.AbsolutePath == "/api/dashboard/simulate") {
                var fields = new Dictionary<string, string>();
                foreach (var part in uri.Query.TrimStart('?').Split('&')) {
                    var pair = part.Split(new[] {'='}, 2);
                    fields[WebUtility.UrlDecode(pair[0])] = pair.Length > 1 ? WebUtility.UrlDecode(pair[1]) : "";
                }
                lock(gate) { requests.Add(firstLine); times.Add(clock.Elapsed.TotalMilliseconds); }
                attempted++;
                if (mode == "429" && attempted == 2) { Reply(stream, 429, "not-json"); return; }
                if (mode == "500") { Reply(stream, 500, "stub failure, not JSON"); return; }
                accepted++;
                if (fields["type"] == "like") likes++;
                if (fields["type"] == "gift") gifts++;
                if (fields["type"] == "follow") follows++;
                // The request is accepted, but the response vanishes. A
                // mutating GET must not be retried by the CLI OR its client.
                if (mode == "close") return;
                Reply(stream, 202, mode == "malformed202" ? "not-json" : "{\"ok\":true,\"queued\":true}");
                return;
            }
            if (uri.AbsolutePath == "/api/dashboard") {
                string session = mode == "session-change" && attempted > 0 ? "stub-session-two" : "stub-session-one";
                Reply(stream, 200, "{\"ready\":true,\"schemaVersion\":2,\"streamSessionId\":\"" + session +
                    "\",\"counters\":{\"received\":" + accepted + ",\"matched\":0,\"queued\":0,\"ignored\":0," +
                    "\"gifts\":" + gifts + ",\"likes\":" + likes + ",\"follows\":" + follows + ",\"coins\":0},\"events\":[]}");
                return;
            }
            if (uri.AbsolutePath == "/api/config/interactions") {
                int queueCount = 0;
                if (mode == "panel-demand") {
                    // Model a cached GET that only requests a later Unity
                    // update. The first response is intentionally stale.
                    if (queueRefreshRequestedAt >= 0 &&
                        clock.Elapsed.TotalMilliseconds - queueRefreshRequestedAt >= 75) {
                        cachedQueueCount = accepted;
                        queueRefreshRequestedAt = -1;
                    } else if (queueRefreshRequestedAt < 0) {
                        queueRefreshRequestedAt = clock.Elapsed.TotalMilliseconds;
                    }
                    queueCount = cachedQueueCount;
                }
                Reply(stream, 200, "{\"ready\":true,\"available\":true,\"interactionsEnabled\":true,\"queuePaused\":false," +
                    "\"queueCount\":" + queueCount + ",\"activeCount\":0,\"pendingCount\":" + queueCount +
                    ",\"backlogCount\":0,\"maxActive\":10}");
                return;
            }
            if (uri.AbsolutePath == "/api/config/interactions/rules") {
                Reply(stream, 200, "{\"ready\":true,\"engineActive\":true,\"rules\":[" +
                    "{\"enabled\":true,\"platform\":\"tiktok\",\"eventType\":\"gift\",\"giftId\":\"5655\"}," +
                    "{\"enabled\":true,\"platform\":\"tiktok\",\"eventType\":\"gift\",\"giftId\":\"5655\"}," +
                    "{\"enabled\":true,\"platform\":\"tiktok\",\"eventType\":\"gift\",\"giftId\":\"6064\"}," +
                    "{\"enabled\":false,\"platform\":\"tiktok\",\"eventType\":\"gift\",\"giftId\":\"9998\"}," +
                    "{\"enabled\":true,\"platform\":\"twitch\",\"eventType\":\"gift\",\"giftId\":\"7777\"}," +
                    "{\"enabled\":true,\"platform\":\"tiktok\",\"eventType\":\"like\",\"giftId\":\"8888\"}," +
                    "{\"enabled\":true,\"platform\":\"tiktok\",\"eventType\":\"gift\",\"giftId\":\"9999\"}]}");
                return;
            }
            if (uri.AbsolutePath == "/assets/creator-tools/gifts/catalog.json") {
                Reply(stream, 200, "{\"schemaVersion\":1,\"catalogVersion\":\"stub\",\"gifts\":[" +
                    "{\"giftId\":\"5655\",\"name\":\"Rose\",\"coinsPerUnit\":1}," +
                    "{\"giftId\":\"6064\",\"name\":\"GG\",\"coinsPerUnit\":1}," +
                    "{\"giftId\":\"9998\",\"name\":\"Inactive\",\"coinsPerUnit\":9}," +
                    "{\"giftId\":\"7777\",\"name\":\"Twitch only\",\"coinsPerUnit\":2}," +
                    "{\"giftId\":\"8888\",\"name\":\"Wrong event type\",\"coinsPerUnit\":3}]}");
                return;
            }
            Reply(stream, 404, "unexpected isolated test route");
        }
        private static void Reply(NetworkStream stream, int status, string body) {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Stub\r\nContent-Type: application/json\r\n" +
                "Content-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(header, 0, header.Length); stream.Write(bytes, 0, bytes.Length); stream.Flush();
        }
        public void Dispose() { stopping = true; listener.Stop(); worker.Join(3000); }
    }
}
'@
}

function Assert-TestPath([string]$Path) {
    $target = [IO.Path]::GetFullPath($Path)
    $cache = [IO.Path]::GetFullPath($testCacheRoot)
    if (!$target.StartsWith($cache + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Stress CLI test path escaped the workspace cache.'
    }
    $current = $target
    while ($current.Length -ge $cache.Length) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Stress CLI test paths must not contain reparse points.'
            }
        }
        if ($current.Equals($cache, [StringComparison]::OrdinalIgnoreCase)) { break }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}
function Check($Value, [string]$Message) { if (!$Value) { throw $Message } }
function Query-Fields([string]$Request) {
    $uri = [uri]('http://127.0.0.1' + $Request.Split(' ')[1])
    $values = @{}
    foreach ($pair in $uri.Query.TrimStart('?').Split('&')) {
        $parts = $pair.Split('=', 2)
        $values[[uri]::UnescapeDataString($parts[0])] = [uri]::UnescapeDataString($parts[1])
    }
    return $values
}
function Run-Case([string]$Name, [string]$Mode, [int]$Events, [int]$Rate, [string]$Type, [scriptblock]$Assert,
    [string[]]$ExtraArguments = @()) {
    $server = [Gilomx.StressToolTests.StubServer]::new($Mode)
    $path = Join-Path $testRunRoot ($Name + '.json')
    Assert-TestPath $path
    try {
        $arguments = @('-NoProfile', '-NonInteractive', '-File', $testTool,
            '-Events', [string]$Events, '-Rate', [string]$Rate, '-Viewers', '2',
            '-Type', $Type, '-Port', [string]$server.Port, '-ReportPath', $path)
        if ($Type -in @('Gift', 'Mixed') -and $ExtraArguments -notcontains '-RandomGifts') {
            $arguments += @('-GiftId', '5655')
        }
        $arguments += $ExtraArguments
        $output = @(& $testPwsh @arguments 2>&1)
        $exitCode = $LASTEXITCODE
        Check (Test-Path -LiteralPath $path) "CLI did not save its report for $Name`: $($output -join ' ')"
        $report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $result = [pscustomobject]@{ Report = $report; ExitCode = $exitCode; Requests = @($server.Requests); Times = @($server.EventTimes) }
        try { & $Assert $result }
        catch { throw ($_.Exception.Message + ' Report: ' + ($report | ConvertTo-Json -Compress -Depth 8) + ' CLI: ' + ($output -join ' ')) }
        Check ($report.attempted -eq $report.accepted + $report.rejected429 + $report.uncertain + $report.httpErrors) "HTTP accounting does not balance for $Name."
        foreach ($request in $server.AllRequests) {
            $requestParts = $request.Split(' ')
            $requestUri = [uri]('http://127.0.0.1' + $requestParts[1])
            Check ($requestParts[0] -eq 'GET' -and $requestUri.AbsolutePath -in @(
                '/api/dashboard', '/api/config/interactions', '/api/config/interactions/rules',
                '/assets/creator-tools/gifts/catalog.json', '/api/dashboard/simulate')) 'Stress CLI attempted a configuration/control mutation.'
        }
        Write-Output "PASS isolated stress CLI: $Name"
    }
    finally { $server.Dispose() }
}

function Check-RandomGiftSelection($Result, [int]$Seed, [int]$GiftEvents) {
    Check ($Result.ExitCode -eq 0 -and $Result.Report.completed) 'Random gift run failed.'
    Check ($Result.Report.randomGifts -and $Result.Report.randomSeed -eq $Seed) 'Random gift mode/seed was not reported.'
    $pool = @($Result.Report.giftPool)
    Check ($pool.Count -eq 2 -and $pool[0] -eq '5655' -and $pool[1] -eq '6064') 'Gift pool must deduplicate IDs and exclude disabled, non-TikTok, non-gift and unknown-catalog rules.'
    $giftRequests = @($Result.Requests | ForEach-Object { Query-Fields $_ } | Where-Object { $_.type -eq 'gift' })
    Check ($giftRequests.Count -eq $GiftEvents) 'Random selection changed the number of independent gift events.'
    $random = [Random]::new($Seed)
    $expectedCounts = @{ '5655' = 0; '6064' = 0 }
    foreach ($fields in $giftRequests) {
        $expected = @('5655', '6064')[$random.Next(2)]
        Check ($fields.giftId -eq $expected -and $fields.count -eq '1') 'Random gift sequence is not deterministic and uniform by unique ID for the selected seed.'
        $expectedCounts[$expected]++
    }
    foreach ($id in @('5655', '6064')) {
        $reported = $Result.Report.acceptedByGift.PSObject.Properties[$id]
        Check ($null -ne $reported -and $reported.Value -eq $expectedCounts[$id]) 'Accepted gifts were not accounted by ID.'
    }
}

try {
    Assert-TestPath $testRunRoot
    $null = [IO.Directory]::CreateDirectory($testRunRoot)
    Run-Case 'mixed-identities' 'panel-demand' 20 50 'Mixed' {
        param($r)
        Check ($r.ExitCode -eq 0 -and $r.Report.completed -and $r.Report.accepted -eq 20) 'Mixed run did not complete.'
        Check ($r.Requests.Count -eq 20) 'Each event must issue exactly one HTTP request.'
        Check ($r.Report.acceptedByType.gift -eq 2 -and $r.Report.acceptedByType.follow -eq 2 -and $r.Report.acceptedByType.like -eq 16) 'Mixed event distribution changed.'
        $followIds = @()
        foreach ($request in $r.Requests) {
            Check ($request.StartsWith('GET /api/dashboard/simulate?')) 'CLI used the wrong mutation endpoint or method.'
            $fields = Query-Fields $request
            Check ($fields.count -eq '1' -and $fields.delaySeconds -eq '0' -and $fields.userId.StartsWith('stress-')) 'CLI changed count, scheduling or test identity contract.'
            if ($fields.type -eq 'follow') { $followIds += $fields.userId }
        }
        Check (($followIds | Sort-Object -Unique).Count -eq 2) 'Follows must use distinct viewer identities.'
        Check ($r.Report.countersComparable -and $r.Report.sharedCounterDelta.received -eq 20) 'Shared dashboard counter comparison failed.'
        Check ($r.Report.queueBefore.queueCount -eq 0 -and $r.Report.queueAfter.queueCount -eq 20) 'Queue observations did not wait for the deferred publication after a read-only request.'
    }
    Run-Case 'rate-limit' 'success' 4 10 'Like' {
        param($r)
        Check ($r.ExitCode -eq 0 -and $r.Times.Count -eq 4) 'Rate fixture failed.'
        for ($i = 1; $i -lt $r.Times.Count; $i++) {
            # Loose tolerance avoids treating OS scheduling jitter as a
            # failure; the exact schedule remains covered by code review.
            Check (($r.Times[$i] - $r.Times[$i - 1]) -ge 65) 'CLI produced an unintended catch-up burst.'
        }
    }
    Run-Case '429-not-json' '429' 3 20 'Like' {
        param($r)
        Check ($r.ExitCode -eq 0 -and $r.Report.accepted -eq 2 -and $r.Report.rejected429 -eq 1 -and $r.Requests.Count -eq 3) '429 was retried or misclassified.'
    }
    Run-Case '500-not-json' '500' 3 20 'Like' {
        param($r)
        Check ($r.ExitCode -ne 0 -and !$r.Report.completed -and $r.Report.httpErrors -eq 1 -and $r.Report.uncertain -eq 0 -and $r.Requests.Count -eq 1) 'HTTP failure was retried, lost, or misclassified.'
    }
    Run-Case '202-not-json' 'malformed202' 3 20 'Like' {
        param($r)
        Check ($r.ExitCode -ne 0 -and !$r.Report.completed -and $r.Report.uncertain -eq 1 -and $r.Report.accepted -eq 0 -and $r.Requests.Count -eq 1) 'Malformed acceptance was retried or not marked uncertain.'
    }
    Run-Case 'accepted-response-lost' 'close' 3 20 'Like' {
        param($r)
        Check ($r.ExitCode -ne 0 -and $r.Report.uncertain -eq 1 -and $r.Report.attempted -eq 1) 'Lost response did not produce an uncertain report.'
        Check ($r.Requests.Count -eq 1) 'Mutating GET was retried after the stub accepted it and closed before response headers.'
    }
    Run-Case 'session-change' 'session-change' 1 20 'Follow' {
        param($r)
        Check ($r.ExitCode -eq 0 -and !$r.Report.countersComparable -and $null -eq $r.Report.sharedCounterDelta) 'Dashboard restart produced a misleading counter delta.'
    }
    Run-Case 'random-gifts-seeded' 'success' 32 200 'Gift' {
        param($r)
        Check-RandomGiftSelection $r 20261030 32
        Check ($r.Requests.Count -eq 32 -and $r.Report.acceptedByType.gift -eq 32) 'Random gifts must remain separate accepted events.'
    } -ExtraArguments @('-RandomGifts', '-RandomSeed', '20261030')
    Run-Case 'random-gifts-filtered-mixed' 'success' 100 500 'Mixed' {
        param($r)
        Check-RandomGiftSelection $r 42 10
        Check ($r.Requests.Count -eq 100 -and $r.Report.acceptedByType.gift -eq 10 -and
            $r.Report.acceptedByType.follow -eq 10 -and $r.Report.acceptedByType.like -eq 80) 'Random gift selection changed the mixed event schedule.'
        foreach ($fields in @($r.Requests | ForEach-Object { Query-Fields $_ } | Where-Object { $_.type -ne 'gift' })) {
            Check (!$fields.ContainsKey('giftId')) 'A non-gift event inherited a random gift ID.'
        }
    } -ExtraArguments @('-RandomGifts')
}
finally {
    if (Test-Path -LiteralPath $testRunRoot) {
        Assert-TestPath $testRunRoot
        Remove-Item -LiteralPath $testRunRoot -Recurse -Force
    }
}
Write-Output 'PASS 9 isolated stress CLI groups; ephemeral local HTTP stubs only, no game requests or configuration changes. Ctrl+C still requires an interactive console check.'
