param(
    [string]$CupheadDir = 'C:\Program Files (x86)\Steam\steamapps\common\Cuphead',
    [string]$CecilPath = "$env:USERPROFILE\.nuget\packages\mono.cecil\0.10.4\lib\net40\Mono.Cecil.dll"
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$module = [Mono.Cecil.ModuleDefinition]::ReadModule(
    (Join-Path $CupheadDir 'Cuphead_Data\Managed\Assembly-CSharp.dll'))
function Require($condition, [string]$message) { if (-not $condition) { throw $message } }
function NativeType([string]$name) {
    $type = $module.Types | Where-Object Name -eq $name
    Require ($null -ne $type) "Missing native type: $name"
    return $type
}
function Method($type, [string]$name) {
    $method = $type.Methods | Where-Object Name -eq $name | Select-Object -First 1
    Require ($null -ne $method) "Missing native method: $($type.Name).$name"
    return $method
}
function Calls($method, [string]$type, [string]$name) {
    @($method.Body.Instructions | Where-Object {
        $_.OpCode.Code -in @('Call', 'Callvirt', 'Newobj') -and
        $_.Operand.DeclaringType.FullName -eq $type -and $_.Operand.Name -eq $name
    })
}
function IteratorMove($type, [string]$name) {
    $iterator = $type.NestedTypes | Where-Object { $_.Name.StartsWith('<' + $name + '>') }
    return Method $iterator 'MoveNext'
}
try {
    $devil = NativeType 'DevilLevelSittingDevil'
    $center = NativeType 'DevilLevelPitchforkSpinnerProjectile'
    $orbit = NativeType 'DevilLevelPitchforkOrbitingProjectile'
    foreach ($entry in @(@('spinnerProjectilePrefab', $center), @('spinnerOrbitingProjectilePrefab', $orbit))) {
        Require (($devil.Fields | Where-Object Name -eq $entry[0]).FieldType.FullName -eq $entry[1].FullName) 'Devil fire prefab changed'
    }
    $init = (Method $devil 'LevelInit').Body.Instructions
    $field = $init | Where-Object { $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'pitchforkFiveFlameSpinnerSpawner' }
    $count = $field.Previous
    while ($count -and $count.OpCode.Code -ne 'Ldc_I4_4') { $count = $count.Previous }
    Require ($null -ne $count -and ($field.Offset - $count.Offset) -lt 30) 'Spinner no longer has four orbiting flames'
    $create = Method $center 'Create'
    Require ($create.IsPublic -and $create.Parameters.Count -eq 6) 'Spinner factory signature changed'
    Require (@(Calls $create 'UnityEngine.MonoBehaviour' 'StartCoroutine').Count -eq 1) 'Spinner factory activation order changed'
    Require (@(Calls $create 'AbstractProjectile' 'SetParryable').Count -eq 1) 'Center is no longer made parryable by its factory'
    $parry = Method $center 'OnParry'
    Require (@(Calls $parry 'UnityEngine.Behaviour' 'set_enabled').Count -eq 1) 'Center parry no longer disables its hitbox'
    Require (@(Calls $parry 'UnityEngine.Renderer' 'set_enabled').Count -eq 1) 'Center parry no longer hides its sprite'
    Require (@(Calls $parry 'UnityEngine.Object' 'Destroy').Count -eq 0) 'Center parry now destroys the orbit target'
    Require (@(Calls $parry 'AbstractProjectile' 'Die').Count -eq 0) 'Center parry now ends the whole formation'
    $move = IteratorMove $center 'main_cr'
    Require (@(Calls $move 'CupheadTime' 'WaitForSeconds').Count -eq 2) 'Spinner dormant/homing timing changed'
    Require (@(Calls $move 'PlayerManager' 'GetNext').Count -eq 1) 'Spinner native target selection changed'
    $fixed = Method $center 'FixedUpdate'
    foreach ($constant in @(10, 1500)) {
        Require (@($fixed.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Ldc_R4' -and $_.Operand -eq $constant }).Count -eq 1) "Spinner coordinate constant changed: $constant"
    }
    Require (@(Calls (Method $orbit 'FixedUpdate') 'CupheadTime' 'get_FixedDelta').Count -eq 1) 'Orbit no longer uses native fixed clock'
    Require (@(Calls (Method $orbit 'Start') 'AbstractProjectile' 'Start').Count -eq 1) 'Orbit damage initialization changed'
    Require (@(Calls (Method $orbit 'FixedUpdate') 'AbstractProjectile' 'get_dead').Count -eq 2) 'Orbit/target death checks changed'
    $groundMove = IteratorMove (NativeType 'GroundHomingMovement') 'loop_cr'
    Require (@(Calls $groundMove 'UnityEngine.Transform' 'set_localPosition').Count -eq 1) 'Homing coordinate-space contract changed'
    foreach ($actor in @($center, $orbit)) {
        Require (@(Calls (Method $actor 'OnCollisionPlayer') 'DamageDealer' 'DealDamage').Count -eq 1) 'Native fire damage changed'
        Require (@(Calls (Method $actor 'Die') 'UnityEngine.Object' 'Destroy').Count -eq 1) 'Native fire destruction changed'
    }
    Write-Output 'Native Devil fire circle: four satellites, pink-center parry, clocks, targeting, local homing, camera patch sites and damage verified.'
}
finally { $module.Dispose() }
