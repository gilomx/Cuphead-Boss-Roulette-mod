#requires -Version 7.2
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (!('LauncherDevEnvironment.NativePaths' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace LauncherDevEnvironment {
    public static class NativePaths {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetCurrentPackageFullName(ref uint length, StringBuilder name);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
    }
}
'@
}

function Get-LauncherDeploymentPackageIdentity {
    [uint32]$length = 0
    $result = [LauncherDevEnvironment.NativePaths]::GetCurrentPackageFullName([ref]$length, $null)
    if ($result -eq 15700) { return $null } # APPMODEL_ERROR_NO_PACKAGE
    if ($result -ne 122) { throw "Cannot determine Windows package identity: $result" }
    $name = [Text.StringBuilder]::new([int]$length)
    $result = [LauncherDevEnvironment.NativePaths]::GetCurrentPackageFullName([ref]$length, $name)
    if ($result -ne 0) { throw "Cannot read Windows package identity: $result" }
    return $name.ToString()
}

function Assert-LauncherPhysicalPath([IO.FileStream]$Stream, [string]$ExpectedPath) {
    $buffer = [Text.StringBuilder]::new(32768)
    $length = [LauncherDevEnvironment.NativePaths]::GetFinalPathNameByHandle(
        $Stream.SafeFileHandle, $buffer, $buffer.Capacity, 0)
    if ($length -eq 0 -or $length -ge $buffer.Capacity) { throw 'Cannot verify physical deployment path.' }
    $physical = $buffer.ToString()
    if ($physical.StartsWith('\\?\UNC\')) { $physical = '\\' + $physical.Substring(8) }
    elseif ($physical.StartsWith('\\?\')) { $physical = $physical.Substring(4) }
    if (![IO.Path]::GetFullPath($ExpectedPath).Equals($physical, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Windows redirected deployment to '$physical'. The real launcher would not see it."
    }
    return $physical
}

function Invoke-LauncherDeploymentOutsidePackage([string]$RepoRoot, [string]$CupheadDir, [string]$BootstrapZip) {
    $runDirectory = Join-Path $RepoRoot ('.deployment-cache/unpackaged-' + [guid]::NewGuid().ToString('N'))
    $null = [IO.Directory]::CreateDirectory($runDirectory)
    $requestPath = Join-Path $runDirectory 'request.json'
    $responsePath = Join-Path $runDirectory 'response.json'
    $logPath = Join-Path $runDirectory 'build.log'
    $request = @{
        UserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        CupheadDir = $CupheadDir
        BootstrapZip = $BootstrapZip
    }
    $request | ConvertTo-Json | Set-Content -LiteralPath $requestPath -Encoding utf8
    $shell = Join-Path $PSHOME 'pwsh.exe'
    $runner = Join-Path $PSScriptRoot 'run-launcher-dev-unpackaged.ps1'
    # Direct process arguments, never cmd.exe/Invoke-Expression or UI input.
    $commandLine = '"' + $shell + '" -NoLogo -NoProfile -NonInteractive -File "' + $runner + '" -RequestPath "' + $requestPath + '"'
    $startup = New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ ShowWindow = [uint16]0 }
    $launch = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
        CommandLine = $commandLine; CurrentDirectory = $RepoRoot; ProcessStartupInformation = $startup
    }
    if ($launch.ReturnValue -ne 0) { throw "Cannot start deployment outside MSIX: $($launch.ReturnValue). Run the canonical command from an ordinary PowerShell window." }
    Write-Host 'Windows package context detected; building in a hidden, independent Windows process.'
    Write-Host "Deployment log: $logPath"
    while (!(Test-Path -LiteralPath $responsePath)) {
        Start-Sleep -Milliseconds 500
        if (!(Get-Process -Id $launch.ProcessId -ErrorAction SilentlyContinue) -and !(Test-Path -LiteralPath $responsePath)) {
            if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath | Write-Host }
            throw 'Independent deployment ended without a completion receipt.'
        }
    }
    $response = Get-Content -LiteralPath $responsePath -Raw | ConvertFrom-Json
    if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath | Write-Host }
    if (!$response.Success) { throw "Independent deployment failed: $($response.Error)" }
}

Export-ModuleMember -Function Get-LauncherDeploymentPackageIdentity, Assert-LauncherPhysicalPath, Invoke-LauncherDeploymentOutsidePackage
