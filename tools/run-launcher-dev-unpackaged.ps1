#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$RequestPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$runDirectory = Split-Path -Parent $RequestPath
$responsePath = Join-Path $runDirectory 'response.json'
$logPath = Join-Path $runDirectory 'build.log'
$response = @{ Success = $false; Error = $null }
try {
    Import-Module (Join-Path $PSScriptRoot 'LauncherDevEnvironment.psm1') -Force
    if (Get-LauncherDeploymentPackageIdentity) { throw 'The independent process still has MSIX package identity.' }
    $request = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
    if ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value -ne $request.UserSid) {
        throw 'Deployment must run as the same Windows user.'
    }
    $repoRoot = Split-Path -Parent $PSScriptRoot
    Set-Location -LiteralPath $repoRoot
    # Scope Git's trusted directory to this child, without changing global settings.
    $env:GIT_CONFIG_COUNT = '1'
    $env:GIT_CONFIG_KEY_0 = 'safe.directory'
    $env:GIT_CONFIG_VALUE_0 = $repoRoot
    $env:PICHI_LAUNCHER_UNPACKAGED_WORKER = '1'
    $parameters = @{}
    if ($request.CupheadDir) { $parameters.CupheadDir = $request.CupheadDir }
    if ($request.BootstrapZip) { $parameters.BootstrapZip = $request.BootstrapZip }
    & (Join-Path $PSScriptRoot 'deploy-launcher-dev.ps1') @parameters *> $logPath
    $response.Success = $true
}
catch { $response.Error = $_.Exception.ToString() }
finally {
    $temporaryResponse = $responsePath + '.tmp'
    $response | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $temporaryResponse -Encoding utf8
    [IO.File]::Move($temporaryResponse, $responsePath)
}
if (!$response.Success) { exit 1 }
