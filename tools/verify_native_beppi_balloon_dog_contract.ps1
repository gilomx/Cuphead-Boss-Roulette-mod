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
try {
    $helium = NativeType 'ClownLevelClownHelium'
    $dog = NativeType 'ClownLevelDogBalloon'
    $base = NativeType 'AbstractProjectile'
    foreach ($name in @('regularDog', 'pinkDog')) {
        Require (($helium.Fields | Where-Object Name -eq $name).FieldType.FullName -eq $dog.FullName) "Native prefab field changed: $name"
    }
    $spawn = Method $helium 'SpawnBalloonDogs'
    Require (@(Calls $spawn 'PlayerManager' 'GetNext').Count -eq 1) 'Native target selection changed'
    Require (@(Calls $spawn 'UnityEngine.Object' 'Instantiate').Count -eq 1) 'Native prefab construction changed'
    Require (@(Calls $spawn 'ClownLevelDogBalloon' 'Init').Count -eq 1) 'Native initialization changed'
    foreach ($name in @('dogHP', 'dogSpeed')) {
        Require (@($spawn.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq $name }).Count -eq 1) "Native property changed: $name"
    }
    $init = Method $dog 'Init'
    Require ($init.IsPublic -and $init.Parameters.Count -eq 6) 'Dog initializer signature changed'
    foreach ($name in @('CalculateDirection', 'CalculateSin', 'move_cr')) {
        Require (@(Calls $init $dog.FullName $name).Count -eq 1) "Dog initializer no longer calls $name"
    }
    Require (@(Calls $init 'UnityEngine.MonoBehaviour' 'StartCoroutine').Count -eq 1) 'Initializer activation contract changed'
    Require (($dog.Fields | Where-Object Name -eq 'normalized').FieldType.FullName -eq 'UnityEngine.Vector3') 'Native wave direction field changed'
    $iterator = $dog.NestedTypes | Where-Object { $_.Name.StartsWith('<move_cr>') }
    $move = Method $iterator 'MoveNext'
    Require (($iterator.Fields | Where-Object Name -eq '$this').FieldType.FullName -eq $dog.FullName) 'Movement iterator owner changed'
    Require (@($move.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Ldc_R4' -and $_.Operand -eq -560 }).Count -eq 1) 'Arena boundary patch site changed'
    Require (@(Calls $move 'AnimatorExtensions' 'WaitForAnimationToEnd').Count -eq 1) 'Native intro anticipation changed'
    Require (@(Calls $move 'UnityEngine.Transform' 'set_position').Count -eq 1) 'Native world coordinate movement changed'
    Require (@(Calls $move 'CupheadTime' 'get_Delta').Count -eq 3) 'Native movement clocks changed'
    Require (@(Calls $move 'ClownLevelDogBalloon' 'CalculateDirection').Count -eq 0) 'Dog now retargets while moving'
    Require (@(Calls (Method $dog 'Awake') 'DamageReceiver' 'add_OnDamageTaken').Count -eq 1) 'Native damage receiver changed'
    Require (@(Calls (Method $dog 'OnDamageTaken') 'UnityEngine.Animator' 'SetTrigger').Count -eq 1) 'Native death animation changed'
    Require (@(Calls (Method $dog 'OnCollisionPlayer') 'DamageDealer' 'DealDamage').Count -eq 1) 'Native player damage changed'
    Require (@(Calls (Method $dog 'Die') 'AbstractProjectile' 'Die').Count -eq 1) 'Native parry/death changed'
    Require (@(Calls (Method $base 'OnParry') 'AbstractProjectile' 'OnParryDie').Count -eq 1) 'Native parry no longer invokes death'
    Require (@(Calls (Method $base 'OnDieAnimationComplete') 'UnityEngine.Object' 'Destroy').Count -eq 1) 'Native death animation no longer destroys its actor'
    Write-Output 'Native Beppi dogs: prefab pair, targeting, initialization, wave, camera patch site, clocks, damage, parry and death verified.'
}
finally { $module.Dispose() }
