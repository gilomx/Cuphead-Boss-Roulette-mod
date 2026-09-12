# Shared by the publisher and its filesystem/ZIP contract tests. No game startup.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-RuletaDevPackagePath {
    $localData = [Environment]::GetFolderPath('LocalApplicationData')
    if ([string]::IsNullOrWhiteSpace($localData)) { throw 'Windows did not provide LocalApplicationData.' }
    Join-Path $localData 'CupheadModLauncher\dev\pichi-ruleta\current.zip'
}

function Get-RuletaLoaderFiles {
    @('winhttp.dll', 'doorstop_config.ini',
      'BepInEx/core/0Harmony.dll', 'BepInEx/core/0Harmony20.dll',
      'BepInEx/core/BepInEx.dll', 'BepInEx/core/BepInEx.Harmony.dll',
      'BepInEx/core/BepInEx.Preloader.dll', 'BepInEx/core/HarmonyXInterop.dll',
      'BepInEx/core/Mono.Cecil.dll', 'BepInEx/core/Mono.Cecil.Mdb.dll',
      'BepInEx/core/Mono.Cecil.Pdb.dll', 'BepInEx/core/Mono.Cecil.Rocks.dll',
      'BepInEx/core/MonoMod.RuntimeDetour.dll', 'BepInEx/core/MonoMod.Utils.dll')
}

function Test-RuletaPackageEntryName([string]$Name) {
    if ($Name -match '(^/|\\|:|(^|/)\.{1,2}(/|$)|//)' -or $Name.EndsWith('/')) { return $false }
    if ($Name -cin (Get-RuletaLoaderFiles)) { return $true }
    $plugin = 'BepInEx/plugins/GilomxBossRoulette/'
    if ($Name -ceq ($plugin + 'Gilomx.CupheadBossRoulette.dll') -or
        $Name -ceq ($plugin + 'companion/LaPichiRuleta.TikFinity.exe')) { return $true }
    # Only distributable resources, never a recursive copy of an installed mod.
    if (-not $Name.StartsWith($plugin + 'assets/', [StringComparison]::Ordinal)) { return $false }
    if ($Name -match '(^|/)(config|cache|logs?|backups?|node_modules|\.git)(/|$)') { return $false }
    if ($Name -ceq ($plugin + 'assets/shaders/gilomx-boss-roulette-shaders')) { return $true }
    if ($Name.EndsWith('.json')) { return $Name -ceq ($plugin + 'assets/creator-tools/gifts/catalog.json') }
    return $Name -match '\.(png|webp|gif|wav|mp3|ogg|html|css|js|tsv)$'
}

function Assert-RuletaX64Executable([IO.Stream]$Stream, [string]$Name) {
    $copy = [IO.MemoryStream]::new()
    try {
        $Stream.CopyTo($copy)
        $bytes = $copy.ToArray()
        if ($bytes.Length -lt 64 -or [BitConverter]::ToUInt16($bytes, 0) -ne 0x5a4d) { throw "Invalid PE executable: $Name" }
        $pe = [BitConverter]::ToInt32($bytes, 60)
        if ($pe -lt 64 -or $pe -gt ($bytes.Length - 6) -or
            [BitConverter]::ToUInt32($bytes, $pe) -ne 0x4550 -or
            [BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664) { throw "Expected an x64 executable: $Name" }
    } finally { $copy.Dispose() }
}

function Assert-RuletaDevPackage([string]$Path, [System.Collections.IDictionary]$ExpectedFiles) {
    $plugin = 'BepInEx/plugins/GilomxBossRoulette/'
    $required = @((Get-RuletaLoaderFiles)) + @(
        $plugin + 'Gilomx.CupheadBossRoulette.dll'
        $plugin + 'companion/LaPichiRuleta.TikFinity.exe'
        $plugin + 'assets/shaders/gilomx-boss-roulette-shaders'
        $plugin + 'assets/creator-tools/config.html'
        $plugin + 'assets/creator-tools/config.js'
        $plugin + 'assets/creator-tools/config.css'
        $plugin + 'assets/creator-tools/overlay.html'
        $plugin + 'assets/creator-tools/gifts/catalog.json'
    )
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    try {
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if (-not (Test-RuletaPackageEntryName $name)) { throw "Disallowed package entry: $name" }
            if (-not $names.Add($name)) { throw "Duplicate package entry: $name" }
            if ($entry.Length -eq 0) { throw "Empty package entry: $name" }
            if (-not $ExpectedFiles.Contains($name)) { throw "Unexpected package entry: $name" }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
            finally { $sha.Dispose(); $stream.Dispose() }
            if ($hash -ne $ExpectedFiles[$name]) { throw "Package content does not match the build: $name" }
            if ($name -eq 'winhttp.dll' -or $name.EndsWith('/LaPichiRuleta.TikFinity.exe')) {
                $stream = $entry.Open()
                try { Assert-RuletaX64Executable $stream $name } finally { $stream.Dispose() }
            }
        }
        foreach ($name in @($required) + @($ExpectedFiles.Keys)) {
            if (-not $names.Contains($name)) { throw "Missing package dependency: $name" }
        }
        $reader = [IO.StreamReader]::new($zip.GetEntry('doorstop_config.ini').Open())
        try { $doorstop = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($doorstop -notmatch '(?im)^enabled\s*=\s*true\s*$' -or
            $doorstop -notmatch '(?im)^targetAssembly\s*=\s*BepInEx[\\/]core[\\/]BepInEx\.Preloader\.dll\s*$' -or
            $doorstop -notmatch '(?im)^dllSearchPathOverride\s*=\s*$') {
            throw 'Doorstop must be enabled and use relative, bundled loader paths.'
        }
    } finally { $zip.Dispose() }
    $names.Count
}

function Publish-RuletaArchiveAtomic([string]$TemporaryPath, [string]$DestinationPath,
    [int]$MaximumAttempts = 5, [int]$RetryDelayMilliseconds = 250) {
    $temporary = [IO.Path]::GetFullPath($TemporaryPath)
    $destination = [IO.Path]::GetFullPath($DestinationPath)
    if ($temporary -eq $destination -or
        [IO.Path]::GetDirectoryName($temporary) -ne [IO.Path]::GetDirectoryName($destination)) {
        throw 'Atomic publication requires a distinct temporary ZIP beside the destination.'
    }
    for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
        try {
            # Windows PowerShell 5 otherwise coerces $null to an empty backup path.
            if ([IO.File]::Exists($destination)) { [IO.File]::Replace($temporary, $destination, [NullString]::Value) }
            else { [IO.File]::Move($temporary, $destination) }
            return
        } catch [IO.IOException] {
            if ($attempt -eq $MaximumAttempts) {
                throw "Publication failed after $MaximumAttempts attempts; previous package preserved. Temporary ZIP: $temporary. $($_.Exception.Message)"
            }
            Start-Sleep -Milliseconds $RetryDelayMilliseconds
        }
    }
}
