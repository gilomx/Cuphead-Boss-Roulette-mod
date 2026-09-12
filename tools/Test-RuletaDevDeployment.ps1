$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'RuletaDevPackage.ps1')
$testRoot = Join-Path (Split-Path -Parent $PSScriptRoot) ('installation-backups\deployment-tests-' + [guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($testRoot)
$script:passed = 0

function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Reject([scriptblock]$Action, [string]$MessagePattern) {
    try { & $Action | Out-Null }
    catch { if ($_.Exception.Message -notmatch $MessagePattern) { throw }; return }
    throw "Expected rejection matching: $MessagePattern"
}
function Pass([string]$Name) { $script:passed++; Write-Output "PASS $Name" }
function BytesHash([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-', '') } finally { $sha.Dispose() }
}
function FixtureFiles {
    $plugin = 'BepInEx/plugins/GilomxBossRoulette/'
    $names = @((Get-RuletaLoaderFiles)) + @(
        $plugin + 'Gilomx.CupheadBossRoulette.dll'
        $plugin + 'companion/LaPichiRuleta.TikFinity.exe'
        $plugin + 'assets/shaders/gilomx-boss-roulette-shaders'
        $plugin + 'assets/creator-tools/config.html'
        $plugin + 'assets/creator-tools/config.js'
        $plugin + 'assets/creator-tools/config.css'
        $plugin + 'assets/creator-tools/overlay.html'
        $plugin + 'assets/creator-tools/gifts/catalog.json'
    )
    $files = @{}
    foreach ($name in $names) { $files[$name] = [Text.Encoding]::UTF8.GetBytes('fixture: ' + $name) }
    $pe = [byte[]]::new(128)
    [BitConverter]::GetBytes([uint16]0x5a4d).CopyTo($pe, 0)
    [BitConverter]::GetBytes([int]64).CopyTo($pe, 60)
    [BitConverter]::GetBytes([uint32]0x4550).CopyTo($pe, 64)
    [BitConverter]::GetBytes([uint16]0x8664).CopyTo($pe, 68)
    $files['winhttp.dll'] = $pe
    $files[$plugin + 'companion/LaPichiRuleta.TikFinity.exe'] = $pe.Clone()
    $files['doorstop_config.ini'] = [Text.Encoding]::UTF8.GetBytes("[UnityDoorstop]`nenabled=true`ntargetAssembly=BepInEx\core\BepInEx.Preloader.dll`ndllSearchPathOverride=`n")
    return $files
}
function WriteFixture([hashtable]$Files, [string]$Duplicate = '') {
    $path = Join-Path $testRoot ([guid]::NewGuid().ToString('N') + '.zip')
    $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    $hashes = @{}
    try {
        foreach ($name in $Files.Keys) {
            $entry = $zip.CreateEntry($name)
            $stream = $entry.Open()
            try { $stream.Write($Files[$name], 0, $Files[$name].Length) } finally { $stream.Dispose() }
            $hashes[$name] = BytesHash $Files[$name]
        }
        if ($Duplicate) { $null = $zip.CreateEntry($Duplicate) }
    } finally { $zip.Dispose() }
    [pscustomobject]@{Path=$path;Hashes=$hashes}
}

$expectedPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'CupheadModLauncher\dev\pichi-ruleta\current.zip'
Check ((Get-RuletaDevPackagePath) -eq $expectedPath) 'Wrong delivery path'
Pass 'per-user delivery path'
$valid = WriteFixture (FixtureFiles)
Check ((Assert-RuletaDevPackage $valid.Path $valid.Hashes) -eq $valid.Hashes.Count) 'Valid ZIP failed'
Pass 'complete package and byte hashes'

$files = FixtureFiles
$files.Remove('BepInEx/plugins/GilomxBossRoulette/companion/LaPichiRuleta.TikFinity.exe')
$missing = WriteFixture $files
Reject { Assert-RuletaDevPackage $missing.Path $missing.Hashes } 'Missing package dependency'
Pass 'missing companion rejected even when absent from manifest'

$files = FixtureFiles
[BitConverter]::GetBytes([uint16]0x14c).CopyTo($files['winhttp.dll'], 68)
$x86 = WriteFixture $files
Reject { Assert-RuletaDevPackage $x86.Path $x86.Hashes } 'Expected an x64'
Pass 'incompatible loader architecture'

foreach ($extra in @('Cuphead.exe', 'UnityPlayer.dll', 'steam_api64.dll',
    'BepInEx/config/personal.json', 'BepInEx/plugins/OtherMod.dll',
    'BepInEx/plugins/GilomxBossRoulette/assets/../personal.json',
    'BepInEx/plugins/GilomxBossRoulette/assets/cache/image.png',
    'BepInEx/plugins/GilomxBossRoulette/assets/creator-tools/catalog.json')) {
    $files = FixtureFiles
    $files[$extra] = [Text.Encoding]::UTF8.GetBytes('must not ship')
    $bad = WriteFixture $files
    Reject { Assert-RuletaDevPackage $bad.Path $bad.Hashes } 'Disallowed package entry'
}
Pass 'game files, personal data, other mods and unsafe paths rejected'

$duplicate = WriteFixture (FixtureFiles) 'winhttp.dll'
Reject { Assert-RuletaDevPackage $duplicate.Path $duplicate.Hashes } 'Duplicate package entry'
Pass 'duplicate ZIP names'
$valid.Hashes['winhttp.dll'] = 'wrong hash'
Reject { Assert-RuletaDevPackage $valid.Path $valid.Hashes } 'does not match the build'
Pass 'changed package content'

$files = FixtureFiles
$files['doorstop_config.ini'] = [Text.Encoding]::UTF8.GetBytes("enabled=true`ntargetAssembly=Z:\old-game\BepInEx.Preloader.dll`ndllSearchPathOverride=`n")
$bad = WriteFixture $files
Reject { Assert-RuletaDevPackage $bad.Path $bad.Hashes } 'relative, bundled loader paths'
Pass 'absolute loader paths rejected'

$destination = Join-Path $testRoot 'current.zip'
$temporary = Join-Path $testRoot 'pending.zip'
[IO.File]::WriteAllText($temporary, 'first complete ZIP')
Publish-RuletaArchiveAtomic $temporary $destination
Check ([IO.File]::ReadAllText($destination) -eq 'first complete ZIP') 'Initial publication failed'
[IO.File]::WriteAllText($temporary, 'second complete ZIP')
$oldReader = [IO.File]::Open($destination, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::Read -bor [IO.FileShare]::Delete))
try {
    Publish-RuletaArchiveAtomic $temporary $destination
    $reader = [IO.StreamReader]::new($oldReader)
    try { Check ($reader.ReadToEnd() -eq 'first complete ZIP') 'Existing reader saw a partial update' } finally { $reader.Dispose() }
    Check ([IO.File]::ReadAllText($destination) -eq 'second complete ZIP') 'Replacement failed'
} finally { $oldReader.Dispose() }
Pass 'atomic initial publish and coherent reader snapshot during replacement'

[IO.File]::WriteAllText($temporary, 'third complete ZIP')
$locked = [IO.File]::Open($destination, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try { Reject { Publish-RuletaArchiveAtomic $temporary $destination 3 10 } 'failed after 3 attempts' }
finally { $locked.Dispose() }
Check ([IO.File]::ReadAllText($destination) -eq 'second complete ZIP') 'Locked destination was damaged'
Check ([IO.File]::ReadAllText($temporary) -eq 'third complete ZIP') 'Failed publication lost the prepared ZIP'
Pass 'bounded retries preserve the previous and pending packages when locked'
Reject { Publish-RuletaArchiveAtomic $destination $destination } 'distinct temporary ZIP'
Reject { Publish-RuletaArchiveAtomic $temporary (Join-Path $testRoot 'other\current.zip') } 'beside the destination'
Pass 'reject direct edits and non-atomic cross-directory publication'
Write-Output "$script:passed deployment checks passed. Fixtures: $testRoot"
