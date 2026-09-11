#requires -Version 7.2
[CmdletBinding()]
param(
    [string]$CupheadDir,
    [string]$BootstrapZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'LauncherDevPackage.psm1') -Force
$repoRoot = Split-Path -Parent $PSScriptRoot
$cacheRoot = Join-Path $repoRoot '.deployment-cache'
$null = [IO.Directory]::CreateDirectory($cacheRoot)

function Invoke-Checked([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Program failed with exit code $LASTEXITCODE" }
}

function Resolve-LocalPath([string]$Path) {
    $expanded = [Environment]::ExpandEnvironmentVariables($Path)
    return [IO.Path]::GetFullPath($expanded, $repoRoot)
}

function Find-Cuphead {
    $steamRoots = @(
        foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
            if (Test-Path $key) {
                $values = Get-ItemProperty $key
                foreach ($name in @('SteamPath', 'InstallPath')) {
                    if ($values.PSObject.Properties[$name] -and $values.$name) { $values.$name }
                }
            }
        }
    )
    $libraries = @($steamRoots)
    foreach ($steam in $steamRoots) {
        $vdf = Join-Path $steam 'steamapps/libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches([IO.File]::ReadAllText($vdf), '"path"\s+"([^"]+)"')) {
                $libraries += $match.Groups[1].Value.Replace('\\', '\')
            }
        }
    }
    $candidates = @(
        foreach ($library in ($libraries | Sort-Object -Unique)) {
            $manifest = Join-Path $library 'steamapps/appmanifest_268910.acf'
            if (!(Test-Path -LiteralPath $manifest)) { continue }
            $match = [regex]::Match([IO.File]::ReadAllText($manifest), '"installdir"\s+"([^"]+)"')
            if (!$match.Success) { continue }
            $candidate = Join-Path (Join-Path $library 'steamapps/common') $match.Groups[1].Value
            if (Test-Path -LiteralPath (Join-Path $candidate 'Cuphead_Data/Managed/Assembly-CSharp.dll')) {
                [IO.Path]::GetFullPath($candidate)
            }
        }
    ) | Sort-Object -Unique
    if (@($candidates).Count -ne 1) {
        throw 'Cannot identify a unique Cuphead install. Set cupheadDir in launcher-dev.local.json or pass -CupheadDir.'
    }
    return $candidates
}

$localConfig = Join-Path $repoRoot 'launcher-dev.local.json'
if (Test-Path -LiteralPath $localConfig) {
    $local = Get-Content -LiteralPath $localConfig -Raw | ConvertFrom-Json -AsHashtable
    foreach ($key in $local.Keys) {
        if ($key -notin @('cupheadDir', 'bootstrapZip')) { throw "Unknown local configuration key: $key" }
    }
    if (!$CupheadDir) { $CupheadDir = $local['cupheadDir'] }
    if (!$BootstrapZip) { $BootstrapZip = $local['bootstrapZip'] }
}
if (!$CupheadDir) { $CupheadDir = Find-Cuphead }
else { $CupheadDir = Resolve-LocalPath $CupheadDir }
if (!(Test-Path -LiteralPath (Join-Path $CupheadDir 'Cuphead_Data/Managed/Assembly-CSharp.dll'))) {
    throw 'Cuphead compilation references are missing.'
}
foreach ($program in @('git', 'dotnet', 'npm.cmd')) { $null = Get-Command $program -ErrorAction Stop }
$bootstrap = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'launcher-dev-bootstrap.json') -Raw | ConvertFrom-Json

$packagePath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'CupheadModLauncher\dev\pichi-ruleta\current.zip'
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($packagePath))
$packageDirectory = [IO.Path]::GetDirectoryName($packagePath)
$runId = [guid]::NewGuid().ToString('N')
$work = Join-Path $cacheRoot "build-$runId"
$stage = Join-Path $work 'package'
$uiOutput = Join-Path $work 'ui'
$temporaryZip = Join-Path $packageDirectory ".current-$runId.tmp.zip"
$download = Join-Path $cacheRoot "download-$runId.tmp.zip"
$lock = $null
try {
    # Serialize builds/publications without interacting with Cuphead or the launcher.
    $lock = [IO.File]::Open((Join-Path $packageDirectory '.publish.lock'),
        [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $null = [IO.Directory]::CreateDirectory($stage)
    if ($BootstrapZip) { $BootstrapZip = Resolve-LocalPath $BootstrapZip }
    else {
        $BootstrapZip = Join-Path $cacheRoot ($bootstrap.sha256 + '.zip')
        if (![IO.File]::Exists($BootstrapZip)) {
            Invoke-WebRequest -Uri $bootstrap.url -OutFile $download
            if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $bootstrap.sha256) {
                throw 'Downloaded reference package has the wrong SHA-256.'
            }
            [IO.File]::Move($download, $BootstrapZip)
        }
    }
    if ((Get-FileHash -LiteralPath $BootstrapZip -Algorithm SHA256).Hash -ne $bootstrap.sha256) {
        throw 'Reference package does not match the pinned published release.'
    }
    # Extract only the pinned loader allowlist, never old plugins or personal config.
    $archive = [IO.Compression.ZipFile]::OpenRead($BootstrapZip)
    try {
        foreach ($name in $bootstrap.files) {
            Assert-PackageEntryName $name
            $entries = @($archive.Entries | Where-Object { $_.FullName.Replace('\', '/') -ceq $name })
            if ($entries.Count -ne 1 -or $entries[0].Length -eq 0) { throw "Missing loader file: $name" }
            $target = Join-Path $stage $name
            $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entries[0], $target)
        }
    }
    finally { $archive.Dispose() }
    $proxy = [IO.File]::ReadAllBytes((Join-Path $stage 'winhttp.dll'))
    $pe = [BitConverter]::ToInt32($proxy, 0x3c)
    if ([BitConverter]::ToUInt16($proxy, $pe + 4) -ne 0x8664) { throw 'Loader is not Windows x64.' }
    $doorstop = [IO.File]::ReadAllText((Join-Path $stage 'doorstop_config.ini'))
    if ($doorstop -notmatch '(?m)^enabled=true\s*$' -or
        $doorstop -notmatch '(?m)^targetAssembly=BepInEx\\core\\BepInEx.Preloader.dll\s*$') {
        throw 'Doorstop must use the relative, enabled BepInEx preloader.'
    }

    Push-Location (Join-Path $repoRoot 'creator-tools-ui')
    try {
        Invoke-Checked 'npm.cmd' @('ci', '--no-audit', '--no-fund')
        Invoke-Checked 'npm.cmd' @('run', 'build', '--', '--outDir', $uiOutput)
    }
    finally { Pop-Location }
    $modOutput = Join-Path $work 'mod'
    Invoke-Checked 'dotnet' @('build', (Join-Path $repoRoot 'CupheadBossRoulette.csproj'),
        '-c', 'Release', '-o', $modOutput, "-p:CupheadDir=$CupheadDir",
        "-p:BepInExCoreDir=$(Join-Path $stage 'BepInEx/core')")
    $pluginRoot = Join-Path $stage 'BepInEx/plugins/GilomxBossRoulette'
    $companion = Join-Path $pluginRoot 'companion'
    $null = [IO.Directory]::CreateDirectory($companion)
    Copy-Item -LiteralPath (Join-Path $modOutput 'Gilomx.CupheadBossRoulette.dll') -Destination $pluginRoot
    Invoke-Checked 'dotnet' @('publish', (Join-Path $repoRoot 'TikFinityCompanion/LaPichiRuleta.TikFinity.csproj'),
        '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $companion,
        '-p:PublishSingleFile=true', '-p:PublishTrimmed=true', '-p:TrimMode=partial',
        '-p:DebugType=None', '-p:DebugSymbols=false')
    $companionFiles = @(Get-ChildItem -LiteralPath $companion -File -Recurse)
    if ($companionFiles.Count -ne 1 -or $companionFiles[0].Name -ne 'LaPichiRuleta.TikFinity.exe') {
        throw 'Companion publish must produce exactly one self-contained EXE.'
    }

    # Git's tracked assets form the resource allowlist; unrelated local files stay out.
    $assets = @(& git -C $repoRoot -c core.quotepath=false ls-files -- assets)
    if ($LASTEXITCODE -ne 0 -or $assets.Count -eq 0) { throw 'Cannot enumerate tracked assets.' }
    foreach ($asset in $assets) {
        Assert-PackageEntryName $asset
        if ($asset -in @('assets/creator-tools/config.html', 'assets/creator-tools/config.css', 'assets/creator-tools/config.js')) { continue }
        if ($asset -match '(?i)(^|/)(cache|backups?|logs?)/|\.(log|bak|tmp|zip)$') { throw "Non-distributable asset: $asset" }
        $source = Join-Path $repoRoot $asset
        if ((Get-Item -LiteralPath $source).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked asset: $asset" }
        $target = Join-Path $pluginRoot $asset
        $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        Copy-Item -LiteralPath $source -Destination $target
    }
    foreach ($file in Get-ChildItem -LiteralPath $uiOutput -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($uiOutput, $file.FullName).Replace('\', '/')
        Assert-PackageEntryName $relative
        $target = Join-Path $pluginRoot "assets/creator-tools/$relative"
        $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
    foreach ($name in @('config.html', 'config.css', 'config.js')) {
        if (!(Test-Path -LiteralPath (Join-Path $uiOutput $name))) { throw "UI output is missing $name" }
    }

    $manifest = Get-PackageManifest $stage
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $temporaryZip, [IO.Compression.CompressionLevel]::Optimal, $false)
    $hash = (Get-FileHash -LiteralPath $temporaryZip -Algorithm SHA256).Hash
    Publish-DevPackage $temporaryZip $packagePath $manifest
    [pscustomobject]@{ Package = $packagePath; Files = $manifest.Count; Sha256 = $hash;
        LauncherIntegration = 'Pending; package prepared for the next supported launch.' } | ConvertTo-Json
}
finally {
    # Delete only this run's temporary paths, after checking their absolute containment.
    foreach ($temporary in @($work, $download, $temporaryZip)) {
        $full = [IO.Path]::GetFullPath($temporary)
        $parent = if ($temporary -eq $temporaryZip) { $packageDirectory } else { $cacheRoot }
        $prefix = [IO.Path]::GetFullPath($parent) + [IO.Path]::DirectorySeparatorChar
        if (!$full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe cleanup path.' }
        if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
    }
    if ($null -ne $lock) { $lock.Dispose() }
}
