#requires -Version 7.2
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'LauncherDevPackage.psm1') -Force
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('pichi-deploy-tests-' + [guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($testRoot)
$current = Join-Path $testRoot 'current.zip'
$candidate = Join-Path $testRoot 'candidate.zip'
$stage = Join-Path $testRoot 'stage'
$null = [IO.Directory]::CreateDirectory($stage)
$count = 0
function Expect-Failure([scriptblock]$Action, [string]$Description) {
    $failed = $false
    try { & $Action } catch { $failed = $true }
    if (!$failed) { throw "Expected rejection: $Description" }
    if ((Get-FileHash -LiteralPath $current).Hash -ne $script:previousHash) {
        throw "Previous package changed: $Description"
    }
    $script:count++
    Write-Host "PASS $Description; previous package preserved"
}
function New-Candidate {
    if (Test-Path -LiteralPath $candidate) { [IO.File]::Delete($candidate) }
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $candidate)
}
try {
    [IO.File]::WriteAllText((Join-Path $stage 'winhttp.dll'), 'fixture version 1')
    $manifest = Get-PackageManifest $stage
    New-Candidate
    Publish-DevPackage $candidate $current $manifest
    Test-DevPackage $current $manifest
    $count++
    Write-Host 'PASS first publication'
    [IO.File]::WriteAllText((Join-Path $stage 'winhttp.dll'), 'fixture version 2')
    $manifest = Get-PackageManifest $stage
    New-Candidate
    Publish-DevPackage $candidate $current $manifest
    Test-DevPackage $current $manifest
    $count++
    Write-Host 'PASS atomic replacement'
    $script:previousHash = (Get-FileHash -LiteralPath $current).Hash

    [IO.File]::WriteAllText($candidate, 'interrupted ZIP')
    Expect-Failure { Publish-DevPackage $candidate $current $manifest } 'truncated ZIP'
    New-Candidate
    $wrong = @{ 'winhttp.dll' = ('0' * 64) }
    Expect-Failure { Publish-DevPackage $candidate $current $wrong } 'wrong content hash'
    $missing = $manifest.Clone()
    $missing['doorstop_config.ini'] = 'missing'
    Expect-Failure { Publish-DevPackage $candidate $current $missing } 'missing required file'
    $archive = [IO.Compression.ZipFile]::Open($candidate, [IO.Compression.ZipArchiveMode]::Update)
    try { $null = $archive.CreateEntry('Cuphead.exe') } finally { $archive.Dispose() }
    Expect-Failure { Publish-DevPackage $candidate $current $manifest } 'extra game file'
    foreach ($badName in @('../escape.dll', '/root.dll', 'C:/root.dll', 'assets/../escape', 'assets\file')) {
        New-Candidate
        $archive = [IO.Compression.ZipFile]::Open($candidate, [IO.Compression.ZipArchiveMode]::Update)
        try { $null = $archive.CreateEntry($badName) } finally { $archive.Dispose() }
        Expect-Failure { Publish-DevPackage $candidate $current $manifest } "unsafe path $badName"
    }
    New-Candidate
    $archive = [IO.Compression.ZipFile]::Open($candidate, [IO.Compression.ZipArchiveMode]::Update)
    try { $null = $archive.CreateEntry('WINHTTP.DLL') } finally { $archive.Dispose() }
    Expect-Failure { Publish-DevPackage $candidate $current $manifest } 'duplicate Windows filename'
    New-Candidate
    $reader = [IO.File]::Open($current, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try { Expect-Failure { Publish-DevPackage $candidate $current $manifest } 'destination in use' }
    finally { $reader.Dispose() }
    Expect-Failure { Publish-DevPackage $current $current $manifest } 'source equals destination'
    $elsewhere = Join-Path $stage 'elsewhere.zip'
    [IO.File]::Copy($candidate, $elsewhere)
    Expect-Failure { Publish-DevPackage $elsewhere $current $manifest } 'temporary on another directory'
    Write-Host "All $count deployment checks passed. No game/launcher files were used."
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (!$resolved.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'pichi-deploy-tests-*') { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
