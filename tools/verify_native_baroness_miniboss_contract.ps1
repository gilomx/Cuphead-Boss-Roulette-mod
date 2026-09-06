param(
    [string]$CupheadDir = 'C:\Program Files (x86)\Steam\steamapps\common\Cuphead',
    [string]$CecilPath = "$env:USERPROFILE\.nuget\packages\mono.cecil\0.10.4\lib\net40\Mono.Cecil.dll"
)

# Reads game IL without loading Unity or invoking any game methods. This
# validates the installed game contract that the adapter depends on; gameplay
# animations, collision placement and Harmony execution still need in-game QA.
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$assemblyPath = Join-Path $CupheadDir 'Cuphead_Data\Managed\Assembly-CSharp.dll'
$module = [Mono.Cecil.ModuleDefinition]::ReadModule($assemblyPath)
function Require($condition, [string]$message) {
    if (-not $condition) { throw $message }
}
function NativeType([string]$name) {
    $type = $module.Types | Where-Object Name -eq $name | Select-Object -First 1
    Require ($null -ne $type) "Missing native type: $name"
    return $type
}
function NativeMethod($type, [string]$name) {
    $method = $type.Methods | Where-Object Name -eq $name | Select-Object -First 1
    Require ($null -ne $method) "Missing native method: $($type.Name).$name"
    return $method
}
function IntegerConstant($instruction) {
    $code = $instruction.OpCode.Code.ToString()
    if ($code -eq 'Ldc_I4' -or $code -eq 'Ldc_I4_S') { return [int]$instruction.Operand }
    if ($code -match '^Ldc_I4_([0-8])$') { return [int]$Matches[1] }
    throw "Expected native HP constant, found $instruction"
}

$base = NativeType 'BaronessLevelMiniBossBase'
$castle = NativeType 'BaronessLevelCastle'
$baroness = (NativeType 'LevelProperties').NestedTypes | Where-Object Name -eq 'Baroness'
$modeInstructions = (NativeMethod $baroness 'GetMode').Body.Instructions
$contracts = @(
    @{ Group='Gumball'; Prefab='gumballPrefab'; Args=3; HP=@(270,270,320) },
    @{ Group='Waffle'; Prefab='wafflePrefab'; Args=5; HP=@(250,250,305) },
    @{ Group='CandyCorn'; Prefab='candyCornPrefab'; Args=4; HP=@(225,225,250) },
    @{ Group='Cupcake'; Prefab='cupcakePrefab'; Args=3; HP=@(185,185,235) },
    @{ Group='Jawbreaker'; Prefab='jawBreakerPrefab'; Args=5; HP=@(180,180,220) }
)
foreach ($contract in $contracts) {
    $name = "BaronessLevel$($contract.Group)"
    $type = NativeType $name
    Require ($type.BaseType.FullName -eq $base.FullName) "$name no longer derives from native miniboss base"
    $prefab = $castle.Fields | Where-Object Name -eq $contract.Prefab
    Require ($prefab.FieldType.FullName -eq $name) "Castle prefab changed: $($contract.Prefab)"
    $init = NativeMethod $type 'Init'
    Require ($init.IsPublic -and $init.Parameters.Count -eq $contract.Args) "$name.Init signature changed"
    Require ($init.Parameters[-1].Name -eq 'health') "$name.Init no longer takes explicit health"
    Require (@($init.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'health'
    }).Count -eq 1) "$name.Init no longer assigns its independent health field"
    $damage = NativeMethod $type 'OnDamageTaken'
    Require (@($damage.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Sub'
    }).Count -gt 0) "$name no longer subtracts damage from its health"
    $actualHP = @()
    for ($i = 0; $i -lt $modeInstructions.Count; $i++) {
        $instruction = $modeInstructions[$i]
        if ($instruction.OpCode.Code -ne 'Newobj' -or
            $instruction.Operand.DeclaringType.FullName -ne "LevelProperties/Baroness/$($contract.Group)") { continue }
        $start = $i - 1
        while ($start -ge 0) {
            $previous = $modeInstructions[$start]
            if ($previous.OpCode.Code -eq 'Newobj' -and
                $previous.Operand.DeclaringType.FullName.StartsWith('LevelProperties/Baroness/')) { break }
            $start--
        }
        Require ($start -ge 0) "Cannot locate $name native property arguments"
        $hpOffset = if ($contract.Group -eq 'Jawbreaker') { 4 } else { 1 }
        $actualHP += IntegerConstant $modeInstructions[$start + $hpOffset]
    }
    Require (($actualHP -join ',') -eq ($contract.HP -join ',')) "$name native HP changed: $actualHP"
    Write-Output ("{0}: Init health independent; Easy/Normal/Expert HP={1}" -f $name, ($actualHP -join '/'))
}

$baseDamage = NativeMethod $base 'OnDamageTaken'
Require (@($baseDamage.Body.Instructions | Where-Object {
    $_.Operand -is [Mono.Cecil.MethodReference] -and
    ($_.Operand.DeclaringType.FullName -eq 'Level' -or $_.Operand.DeclaringType.FullName -eq 'Level/Timeline')
}).Count -eq 0) 'Base miniboss damage now directly alters the main boss'

# This class also inherits AbstractProjectile.Create(). The integration must
# select this explicit overload, whose return type matches its tracking hook.
$gumballCreate = @((NativeType 'BaronessLevelGumballProjectile').Methods | Where-Object {
    $_.Name -eq 'Create' -and
    ($_.Parameters.ParameterType.FullName -join ',') -eq 'UnityEngine.Vector2,UnityEngine.Vector2,System.Single'
})
Require ($gumballCreate.Count -eq 1 -and $gumballCreate[0].ReturnType.FullName -eq 'BaronessLevelGumballProjectile') 'Typed Gumball projectile factory contract changed'

$coordinateContracts = @(
    @('BaronessLevelCandyCorn', 'MoveAlongX', -640),
    @('BaronessLevelCandyCorn', 'MoveAlongY', 360),
    @('BaronessLevelCupcake', 'GoingUp', 360),
    @('BaronessLevelCupcake', 'GoingDown', 120),
    @('BaronessLevelCupcake', 'BoundaryCheck', -540),
    @('BaronessLevelCupcake', 'BoundaryCheck', 540),
    @('BaronessLevelCandyCornMini', 'FixedUpdate', 720),
    @('BaronessLevelGumballProjectile', 'Update', -360)
)
foreach ($contract in $coordinateContracts) {
    $method = NativeMethod (NativeType $contract[0]) $contract[1]
    Require (@($method.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldc_R4' -and [float]$_.Operand -eq $contract[2]
    }).Count -gt 0) "Native arena coordinate changed: $($contract -join '/')"
}
foreach ($contract in @(
    @('BaronessLevelGumball', '<move_cr>', $null),
    @('BaronessLevelCandyCorn', '<spawnMinis_cr>', 'BaronessLevelCandyCornMini'),
    @('BaronessLevelCandyCorn', '<death_cr>', $null),
    @('BaronessLevelCupcake', '<splash_cr>', 'Effect'),
    @('BaronessLevelWaffle', '<enter_cr>', $null),
    @('BaronessLevelJawbreaker', '<minis_cr>', 'BaronessLevelJawbreakerMini'),
    @('BaronessLevelJawbreaker', '<dying_cr>', 'BaronessLevelJawbreakerGhost'),
    @('BaronessLevelGumballProjectile', '<spawn_trail_cr>', $null)
)) {
    $iterator = (NativeType $contract[0]).NestedTypes | Where-Object { $_.Name.StartsWith($contract[1]) }
    Require ($null -ne $iterator) "Missing coroutine contract: $($contract[0]).$($contract[1])"
    Require (@($iterator.Fields | Where-Object Name -eq '$this').Count -eq 1) 'Coroutine no longer exposes its native owner'
    if ($null -ne $contract[2]) {
        Require (@($iterator.Fields | Where-Object { $_.FieldType.FullName -eq $contract[2] }).Count -gt 0) 'Coroutine child ownership contract changed'
    }
}
# Cupcake's landing and splash must share the water surface. Validate every
# native Ground read, including the coroutine whose effects outlive a bounce.
$cupcake = NativeType 'BaronessLevelCupcake'
$cupcakeSplash = $cupcake.NestedTypes | Where-Object { $_.Name.StartsWith('<splash_cr>') }
foreach ($groundContract in @(
    @{ Method = (NativeMethod $cupcake 'GoingDown'); Count = 2 },
    @{ Method = (NativeMethod $cupcakeSplash 'MoveNext'); Count = 1 }
)) {
    $groundReads = @($groundContract.Method.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Callvirt' -and $_.Operand.Name -eq 'get_Ground' -and
        $_.Operand.DeclaringType.FullName -eq 'Level'
    })
    Require ($groundReads.Count -eq $groundContract.Count) "Cupcake water-floor reads changed: $($groundContract.Method.FullName)"
}
$cornInit = NativeMethod (NativeType 'BaronessLevelCandyCorn') 'Init'
Require (@($cornInit.Body.Instructions | Where-Object {
    $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'bottomPoint'
}).Count -eq 1) 'CandyCorn no longer stores its lower lane from the spawn position'

# The original pursuer accepts the same abstract controller used by aircraft;
# keeping its native center targeting preserves normal and cooperative play.
$jawbreaker = NativeType 'BaronessLevelJawbreaker'
$jawbreakerInit = NativeMethod $jawbreaker 'Init'
Require ($jawbreakerInit.Parameters[1].ParameterType.FullName -eq 'AbstractPlayerController') 'Jawbreaker no longer accepts the shared player controller'
Require (@((NativeMethod $jawbreaker 'FixedUpdate').Body.Instructions | Where-Object {
    $_.Operand -is [Mono.Cecil.MethodReference] -and
    $_.Operand.DeclaringType.FullName -eq 'AbstractPlayerController' -and
    $_.Operand.Name -eq 'get_center'
}).Count -eq 1) 'Jawbreaker native pursuit no longer targets the player center'
Require ((NativeType 'PlanePlayerController').BaseType.FullName -eq 'AbstractPlayerController') 'Aircraft player inheritance changed'

# The two CandyCorn prefabs use solid kinematic colliders. Their aircraft
# copies use triggers so solid plane bullets can generate contacts. Both
# physics callbacks must still dispatch through the original collision code.
$collidable = NativeType 'AbstractCollidableObject'
foreach ($contact in @('OnTriggerEnter2D', 'OnTriggerStay2D', 'OnTriggerExit2D',
    'OnCollisionEnter2D', 'OnCollisionStay2D', 'OnCollisionExit2D')) {
    Require (@((NativeMethod $collidable $contact).Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.FullName -eq 'AbstractCollidableObject' -and
        $_.Operand.Name -eq 'checkCollision'
    }).Count -eq 1) "Native contact dispatch changed: $contact"
}
$miniCorn = NativeType 'BaronessLevelCandyCornMini'
Require (@((NativeMethod $miniCorn 'Awake').Body.Instructions | Where-Object {
    $_.OpCode.Code -eq 'Ldftn' -and
    $_.Operand.DeclaringType.FullName -eq 'BaronessLevelCandyCornMini' -and
    $_.Operand.Name -eq 'OnDamageTaken'
}).Count -eq 1) 'Mini CandyCorn no longer subscribes its native damage handler'
Require (@((NativeMethod $miniCorn 'OnDamageTaken').Body.Instructions | Where-Object {
    $_.OpCode.Code -eq 'Sub'
}).Count -eq 1) 'Mini CandyCorn no longer subtracts damage from its own health'
Require (@((NativeMethod $miniCorn 'OnDamageTaken').Body.Instructions | Where-Object {
    $_.Operand -is [Mono.Cecil.MethodReference] -and
    $_.Operand.DeclaringType.FullName -eq 'AbstractProjectile' -and
    $_.Operand.Name -eq 'Die'
}).Count -eq 1) 'Mini CandyCorn native damage death path changed'

# Cala's splash trigger is the physical waterline. Its head phase can keep
# wave objects alive, so availability also depends on the native level phase.
$waterHead = NativeType 'FlyingMermaidLevelMerdusaHead'
foreach ($wave in @('wave1', 'wave2')) {
    Require (@($waterHead.Fields | Where-Object {
        $_.Name -eq $wave -and $_.FieldType.FullName -eq 'UnityEngine.SpriteRenderer'
    }).Count -eq 1) "Cala water renderer reference changed: $wave"
}
$mermaidLevel = NativeType 'FlyingMermaidLevel'
Require (@($mermaidLevel.Fields | Where-Object {
    $_.Name -eq 'properties' -and $_.FieldType.FullName -eq 'LevelProperties/FlyingMermaid'
}).Count -eq 1) 'Cala native phase properties changed'
$mermaidProperties = (NativeType 'LevelProperties').NestedTypes | Where-Object Name -eq 'FlyingMermaid'
$mermaidStates = $mermaidProperties.NestedTypes | Where-Object Name -eq 'States'
Require (@($mermaidStates.Fields | Where-Object {
    $_.Name -eq 'Head' -and $_.Constant -eq 3
}).Count -eq 1) 'Cala head phase enum changed'
Require (@((NativeMethod $mermaidLevel 'OnStateChanged').Body.Instructions | Where-Object {
    $_.Operand -is [Mono.Cecil.MethodReference] -and
    $_.Operand.Name -eq 'transform_to_head_cr'
}).Count -eq 1) 'Cala native head transition changed'
$splashManager = NativeType 'FlyingMermaidLevelSplashManager'
Require ((NativeMethod $splashManager 'OnTriggerEnter2D').Parameters[0].ParameterType.FullName -eq 'UnityEngine.Collider2D') 'Cala native water detection is no longer a 2D trigger'

Write-Output 'Native Baroness miniboss HP, prefab, damage, coordinates, water-floor, aircraft contacts/targeting and secondary-ownership contracts passed.'
$module.Dispose()
