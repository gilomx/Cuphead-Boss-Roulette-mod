[CmdletBinding()]
param(
    [string]$CupheadDir = $env:CUPHEAD_DIR,
    [string]$LoaderPackage,
    [switch]$PrepareOnly,
    [string]$PreparedPackage
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'RuletaDevPackage.ps1')
$repo = Split-Path -Parent $PSScriptRoot

function Resolve-CupheadReferences([string]$ExplicitPath) {
    if ($ExplicitPath) {
        $candidate = [IO.Path]::GetFullPath($ExplicitPath)
        if (-not (Test-Path -LiteralPath (Join-Path $candidate 'Cuphead_Data\Managed\Assembly-CSharp.dll'))) {
            throw 'CupheadDir does not contain the original Cuphead managed assemblies.'
        }
        return $candidate
    }
    $steamPaths = @(
        (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
        (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Steam')
    ) | Where-Object { $_ } | Select-Object -Unique
    $libraries = @($steamPaths)
    foreach ($steam in $steamPaths) {
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $libraries += $match.Groups[1].Value.Replace('\\', '\')
            }
        }
    }
    foreach ($library in ($libraries | Select-Object -Unique)) {
        $candidate = Join-Path $library 'steamapps\common\Cuphead'
        if (Test-Path -LiteralPath (Join-Path $candidate 'Cuphead_Data\Managed\Assembly-CSharp.dll')) { return $candidate }
    }
    throw 'Cuphead references not found. Pass -CupheadDir or set CUPHEAD_DIR locally.'
}

if ($PrepareOnly -and $PreparedPackage) { throw 'Choose PrepareOnly or PreparedPackage.' }
if (-not $PreparedPackage) {
    $references = Resolve-CupheadReferences $CupheadDir
    if (-not $LoaderPackage) { $LoaderPackage = Join-Path $repo 'dist\La-Pichi-Ruleta-0.6.0.zip' }
    if (-not (Test-Path -LiteralPath $LoaderPackage -PathType Leaf)) {
        throw 'Provide -LoaderPackage with a published La Pichi Ruleta ZIP containing BepInEx 5 x64.'
    }
    $work = Join-Path $repo ('installation-backups\launcher-dev-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
    $null = [IO.Directory]::CreateDirectory($work)
    $files = @{}
    $baseZip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($LoaderPackage))
    try {
        # Copy only the known loader; never carry old plugin files or user data forward.
        foreach ($name in (Get-RuletaLoaderFiles)) {
            $entries = @($baseZip.Entries | Where-Object { $_.FullName -ieq $name })
            if ($entries.Count -ne 1) { throw "Missing or duplicate loader dependency: $name" }
            $target = Join-Path $work ('loader\' + $name.Replace('/', '\'))
            $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entries[0], $target)
            $files[$name] = $target
        }
    } finally { $baseZip.Dispose() }
    $stream = [IO.File]::OpenRead($files['winhttp.dll'])
    try { Assert-RuletaX64Executable $stream 'winhttp.dll' } finally { $stream.Dispose() }
    $loaderCore = Join-Path $work 'loader\BepInEx\core'
    $bepVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $loaderCore 'BepInEx.dll')).Version
    if ($bepVersion.Major -ne 5) { throw 'This build requires BepInEx 5 x64.' }

    Push-Location (Join-Path $repo 'creator-tools-ui')
    try {
        if (-not (Test-Path -LiteralPath 'node_modules\.bin\vite.cmd')) {
            & npm.cmd ci
            if ($LASTEXITCODE -ne 0) { throw 'Panel dependency installation failed.' }
        }
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Panel build failed.' }
    } finally { Pop-Location }
    & dotnet build (Join-Path $repo 'CupheadBossRoulette.csproj') -c Release "-p:CupheadDir=$references" "-p:BepInExCoreDir=$loaderCore"
    if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
    & (Join-Path $repo 'TikFinityCompanion\scripts\publish-win-x64.ps1')
    if (-not $?) { throw 'Companion publication failed.' }

    $plugin = 'BepInEx/plugins/GilomxBossRoulette/'
    $files[$plugin + 'Gilomx.CupheadBossRoulette.dll'] = Join-Path $repo 'bin\Release\net35\Gilomx.CupheadBossRoulette.dll'
    $files[$plugin + 'companion/LaPichiRuleta.TikFinity.exe'] = Join-Path $repo 'TikFinityCompanion\artifacts\win-x64\companion\LaPichiRuleta.TikFinity.exe'
    $assetRoot = Join-Path $repo 'assets'
    foreach ($asset in (Get-ChildItem -LiteralPath $assetRoot -Recurse -Force)) {
        if ($asset.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked resources are not supported: $($asset.FullName)" }
        if ($asset.PSIsContainer) { continue }
        $relative = $asset.FullName.Substring($assetRoot.Length + 1).Replace('\', '/')
        $name = $plugin + 'assets/' + $relative
        if (Test-RuletaPackageEntryName $name) { $files[$name] = $asset.FullName }
        elseif ($asset.Extension -notin @('.psd', '.md')) { throw "Unexpected file in distributable assets: $relative" }
    }
    $PreparedPackage = Join-Path $work 'ruleta-dev.zip'
    $hashes = @{}
    $zip = [IO.Compression.ZipFile]::Open($PreparedPackage, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in ($files.Keys | Sort-Object)) {
            $hashes[$name] = (Get-FileHash -LiteralPath $files[$name] -Algorithm SHA256).Hash
            $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $files[$name], $name, [IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $zip.Dispose() }
    $fileCount = Assert-RuletaDevPackage $PreparedPackage $hashes
    $manifest = [ordered]@{ Sha256=(Get-FileHash -LiteralPath $PreparedPackage -Algorithm SHA256).Hash; Files=$hashes }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath ($PreparedPackage + '.manifest.json') -Encoding UTF8
    if ($PrepareOnly) {
        [pscustomobject]@{Status='Prepared and validated';Package=$PreparedPackage;Files=$fileCount;Sha256=$manifest.Sha256} | ConvertTo-Json
        return
    }
}

# A prepared build can be published with only the destination write permission.
$PreparedPackage = [IO.Path]::GetFullPath($PreparedPackage)
$manifest = Get-Content -LiteralPath ($PreparedPackage + '.manifest.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $PreparedPackage -Algorithm SHA256).Hash -ne $manifest.Sha256) { throw 'Prepared ZIP changed after validation.' }
$hashes = @{}
foreach ($property in $manifest.Files.PSObject.Properties) { $hashes[$property.Name] = [string]$property.Value }
$null = Assert-RuletaDevPackage $PreparedPackage $hashes
$destination = Get-RuletaDevPackagePath
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
$temporary = Join-Path ([IO.Path]::GetDirectoryName($destination)) ('current.' + [guid]::NewGuid().ToString('N') + '.tmp.zip')
[IO.File]::Copy($PreparedPackage, $temporary, $false)
if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $manifest.Sha256) { throw 'Delivery copy verification failed; current.zip was not changed.' }
$null = Assert-RuletaDevPackage $temporary $hashes
Publish-RuletaArchiveAtomic $temporary $destination
if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $manifest.Sha256) { throw 'Published ZIP verification failed.' }
$result = [ordered]@{Status='Published for next Dev launch';PublishedAt=(Get-Date -Format o);Destination=$destination;Sha256=$manifest.Sha256;Files=$hashes.Count}
$result | ConvertTo-Json | Set-Content -LiteralPath ($PreparedPackage + '.published.json') -Encoding UTF8
$result | ConvertTo-Json
