param(
    [string]$CupheadDir = 'C:\Program Files (x86)\Steam\steamapps\common\Cuphead',
    [string]$CecilPath = "$env:USERPROFILE\.nuget\packages\mono.cecil\0.10.4\lib\net40\Mono.Cecil.dll"
)

# Read game IL without invoking Unity. This protects the native factory,
# targeting, motion, hitbox transition and effect site used by the adapter.
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$module = [Mono.Cecil.ModuleDefinition]::ReadModule(
    (Join-Path $CupheadDir 'Cuphead_Data\Managed\Assembly-CSharp.dll'))
function Require($condition, [string]$message) {
    if (-not $condition) { throw $message }
}
function Method($type, [string]$name) {
    $method = $type.Methods | Where-Object Name -eq $name | Select-Object -First 1
    Require ($null -ne $method) "Missing native method: $($type.FullName).$name"
    return $method
}
function Calls($method, [string]$type, [string]$name) {
    @($method.Body.Instructions | Where-Object {
        $_.OpCode.Code -in @('Call', 'Callvirt', 'Newobj') -and
        $_.Operand.DeclaringType.FullName -eq $type -and $_.Operand.Name -eq $name
    })
}
try {
    $ring = $module.Types | Where-Object Name -eq TrainLevelEngineBossDropperProjectile
    $engine = $module.Types | Where-Object Name -eq TrainLevelEngineBoss
    Require ($ring.BaseType.FullName -eq 'AbstractProjectile') 'Ring base projectile changed'
    $prefab = $engine.Fields | Where-Object Name -eq dropperPrefab
    Require ($prefab.FieldType.FullName -eq $ring.FullName) 'Engine ring prefab changed'
    $create = Method $ring 'Create'
    Require ($create.IsPublic -and $create.Parameters.Count -eq 4) 'Ring factory signature changed'
    Require (@(Calls $create 'AbstractMonoBehaviour' 'InstantiatePrefab').Count -eq 1) 'Ring factory no longer clones the native prefab'
    Require (@(Calls $create $ring.FullName 'Init').Count -eq 1) 'Ring factory no longer initializes its clone'
    $init = Method $ring 'Init'
    Require (@(Calls $init 'UnityEngine.MonoBehaviour' 'StartCoroutine').Count -eq 2) 'Ring activation order changed'
    Require (@(Calls $init 'UnityEngine.Transform' 'set_position').Count -eq 1) 'Ring initial position changed'
    $iterator = $ring.NestedTypes | Where-Object { $_.Name.StartsWith('<go_cr>') }
    Require ($null -ne ($iterator.Fields | Where-Object Name -eq '$this')) 'Ring iterator owner changed'
    $move = Method $iterator 'MoveNext'
    Require (@(Calls $move 'PlayerManager' 'GetNext').Count -eq 2) 'Ring selection/retargeting changed'
    Require (@(Calls $move 'AbstractPlayerController' 'get_center').Count -eq 2) 'Ring player-height/direction targeting changed'
    Require (@(Calls $move 'TransformExtensions' 'AddPosition').Count -eq 2) 'Ring native vertical/horizontal motion changed'
    Require (@(Calls $move 'CupheadTime' 'get_Delta').Count -eq 3) 'Ring pause-aware clock changed'
    Require (@($move.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Ldstr' -and $_.Operand -eq 'Horizontal' }).Count -eq 1) 'Ring horizontal animation trigger changed'
    foreach ($fieldName in @('verticalCollider', 'horizontalCollider')) {
        $load = @($move.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq $fieldName })
        Require ($load.Count -eq 1) "Ring hitbox transition changed: $fieldName"
        $expected = if ($fieldName -eq 'verticalCollider') { 'Ldc_I4_0' } else { 'Ldc_I4_1' }
        Require ($load[0].Next.OpCode.Code -eq $expected) "Ring hitbox state changed: $fieldName"
    }
    $dust = @(Calls $move 'Effect' 'Create')
    Require ($dust.Count -eq 1 -and $dust[0].Operand.Parameters.Count -eq 2) 'Ring dust registration site changed'
    Require (@(Calls $move 'Effect' 'Play').Count -eq 1) 'Ring dust playback changed'
    $collision = Method $ring 'OnCollisionPlayer'
    Require (@(Calls $collision 'DamageDealer' 'DealDamage').Count -eq 1) 'Ring player damage changed'
    Require (@(Calls $collision 'AbstractProjectile' 'Die').Count -eq 1) 'Ring impact death changed'
    $train = ($module.Types | Where-Object Name -eq LevelProperties).NestedTypes | Where-Object Name -eq Train
    $settings = (Method $train 'GetMode').Body.Instructions
    $speeds = @()
    for ($i = 0; $i -lt $settings.Count; $i++) {
        if ($settings[$i].OpCode.Code -ne 'Newobj' -or
            $settings[$i].Operand.DeclaringType.FullName -ne 'LevelProperties/Train/Engine') { continue }
        Require ($settings[$i - 3].Operand -eq 650) 'Native ring launch speed changed'
        Require ($settings[$i - 1].Operand -eq 1000) 'Native ring gravity changed'
        $speeds += $settings[$i - 2].Operand
    }
    Require (($speeds | Sort-Object) -join ',' -eq '750,850,1000') 'Native ring difficulty speeds changed'
    Write-Output 'Native train bone ring factory, targeting, clocks, hitboxes, dust, damage and difficulty contracts passed.'
}
finally {
    $module.Dispose()
}
