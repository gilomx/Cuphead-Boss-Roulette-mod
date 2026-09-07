#requires -Version 7.0
param(
    [string]$CupheadDir = 'C:\Program Files (x86)\Steam\steamapps\common\Cuphead',
    [string]$ModPath = (Join-Path $PSScriptRoot '..\bin\Release\net35\Gilomx.CupheadBossRoulette.dll'),
    [string]$CecilPath = "$env:USERPROFILE\.nuget\packages\mono.cecil\0.10.4\lib\net40\Mono.Cecil.dll"
)

# Inspect native IL without invoking the game. The only mod method executed
# below is pure angle arithmetic; Harmony patch execution still needs Unity.
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$managed = Join-Path $CupheadDir 'Cuphead_Data\Managed'
$module = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $managed 'Assembly-CSharp.dll'))
function Require($condition, [string]$message) {
    if (-not $condition) { throw $message }
}
function Method($type, [string]$name) {
    $method = $type.Methods | Where-Object Name -eq $name | Select-Object -First 1
    Require ($null -ne $method) "Missing native method: $($type.Name).$name"
    return $method
}
function IteratorMove($type, [string]$name) {
    $iterator = $type.NestedTypes | Where-Object { $_.Name.StartsWith('<' + $name + '>') }
    Require ($null -ne $iterator) "Missing native coroutine: $name"
    Require (@($iterator.Fields | Where-Object Name -eq '$this').Count -eq 1) "Missing coroutine owner: $name"
    return Method $iterator 'MoveNext'
}
function Calls($method, [string]$type, [string]$name) {
    return @($method.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.FullName -eq $type -and $_.Operand.Name -eq $name
    })
}
$meteor = $module.Types | Where-Object Name -eq 'DragonLevelMeteor'
$projectile = $module.Types | Where-Object Name -eq 'AbstractProjectile'
$mono = $module.Types | Where-Object Name -eq 'AbstractMonoBehaviour'
$horizontal = IteratorMove $meteor 'moveX_cr'
$vertical = IteratorMove $meteor 'moveY_cr'
$rotation = IteratorMove $meteor 'rotate_cr'
Require (@($horizontal.Body.Instructions | Where-Object {
    $_.OpCode.Code -eq 'Ldc_R4' -and $_.Operand -eq -840
}).Count -eq 1) 'Dragon native horizontal death boundary changed'
Require (@(Calls $horizontal 'UnityEngine.WaitForFixedUpdate' '.ctor').Count -eq 1) 'Dragon X no longer uses fixed updates'
Require (@(Calls $horizontal 'CupheadTime' 'get_FixedDelta').Count -eq 1) 'Dragon X time source changed'
Require (@(Calls $vertical 'UnityEngine.Vector2' '.ctor').Count -eq 2) 'Dragon native Y endpoint construction changed'
Require (@($vertical.Body.Instructions | Where-Object {
    $_.OpCode.Code -eq 'Ldc_R4' -and $_.Operand -eq 300
}).Count -eq 2) 'Dragon native vertical amplitude changed'
Require (@(Calls $vertical 'AbstractMonoBehaviour' 'TweenPositionY').Count -eq 2) 'Dragon native vertical easing changed'
Require (@(Calls $rotation 'TransformExtensions' 'LookAt2D').Count -eq 1) 'Dragon native rotation contract changed'
$tween = IteratorMove $mono 'tweenPositionY_cr'
Require (@(Calls $tween 'AbstractMonoBehaviour' 'get_LocalDeltaTime').Count -eq 1) 'Dragon Y tween local clock changed'
Require (@(Calls $tween 'UnityEngine.WaitForFixedUpdate' '.ctor').Count -eq 0) 'Dragon Y tween now uses fixed updates'
Require (@($tween.Body.Instructions | Where-Object {
    $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq '$current' -and $_.Previous.OpCode.Code -eq 'Ldnull'
}).Count -gt 0) 'Dragon Y tween no longer yields rendered frames'
Require (@(Calls (Method $meteor 'Die') 'AbstractMonoBehaviour' 'StopAllCoroutines').Count -eq 1) 'Dragon native death no longer stops movement'
Require (@(Calls (Method $projectile 'Create') 'UnityEngine.GameObject' 'SetActive').Count -eq 0) 'Projectile factory now activates before marking'
Require ((Method $projectile 'get_dead').IsPublic) 'Dragon dead state is no longer available'
$states = $meteor.NestedTypes | Where-Object Name -eq 'State'
Require (($states.Fields | Where-Object Name -eq 'Up').Constant -eq 1) 'Dragon Up sign changed'
Require (($states.Fields | Where-Object Name -eq 'Down').Constant -eq -1) 'Dragon Down sign changed'
Write-Output 'Native Dragon bounds, easing, coroutine clocks, activation order and death contracts passed.'
$module.Dispose()

# Execute only the production numeric helper. At 120 FPS, a frame with no
# physics X movement still needs the same heading as a 60 FPS sample.
foreach ($name in @('UnityEngine.CoreModule.dll', 'UnityEngine.dll', 'Assembly-CSharp.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $managed $name))
}
$mod = [Reflection.Assembly]::LoadFrom((Resolve-Path $ModPath))
$patch = $mod.GetType('Gilomx.CupheadBossRoulette.DragonFireballsInteractionPatches', $true)
$angle = $patch.GetMethod('RotationDegrees', [Reflection.BindingFlags]'Static,NonPublic')
foreach ($fps in @(30, 60, 120, 144)) {
    $delta = [float](1.0 / $fps)
    foreach ($scale in @(1.0, 1.5, 2.0)) {
        foreach ($verticalSpeed in @(-300, 0, 300)) {
            $arguments = [object[]]@([float](400 * $scale), $delta, [float]($verticalSpeed * $scale * $delta))
            $actual = $angle.Invoke($null, $arguments)
            $expected = [Math]::Atan2(-$verticalSpeed, 400) * 180.0 / [Math]::PI
            Require ([Math]::Abs($actual - $expected) -lt 0.001) "Heading changed with frame rate or camera scale: $fps / $scale"
        }
    }
}
Write-Output 'Dragon production heading passed 36 frame-rate, camera-scale and vertical-direction cases.'
