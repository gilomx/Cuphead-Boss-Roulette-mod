#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CupheadDir,
    [string]$CecilPath = (Join-Path $env:USERPROFILE '.nuget/packages/mono.cecil/0.10.4/lib/net40/Mono.Cecil.dll'),
    [string]$ModPath
)

# Read-only checks of the native API and optional compiled cover-probe wiring.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
function Require($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Calls($Method, [string]$Name) {
    @($Method.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $Name
    })
}
$core = [Mono.Cecil.ModuleDefinition]::ReadModule(
    (Join-Path $CupheadDir 'Cuphead_Data/Managed/UnityEngine.CoreModule.dll'))
try {
    $objectType = $core.Types | Where-Object FullName -eq 'UnityEngine.Object'
    $find = @($objectType.Methods | Where-Object {
        $_.Name -eq 'FindObjectsOfType' -and $_.IsPublic -and $_.IsStatic -and
        !$_.HasGenericParameters -and $_.Parameters.Count -eq 1 -and
        $_.Parameters[0].ParameterType.FullName -eq 'System.Type' -and
        $_.ReturnType.FullName -eq 'UnityEngine.Object[]'
    })
    Require ($find.Count -eq 1) 'Native untyped active-object discovery API changed.'
    $component = $core.Types | Where-Object FullName -eq 'UnityEngine.Component'
    $children = @($component.Methods | Where-Object {
        $_.Name -eq 'GetComponentsInChildren' -and $_.IsPublic -and !$_.IsStatic -and
        $_.GenericParameters.Count -eq 1 -and $_.Parameters.Count -eq 2 -and
        $_.Parameters[0].ParameterType.FullName -eq 'System.Boolean' -and
        $_.Parameters[1].ParameterType -is [Mono.Cecil.GenericInstanceType] -and
        $_.Parameters[1].ParameterType.ElementType.FullName -eq 'System.Collections.Generic.List`1' -and
        $_.ReturnType.FullName -eq 'System.Void'
    })
    Require ($children.Count -eq 1) 'Native reusable child-list discovery API changed.'
    $scene = $core.Types | Where-Object FullName -eq 'UnityEngine.SceneManagement.Scene'
    Require (@($scene.Methods | Where-Object {
        $_.Name -eq 'op_Equality' -and $_.IsPublic -and $_.IsStatic -and
        $_.Parameters.Count -eq 2 -and $_.ReturnType.FullName -eq 'System.Boolean'
    }).Count -eq 1) 'Native public scene equality API changed.'
    Write-Output 'PASS Native screen-cover APIs: untyped object discovery, reusable child-list overload and scene equality.'
}
finally { $core.Dispose() }

if ($ModPath) {
    $module = [Mono.Cecil.ModuleDefinition]::ReadModule($ModPath)
    try {
        $probe = $module.Types | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.CreatorToolsScreenCoverProbe'
        $priority = $module.Types | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.CreatorToolsInteractionRenderPriority'
        Require ($null -ne $probe -and $null -ne $priority) 'Compiled screen-cover helper is missing.'
        $query = $probe.Methods | Where-Object Name -eq 'IsActive'
        $wrapper = $priority.Methods | Where-Object Name -eq 'IsScreenCoverActive'
        $delegation = @(Calls $wrapper 'IsActive')
        Require ($delegation.Count -eq 1 -and $delegation[0].Operand.DeclaringType.FullName -eq $probe.FullName) 'Render priority bypasses the shared screen-cover probe.'
        $find = @(Calls $query 'FindObjectsOfType')
        Require ($find.Count -eq 1 -and
            $find[0].Operand -isnot [Mono.Cecil.GenericInstanceMethod] -and
            $find[0].Operand.Parameters.Count -eq 1 -and
            $find[0].Operand.Parameters[0].ParameterType.FullName -eq 'System.Type') 'Screen-cover discovery recreates Unity typed-array copies.'
        $children = @(Calls $query 'GetComponentsInChildren')
        Require ($children.Count -eq 1 -and $children[0].Operand.Parameters.Count -eq 2 -and
            $children[0].Operand.ReturnType.FullName -eq 'System.Void' -and
            $children[0].Operand.GenericArguments[0].FullName -eq 'UnityEngine.SpriteRenderer') 'Screen-cover children no longer use the reusable sprite list.'
        Require (@(Calls $query 'GetActiveScene').Count -eq 1 -and
            @(Calls $query 'op_Equality' | Where-Object {
                $_.Operand.DeclaringType.FullName -eq 'UnityEngine.SceneManagement.Scene'
            }).Count -eq 1 -and @(Calls $query 'get_frameCount').Count -ge 1) 'Screen-cover cache lost frame/scene invalidation.'
        Require (@($query.Body.Instructions | Where-Object {
            $_.OpCode.Code -in @('Newarr', 'Newobj', 'Box')
        }).Count -eq 0) 'Screen-cover queries now allocate managed objects directly.'
        $finally = @($query.Body.ExceptionHandlers | Where-Object HandlerType -eq 'Finally')
        Require ($finally.Count -eq 1 -and @(Calls $query 'Clear' | Where-Object {
            $_.Offset -ge $finally[0].HandlerStart.Offset -and
            $_.Offset -lt $finally[0].HandlerEnd.Offset
        }).Count -eq 1) 'Screen-cover scratch references are not cleared on every exit.'
        Write-Output 'PASS Compiled screen-cover wiring: shared frame/scene discovery, no typed-array copies, reusable child buffer and finally cleanup.'
    }
    finally { $module.Dispose() }
}
