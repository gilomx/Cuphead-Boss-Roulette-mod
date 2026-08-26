$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $projectRoot "tests\LaPichiRuleta.TikFinity.Tests.csproj"

dotnet run --project $testProject --configuration Release
exit $LASTEXITCODE
