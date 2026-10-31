#requires -Version 7.2
<#
.SYNOPSIS
Prueba aislada de recepcion, deduplicacion, reglas y backlog de eventos.
.DESCRIPTION
No envia solicitudes al juego ni a plataformas reales. Usa datos temporales y
un servidor de prueba en un puerto local libre. Rate=0 genera una rafaga; otro
valor limita mensajes normalizados por segundo, incluidos duplicados de prueba.
.EXAMPLE
pwsh -NoProfile -File ./tools/test-stream-stress.ps1 -Events 100000 -Viewers 1000
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 1000000)][int]$Events = 10000,
    [ValidateRange(1, 10000)][int]$Viewers = 100,
    [ValidateRange(0, 1000000)][int]$Rate = 0,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$stressRepoRoot = Split-Path -Parent $PSScriptRoot
if (!$ReportPath) {
    $ReportPath = Join-Path $stressRepoRoot ('.deployment-cache/stress-offline-' + [guid]::NewGuid().ToString('N') + '.json')
}
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
$stressProject = Join-Path $stressRepoRoot 'CreatorToolsRuntimeTests/CreatorToolsRuntimeTests.csproj'
& dotnet run --project $stressProject -c Release -- --stress --events $Events --viewers $Viewers --rate $Rate --report $ReportPath
if ($LASTEXITCODE -ne 0) { throw "La prueba aislada fallo con codigo $LASTEXITCODE." }
Write-Host "Reporte: $ReportPath"
