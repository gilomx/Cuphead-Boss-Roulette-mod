[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "LaPichiRuleta.TikFinity.csproj"
$outputPath = Join-Path $projectRoot "artifacts\win-x64\companion"

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $outputPath `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=true `
    -p:TrimMode=partial `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$executable = Join-Path $outputPath "LaPichiRuleta.TikFinity.exe"
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Publish did not create $executable"
}

$unexpectedFiles = @(Get-ChildItem -LiteralPath $outputPath -File |
    Where-Object { $_.Name -ne "LaPichiRuleta.TikFinity.exe" })
if ($unexpectedFiles.Count -gt 0) {
    throw "Single-file publish created unexpected sidecar files: $($unexpectedFiles.Name -join ', ')"
}

$publishedFile = Get-Item -LiteralPath $executable
[pscustomobject]@{
    FullName = $publishedFile.FullName
    Length = $publishedFile.Length
    Sha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
} | ConvertTo-Json -Compress
