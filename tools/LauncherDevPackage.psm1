#requires -Version 7.2
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-PackageEntryName([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name) -or $Name.Contains('\') -or
        $Name -match '[:\x00-\x1f]' -or $Name.StartsWith('/') -or
        @($Name.Split('/') | Where-Object { $_ -in @('', '.', '..') -or $_ -match '[. ]$' }).Count) {
        throw "Unsafe ZIP entry: $Name"
    }
}

function Get-PackageManifest([string]$Directory) {
    $root = [IO.Path]::GetFullPath($Directory)
    $result = @{}
    foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse) {
        $name = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        Assert-PackageEntryName $name
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked file: $name" }
        $result.Add($name, (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)
    }
    if ($result.Count -eq 0) { throw 'Empty package.' }
    return $result
}

function Test-DevPackage([string]$ZipPath, [hashtable]$Manifest) {
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $seen = @{}
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName
            Assert-PackageEntryName $name
            if ($seen.ContainsKey($name) -or !$Manifest.ContainsKey($name)) {
                throw "Unexpected or duplicated ZIP entry: $name"
            }
            $seen.Add($name, $true)
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [Convert]::ToHexString($sha.ComputeHash($stream)) }
            finally { $sha.Dispose(); $stream.Dispose() }
            if ($hash -ne $Manifest[$name]) { throw "ZIP content mismatch: $name" }
        }
        if ($seen.Count -ne $Manifest.Count) { throw 'ZIP is missing required files.' }
    }
    finally { $archive.Dispose() }
}

function Publish-DevPackage([string]$TemporaryZip, [string]$PackagePath, [hashtable]$Manifest) {
    $source = [IO.Path]::GetFullPath($TemporaryZip)
    $target = [IO.Path]::GetFullPath($PackagePath)
    if ($source -eq $target -or
        [IO.Path]::GetDirectoryName($source) -ne [IO.Path]::GetDirectoryName($target)) {
        throw 'Atomic publication requires distinct files in the same directory.'
    }
    Test-DevPackage $source $Manifest
    # No copy/delete fallback: a sharing/filesystem failure must retain current.zip.
    if ([IO.File]::Exists($target)) { [IO.File]::Replace($source, $target, [NullString]::Value) }
    else { [IO.File]::Move($source, $target) }
}

Export-ModuleMember -Function Assert-PackageEntryName, Get-PackageManifest, Test-DevPackage, Publish-DevPackage
