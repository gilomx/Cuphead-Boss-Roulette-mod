#requires -Version 7.0
param(
    [string]$CupheadDir = 'C:\Program Files (x86)\Steam\steamapps\common\Cuphead',
    [string]$CecilPath = "$env:USERPROFILE\.nuget\packages\mono.cecil\0.10.4\lib\net40\Mono.Cecil.dll"
)

# Read native IL only. Verify the safe insertion point without launching Unity
# or claiming that an IL check measures real frame times.
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$module = [Mono.Cecil.ModuleDefinition]::ReadModule(
    (Join-Path $CupheadDir 'Cuphead_Data\Managed\Assembly-CSharp.dll'))
function Require($condition, [string]$message) {
    if (-not $condition) { throw $message }
}
function Calls($method, [string]$typeName, [string]$methodName) {
    @($method.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.FullName -eq $typeName -and
        $_.Operand.Name -eq $methodName
    })
}
try {
    $loader = $module.Types | Where-Object Name -eq 'SceneLoader'
    $factory = $loader.Methods | Where-Object Name -eq 'load_cr'
    Require ($factory.ReturnType.FullName -eq 'System.Collections.IEnumerator') 'load_cr return type changed'
    Require (@(Calls $factory 'UnityEngine.SceneManagement.SceneManager' 'LoadSceneAsync').Count -eq 0) 'Iterator factory now starts native I/O eagerly'
    $done = $loader.Fields | Where-Object Name -eq 'doneLoadingSceneAsync'
    Require ($null -ne $done -and -not $done.IsStatic -and $done.FieldType.FullName -eq 'System.Boolean') 'Native completion flag changed'

    $loop = ($loader.NestedTypes | Where-Object Name -like '<loop_cr>*').Methods | Where-Object Name -eq 'MoveNext'
    $fade = @(Calls $loop 'SceneLoader' 'in_cr')
    $load = @(Calls $loop 'SceneLoader' 'load_cr')
    Require ($fade.Count -eq 1 -and $load.Count -eq 1) 'Native loading sequence changed'
    Require ($fade[0].Offset -lt $load[0].Offset) 'Native load no longer follows the fade'
    Require ($fade[0].Next.Operand.Name -eq 'StartCoroutine' -and
        $fade[0].Next.Next.OpCode.Code -eq 'Stfld' -and
        $fade[0].Next.Next.Operand.Name -eq '$current') 'Native loader no longer yields until the fade finishes'
    $completionPoll = @($loop.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq 'doneLoadingSceneAsync'
    })
    Require ($completionPoll.Count -eq 1 -and $completionPoll[0].Offset -gt $load[0].Offset) 'Native loader no longer waits for its completion flag'
    Require ($completionPoll[0].Next.OpCode.Code -in @('Brfalse', 'Brfalse_S')) 'Native completion wait changed'
    $out = @(Calls $loop 'SceneLoader' 'out_cr')
    Require ($out.Count -eq 1 -and $out[0].Offset -gt $completionPoll[0].Offset) 'Scene is revealed before native loading finishes'

    $loadMove = ($loader.NestedTypes | Where-Object Name -like '<load_cr>*').Methods | Where-Object Name -eq 'MoveNext'
    $nativeLoads = @(Calls $loadMove 'UnityEngine.SceneManagement.SceneManager' 'LoadSceneAsync')
    Require ($nativeLoads.Count -eq 2) 'Intermediate/target scene loading contract changed'
    $reset = @($loadMove.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'doneLoadingSceneAsync' -and
        $_.Previous.OpCode.Code -eq 'Ldc_I4_0'
    })
    Require ($reset.Count -eq 1 -and $reset[0].Offset -lt $nativeLoads[0].Offset) 'Native completion reset moved'
    Require (@(Calls $loadMove 'UnityEngine.AsyncOperation' 'set_allowSceneActivation').Count -eq 0) 'Native loader now holds an async activation barrier'

    # Transition.None still covers the old scene, rather than bypassing the
    # loading screen on retries or programmatic native door transitions.
    $fadeMove = ($loader.NestedTypes | Where-Object Name -like '<in_cr>*').Methods | Where-Object Name -eq 'MoveNext'
    $cover = @(Calls $fadeMove 'SceneLoader' 'SetFaderAlpha')
    Require ($cover.Count -eq 1 -and $cover[0].Previous.Operand -eq 1) 'Immediate transition no longer covers the scene'
    Write-Output 'Native loading contract passed: fade completes before lazy scene loading, completion is awaited, and immediate transitions stay covered.'
}
finally {
    $module.Dispose()
}
