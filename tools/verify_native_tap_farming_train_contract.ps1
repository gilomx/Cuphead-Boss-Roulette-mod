#requires -Version 7.2
[CmdletBinding()]
param([string]$CupheadDir, [string]$CecilPath, [string]$ModPath)

# Inspect native metadata and IL without loading game code or starting Cuphead.
# Reuse portable reference discovery and the reviewed generic damage inventory.
. (Join-Path $PSScriptRoot 'verify_native_tap_farming_contract.ps1') -CupheadDir $CupheadDir -CecilPath $CecilPath
$trainModule = [Mono.Cecil.ModuleDefinition]::ReadModule($nativePath)
try {
    $trainTypes = @(All-Types $trainModule.Types)
    function Train-Type([string]$Name) { $trainTypes | Where-Object FullName -eq $Name }
    foreach ($entry in @(
        @{ Type='TrainLevel'; Field='properties'; Target='LevelProperties/Train' },
        @{ Type='TrainLevel'; Field='currentPhase'; Target='System.Int32' },
        @{ Type='TrainLevel'; Field='blindSpecter'; Target='TrainLevelBlindSpecter' },
        @{ Type='TrainLevel'; Field='skeleton'; Target='TrainLevelSkeleton' },
        @{ Type='TrainLevel'; Field='ghouls'; Target='TrainLevelLollipopGhoulsManager' },
        @{ Type='TrainLevel'; Field='engine'; Target='TrainLevelEngineBoss' },
        @{ Type='TrainLevelLollipopGhoulsManager'; Field='ghoulLeft'; Target='TrainLevelLollipopGhoul' },
        @{ Type='TrainLevelLollipopGhoulsManager'; Field='ghoulRight'; Target='TrainLevelLollipopGhoul' },
        @{ Type='LevelProperties/Train/Entity'; Field='<properties>k__BackingField'; Target='LevelProperties/Train' }
    )) {
        $field = (Train-Type $entry.Type).Fields | Where-Object Name -eq $entry.Field
        Require ($null -ne $field -and !$field.IsStatic -and $field.FieldType.FullName -eq $entry.Target) "Train owned-health metadata changed: $($entry.Type).$($entry.Field)."
    }

    $actors = @(
        @{ Type='TrainLevelBlindSpecter'; Group='BlindSpecter'; Guard='dead' },
        @{ Type='TrainLevelSkeleton'; Group='Skeleton'; Guard='dead' },
        @{ Type='TrainLevelLollipopGhoul'; Group='LollipopGhouls'; Guard='health' },
        @{ Type='TrainLevelEngineBoss'; Group='Engine'; Guard='dead' }
    )
    foreach ($entry in $actors) {
        $type = Train-Type $entry.Type
        $health = $type.Fields | Where-Object Name -eq 'health'
        Require ($type.BaseType.FullName -eq 'LevelProperties/Train/Entity' -and
            $health.FieldType.FullName -eq 'System.Single' -and !$health.IsStatic) "Train actor local HP changed: $($entry.Type)."
        $callback = @($type.Methods | Where-Object Name -eq 'OnDamageTaken')
        Require ($callback.Count -eq 1 -and $callback[0].Parameters.Count -eq 1 -and
            $callback[0].Parameters[0].ParameterType.FullName -eq 'DamageDealer/DamageInfo' -and
            $callback[0].ReturnType.FullName -eq 'System.Void' -and
            $callback[0].Body.ExceptionHandlers.Count -eq 0) "Train damage callback shape changed: $($entry.Type)."
        $method = $callback[0]
        $reads = @(Calls $method 'get_damage')
        Require ($reads.Count -eq 2 -and
            $reads[0].Operand.DeclaringType.FullName -eq 'DamageDealer/DamageInfo' -and
            $reads[1].Operand.DeclaringType.FullName -eq 'DamageDealer/DamageInfo' -and
            $reads[0].Previous.OpCode.Code -eq 'Ldarg_1' -and
            $reads[1].Previous.OpCode.Code -eq 'Ldarg_1' -and
            $reads[0].Next.Operand.Name -eq 'Invoke' -and
            $reads[1].Next.OpCode.Code -eq 'Sub' -and
            $reads[1].Next.Next.OpCode.Code -eq 'Stfld' -and
            $reads[1].Next.Next.Operand.FullName -eq $health.FullName) "Train timeline/local HP damage reads changed: $($entry.Type)."
        Require (@(Calls $method 'DealDamage' | Where-Object {
            $_.Operand.DeclaringType.Name -eq 'AbstractLevelProperties`3'
        }).Count -eq 0) "Train local callback now also applies generic boss damage: $($entry.Type)."
        $eventReads = @($method.Body.Instructions | Where-Object {
            $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq 'OnDamageTakenEvent'
        })
        Require ($eventReads.Count -eq 2 -and $eventReads[0].Previous.OpCode.Code -eq 'Ldarg_0' -and
            $eventReads[0].Next.OpCode.Code -eq 'Brfalse') "Train optional timeline event routing changed: $($entry.Type)."
        $aliveEntry = $eventReads[0].Previous
        $liveGuard = @($method.Body.Instructions | Where-Object { $_.Operand -eq $aliveEntry })
        Require ($liveGuard.Count -eq 1 -and $liveGuard[0].Next.OpCode.Code -eq 'Ret' -and
            $liveGuard[0].Previous.OpCode.Code -eq $(if ($entry.Guard -eq 'dead') { 'Ldfld' } else { 'Ldc_R4' })) "Train live-actor guard changed: $($entry.Type)."
        Require ($eventReads[0].Next.Operand.Offset -gt $reads[0].Offset -and
            $eventReads[0].Next.Operand.Offset -lt $reads[1].Offset) "Train damage preparation must precede the optional event branch: $($entry.Type)."
        $init = $type.Methods | Where-Object Name -eq 'LevelInit'
        Require (@(Calls $init 'LevelInit' | Where-Object {
            $_.Operand.DeclaringType.FullName -eq 'LevelProperties/Train/Entity'
        }).Count -eq 1 -and @($init.Body.Instructions | Where-Object {
            $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq 'health' -and
            $_.Operand.DeclaringType.FullName -eq ('LevelProperties/Train/' + $entry.Group)
        }).Count -eq 1 -and @($init.Body.Instructions | Where-Object {
            $_.OpCode.Code -eq 'Stfld' -and $_.Operand.FullName -eq $health.FullName
        }).Count -eq 1) "Train original local HP initialization changed: $($entry.Type)."
    }

    $level = Train-Type 'TrainLevel'
    $start = $level.Methods | Where-Object Name -eq 'Start'
    Require (@(Calls $start 'add_OnDamageTakenEvent').Count -eq 4 -and
        @($start.Body.Instructions | Where-Object {
            $_.OpCode.Code -eq 'Ldftn' -and $_.Operand.Name -eq 'DealDamage' -and
            $_.Operand.DeclaringType.FullName -eq 'Level/Timeline'
        }).Count -eq 5) 'Train must route all four local actor events into its separate native timeline.'
    $managerInit = (Train-Type 'TrainLevelLollipopGhoulsManager').Methods | Where-Object Name -eq 'LevelInit'
    Require (@(Calls $managerInit 'add_OnDamageTakenEvent').Count -eq 2 -and
        @(Calls $managerInit 'add_OnDeathEvent').Count -eq 2) 'Train ghoul manager no longer owns two independent local HP pools.'
    foreach ($entry in @(
        @{ Method='OnLevelStart'; Phase='Ldc_I4_1' },
        @{ Method='OnBlindSpecterDeath'; Phase='Ldc_I4_2' },
        @{ Method='OnSkeletonDeath'; Phase='Ldc_I4_3' },
        @{ Method='OnLollipopsDeath'; Phase='Ldc_I4_4' }
    )) {
        $advance = @(Calls ($level.Methods | Where-Object Name -eq $entry.Method) 'setPhase')
        Require ($advance.Count -eq 1 -and $advance[0].Previous.OpCode.Code -eq $entry.Phase) "Train phase lifecycle changed: $($entry.Method)."
    }
    $ghoulDeath = $level.Methods | Where-Object Name -eq 'OnLollipopsDeath'
    Require (@(Calls $ghoulDeath 'get_mode').Count -eq 1 -and
        @(Calls $ghoulDeath 'get_mode')[0].Next.OpCode.Code -eq 'Brtrue' -and
        @(Calls $ghoulDeath 'WinInstantly').Count -eq 1 -and
        @(Calls ($level.Methods | Where-Object Name -eq 'OnEngineDeath') 'WinInstantly').Count -eq 1) 'Train Easy mode must end after both ghouls; Normal/Hard end after the engine.'

    # Interpret factory constructor literals, never invoke native GetMode.
    $factory = (Train-Type 'LevelProperties/Train').Methods | Where-Object Name -eq 'GetMode'
    $stack = [Collections.Generic.List[object]]::new()
    $locals = @{}
    $states = [Collections.Generic.List[object]]::new()
    function Pop-TrainLiteral {
        $value = $stack[$stack.Count - 1]
        $stack.RemoveAt($stack.Count - 1)
        return ,$value
    }
    foreach ($instruction in $factory.Body.Instructions) {
        switch -Regex ([string]$instruction.OpCode.Code) {
            '^Ldc_I4_([0-8])$' { $stack.Add([int]$Matches[1]); break }
            '^Ldc_(I4|I4_S|R4)$' { $stack.Add($instruction.Operand); break }
            '^Ldnull$' { $stack.Add($null); break }
            '^Ldstr$' { $stack.Add($instruction.Operand); break }
            '^Ldarg_0$' { $stack.Add('mode'); break }
            '^Stloc_([0-2])$' { $locals[[int]$Matches[1]] = Pop-TrainLiteral; break }
            '^Ldloc_([0-2])$' { $stack.Add($locals[[int]$Matches[1]]); break }
            '^Dup$' { $stack.Add($stack[$stack.Count - 1]); break }
            '^Newarr$' { $null = Pop-TrainLiteral; $stack.Add(@{ kind='array' }); break }
            '^Stelem_Ref$' { $null=Pop-TrainLiteral; $null=Pop-TrainLiteral; $null=Pop-TrainLiteral; break }
            '^Brfalse$' { $null=Pop-TrainLiteral; break }
            '^Beq$' { $null=Pop-TrainLiteral; $null=Pop-TrainLiteral; break }
            '^Br$' { break }
            '^Newobj$' {
                $ctor = $instruction.Operand
                $arguments = @{}
                for ($i = $ctor.Parameters.Count - 1; $i -ge 0; $i--) {
                    $arguments[$ctor.Parameters[$i].Name] = Pop-TrainLiteral
                }
                if ($ctor.DeclaringType.FullName -eq 'LevelProperties/Train/State') {
                    $states.Add($arguments)
                }
                $stack.Add(@{ type=$ctor.DeclaringType.FullName; args=$arguments })
                break
            }
            '^Callvirt$' {
                for ($i=0; $i -lt $instruction.Operand.Parameters.Count; $i++) { $null=Pop-TrainLiteral }
                $null=Pop-TrainLiteral
                break
            }
            default { throw "Unhandled Train factory literal opcode: $($instruction.OpCode.Code)." }
        }
        if ($states.Count -eq 3) { break }
    }
    Require ($states.Count -eq 3) 'Train difficulty factory no longer has three reviewed configurations.'
    $expected = @(@(400,320,180,230), @(425,325,200,200), @(425,325,200,200))
    for ($i=0; $i -lt $states.Count; $i++) {
        $state = $states[$i]
        $actual = @($state.blindSpecter.args.health, $state.skeleton.args.health,
            $state.lollipopGhouls.args.health, $state.engine.args.health)
        Require (($actual -join ',') -eq ($expected[$i] -join ',') -and $state.stateName -eq 0) "Train difficulty $i original health pools changed."
    }
    Write-Output 'PASS Native Train contract: five owned HP pools, four guarded damage callbacks, shared event/local damage, exact phase lifecycle and Simple/Normal/Expert totals 1080/1350/1350.'

    if ($ModPath) {
        $compiled = [Mono.Cecil.ModuleDefinition]::ReadModule((Resolve-ReferencePath $ModPath))
        try {
            $compiledTypes = @(All-Types $compiled.Types)
            $adapter = $compiledTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.CreatorToolsTapFarmingTrainHealth'
            $bridge = $compiledTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.Plugin/CreatorToolsTapFarmingDamagePatch'
            $controller = $compiledTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.CreatorToolsTapFarmingController'
            Require ($null -ne $adapter -and $null -ne $bridge -and $null -ne $controller) 'Compiled Train health adapter/bridge is missing.'
            foreach ($name in @('TryCapture','TryRead','OwnsActor')) {
                Require (@($adapter.Methods | Where-Object Name -eq $name).Count -eq 1) "Compiled Train adapter lacks $name."
            }
            $enabled = $bridge.Fields | Where-Object Name -eq 'TrainLocalDamageEnabled'
            Require ($null -ne $enabled -and $enabled.IsStatic -and $enabled.FieldType.FullName -eq 'System.Boolean') 'Compiled Train local-damage installation guard is missing.'
            $transpiler = $bridge.Methods | Where-Object Name -eq 'TrainLocalDamageTranspiler'
            $read = $bridge.Methods | Where-Object Name -eq 'ReadTrainLocalDamage'
            $postfix = $bridge.Methods | Where-Object Name -eq 'TrainDamagePostfix'
            Require ($null -ne $transpiler -and $null -ne $read -and $null -ne $postfix -and
                $read.ReturnType.FullName -eq 'System.Single' -and $read.Parameters.Count -eq 3 -and
                $read.Parameters[0].ParameterType.FullName -eq 'DamageDealer/DamageInfo' -and
                $read.Parameters[1].ParameterType.FullName -eq 'System.Object' -and
                $read.Parameters[2].ParameterType.FullName -eq 'System.Single') 'Compiled Train local-damage callback shape changed.'
            Require (@(Calls $read 'get_damage').Count -eq 1 -and
                @(Calls $read 'PrepareTapFarmingTrainDamage').Count -eq 1 -and
                @($read.Body.ExceptionHandlers | Where-Object HandlerType -eq 'Finally').Count -eq 1 -and
                @($read.Body.Instructions | Where-Object {
                    $_.OpCode.Code -eq 'Stfld' -and $_.Operand.DeclaringType.FullName -eq 'DamageDealer/DamageInfo'
                }).Count -eq 0) 'Compiled Train helper must account once without mutating the original DamageInfo.'
            Require (@(Calls $transpiler 'DeclareLocal').Count -eq 1 -and
                @(Calls $postfix 'ObserveTapFarmingTrainDamage').Count -eq 1) 'Compiled Train bridge must prepare one shared local and refresh local HP after native damage/death.'
            $prepare = $controller.Methods | Where-Object Name -eq 'PrepareTrainDamage'
            $observe = $controller.Methods | Where-Object Name -eq 'ObserveTrainDamage'
            Require (@(Calls $prepare 'OwnsActor').Count -eq 1 -and
                @(Calls $prepare 'PrepareBossDamage').Count -eq 1 -and
                @(Calls $observe 'OwnsActor').Count -eq 1 -and
                @(Calls $observe 'ObserveBossDamage').Count -eq 1) 'Compiled Train actor callbacks must share reserve accounting only for owned actors.'
            $builder = $controller.Methods | Where-Object Name -eq 'BuildLocalHealthSnapshot'
            $refresh = $controller.Methods | Where-Object Name -eq 'RefreshBossSnapshotMainThread'
            Require ($null -ne $builder -and $builder.Parameters[0].ParameterType.FullName -eq 'System.Double[]' -and
                @(Calls $refresh 'BuildLocalHealthSnapshot').Count -ge 2 -and
                @(Calls $refresh 'TryRead' | Where-Object {
                    $_.Operand.DeclaringType.Name -eq 'CreatorToolsTapFarmingTrainHealth'
                }).Count -eq 1 -and
                @(Calls $refresh 'TryRead' | Where-Object {
                    $_.Operand.DeclaringType.Name -eq 'CreatorToolsTapFarmingRobotHealth'
                }).Count -eq 1) 'Compiled Train/Robot snapshots must share the local-health projection while reading their own adapters.'
            $discovery = @($adapter.Methods | Where-Object HasBody | ForEach-Object {
                $_.Body.Instructions | Where-Object {
                    $_.Operand -is [Mono.Cecil.MethodReference] -and
                    $_.Operand.Name -in @('FindObjectOfType','FindObjectsOfType','FindObjectsOfTypeAll','GetMode')
                }
            })
            Require ($discovery.Count -eq 0) 'Compiled Train adapter must use owned actors without scene discovery or native factories.'
            Write-Output 'PASS Compiled Train adapter/bridge: guarded local damage, one damage read/accounting path, immutable DamageInfo and owned actor discovery.'
        }
        finally { $compiled.Dispose() }
    }
}
finally { $trainModule.Dispose() }
