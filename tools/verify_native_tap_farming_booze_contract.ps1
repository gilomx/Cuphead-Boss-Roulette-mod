#requires -Version 7.2
[CmdletBinding()]
param([string]$CupheadDir, [string]$CecilPath, [string]$ModPath)

# Read native IL only. Tipsy Troop is the sole shipped Dice Palace miniboss
# whose actual boss HP bypasses AbstractLevelProperties.DealDamage(float).
. (Join-Path $PSScriptRoot 'verify_native_tap_farming_contract.ps1') -CupheadDir $CupheadDir -CecilPath $CecilPath
$boozeModule = [Mono.Cecil.ModuleDefinition]::ReadModule($nativePath)
try {
    $boozeTypes = @(All-Types $boozeModule.Types)
    function Booze-Type([string]$Name) { $boozeTypes | Where-Object FullName -eq $Name }
    foreach ($entry in @(
        @{ Type='DicePalaceBoozeLevel'; Field='properties'; Target='LevelProperties/DicePalaceBooze' },
        @{ Type='DicePalaceBoozeLevel'; Field='decanter'; Target='DicePalaceBoozeLevelDecanter' },
        @{ Type='DicePalaceBoozeLevel'; Field='martini'; Target='DicePalaceBoozeLevelMartini' },
        @{ Type='DicePalaceBoozeLevel'; Field='tumbler'; Target='DicePalaceBoozeLevelTumbler' },
        @{ Type='DicePalaceBoozeLevelBossBase'; Field='health'; Target='System.Single' },
        @{ Type='LevelProperties/DicePalaceBooze/Entity'; Field='<properties>k__BackingField'; Target='LevelProperties/DicePalaceBooze' }
    )) {
        $field = (Booze-Type $entry.Type).Fields | Where-Object Name -eq $entry.Field
        Require ($null -ne $field -and !$field.IsStatic -and $field.FieldType.FullName -eq $entry.Target) "Tipsy Troop owned HP metadata changed: $($entry.Type).$($entry.Field)."
    }
    $base = Booze-Type 'DicePalaceBoozeLevelBossBase'
    $health = $base.Fields | Where-Object Name -eq 'health'
    Require ($base.BaseType.FullName -eq 'LevelProperties/DicePalaceBooze/Entity' -and
        @($base.Properties | Where-Object { $_.Name -eq 'isDead' -and $_.PropertyType.FullName -eq 'System.Boolean' }).Count -eq 1) 'Tipsy Troop shared local HP/death metadata changed.'
    foreach ($entry in @(
        @{ Type='DicePalaceBoozeLevelDecanter'; Group='Decanter'; HP='decanterHP' },
        @{ Type='DicePalaceBoozeLevelMartini'; Group='Martini'; HP='martiniHP' },
        @{ Type='DicePalaceBoozeLevelTumbler'; Group='Tumbler'; HP='tumblerHP' }
    )) {
        $type = Booze-Type $entry.Type
        Require ($type.BaseType.FullName -eq $base.FullName) "Tipsy Troop owned boss actor base changed: $($entry.Type)."
        $callbacks = @($type.Methods | Where-Object Name -eq 'OnDamageTaken')
        Require ($callbacks.Count -eq 1 -and $callbacks[0].HasBody -and
            $callbacks[0].Parameters.Count -eq 1 -and $callbacks[0].Parameters[0].ParameterType.FullName -eq 'DamageDealer/DamageInfo' -and
            $callbacks[0].Body.ExceptionHandlers.Count -eq 0 -and $callbacks[0].Body.Variables.Count -ge 1 -and
            $callbacks[0].Body.Variables[0].VariableType.FullName -eq 'System.Single') "Tipsy Troop callback shape changed: $($entry.Type)."
        $method = $callbacks[0]
        $il = $method.Body.Instructions
        $reads = @(Calls $method 'get_damage')
        Require ($il[0].OpCode.Code -eq 'Ldarg_0' -and $il[1].Operand.FullName -eq $health.FullName -and
            $il[2].OpCode.Code -eq 'Stloc_0' -and $reads.Count -eq 1 -and
            $reads[0].Operand.DeclaringType.FullName -eq 'DamageDealer/DamageInfo' -and $reads[0].Previous.OpCode.Code -eq 'Ldarg_1' -and
            $reads[0].Next.OpCode.Code -eq 'Sub' -and $reads[0].Next.Next.OpCode.Code -eq 'Stfld' -and
            $reads[0].Next.Next.Operand.FullName -eq $health.FullName) "Tipsy Troop original-health capture/single local subtraction changed: $($entry.Type)."
        Require (@(Calls $method 'DealDamage' | Where-Object { $_.Operand.DeclaringType.Name -eq 'AbstractLevelProperties`3' }).Count -eq 0 -and
            @(Calls $method 'DealDamage' | Where-Object { $_.Operand.DeclaringType.FullName -eq 'Level/Timeline' }).Count -eq 1 -and
            @(Calls $method 'Clamp').Count -eq 1 -and @(Calls $method 'get_isDead').Count -eq 1 -and
            @(Calls $method 'StartDying').Count -eq 1) "Tipsy Troop timeline/death routing changed: $($entry.Type)."
        $init = $type.Methods | Where-Object Name -eq 'LevelInit'
        Require (@(Calls $init 'LevelInit' | Where-Object { $_.Operand.DeclaringType.FullName -eq $base.FullName }).Count -eq 1 -and
            @($init.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq $entry.HP -and
                $_.Operand.DeclaringType.FullName -eq ('LevelProperties/DicePalaceBooze/' + $entry.Group) }).Count -eq 1) "Tipsy Troop original owned HP initialization changed: $($entry.Type)."
    }
    $start = (Booze-Type 'DicePalaceBoozeLevel').Methods | Where-Object Name -eq 'Start'
    Require (@(Calls $start 'LevelInit').Count -eq 3 -and
        @(Calls $start 'remove_OnBossDamaged').Count -eq 1 -and
        @($start.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Stfld' -and $_.Operand.FullName -eq 'System.Single Level/Timeline::health' }).Count -eq 1) 'Tipsy Troop must replace the generic timeline with its three local pools.'
    $allDead = $base.Methods | Where-Object Name -eq 'AllDead'
    $dying = $base.Methods | Where-Object Name -eq 'StartDying'
    Require (@(Calls $dying 'AllDead').Count -eq 1 -and
        @($dying.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Ldc_I4_3' }).Count -eq 1 -and
        @(Calls $allDead 'DealDamageToNextNamedState').Count -eq 1) 'Tipsy Troop must advance its generic state only after all three native deaths.'

    # Confirm that the other shipped minibosses and final King Dice retain
    # the generic bridge. Destructible olives/boomerangs/bots are excluded.
    foreach ($name in @('DicePalaceChipsLevelChips','DicePalaceCigarLevelCigar','DicePalaceDominoLevelDomino',
        'DicePalaceEightBallLevelEightBall','DicePalaceFlyingHorseLevelHorse','DicePalaceFlyingMemoryLevelStuffedToy',
        'DicePalaceRabbitLevelRabbit','DicePalaceRouletteLevelRoulette','DicePalaceMainLevelKingDice')) {
        $method = (Booze-Type $name).Methods | Where-Object Name -eq 'OnDamageTaken'
        Require (@(Calls $method 'DealDamage' | Where-Object { $_.Operand.DeclaringType.Name -eq 'AbstractLevelProperties`3' }).Count -eq 1 -and
            @(Calls $method 'get_damage').Count -eq 1) "Dice Palace generic player-damage route changed: $name."
    }

    # Interpret constructor literals in all three difficulty branches;
    # never execute the game's property factory or load Unity game code.
    $factory = (Booze-Type 'LevelProperties/DicePalaceBooze').Methods | Where-Object Name -eq 'GetMode'
    $stack = [Collections.Generic.List[object]]::new()
    $locals = @{}
    $states = [Collections.Generic.List[object]]::new()
    function Pop-BoozeLiteral {
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
            '^Stloc_([0-2])$' { $locals[[int]$Matches[1]] = Pop-BoozeLiteral; break }
            '^Ldloc_([0-2])$' { $stack.Add($locals[[int]$Matches[1]]); break }
            '^Dup$' { $stack.Add($stack[$stack.Count - 1]); break }
            '^Newarr$' { $null = Pop-BoozeLiteral; $stack.Add(@{ kind='array' }); break }
            '^Stelem_Ref$' { $null=Pop-BoozeLiteral; $null=Pop-BoozeLiteral; $null=Pop-BoozeLiteral; break }
            '^Brfalse$' { $null=Pop-BoozeLiteral; break }
            '^Beq$' { $null=Pop-BoozeLiteral; $null=Pop-BoozeLiteral; break }
            '^Br$' { break }
            '^Newobj$' {
                $ctor = $instruction.Operand
                $arguments = @{}
                for ($i = $ctor.Parameters.Count - 1; $i -ge 0; $i--) { $arguments[$ctor.Parameters[$i].Name] = Pop-BoozeLiteral }
                if ($ctor.DeclaringType.FullName -eq 'LevelProperties/DicePalaceBooze/State') { $states.Add($arguments) }
                $stack.Add(@{ type=$ctor.DeclaringType.FullName; args=$arguments })
                break
            }
            '^Callvirt$' {
                for ($i=0; $i -lt $instruction.Operand.Parameters.Count; $i++) { $null=Pop-BoozeLiteral }
                $null=Pop-BoozeLiteral
                break
            }
            default { throw "Unhandled Tipsy Troop factory literal opcode: $($instruction.OpCode.Code)." }
        }
        if ($states.Count -eq 3) { break }
    }
    Require ($states.Count -eq 3) 'Tipsy Troop no longer has three reviewed difficulty configurations.'
    for ($i=0; $i -lt $states.Count; $i++) {
        $state = $states[$i]
        $expected = if ($i -eq 2) { 265 } else { 215 }
        Require ($state.decanter.args.decanterHP -eq $expected -and $state.martini.args.martiniHP -eq $expected -and
            $state.tumbler.args.tumblerHP -eq $expected) "Tipsy Troop difficulty $i HP totals changed."
    }
    Write-Output 'PASS Native Tipsy Troop contract: three owned HP pools, single immutable damage read, clamped timeline, three-death progression, 645/645/795 native HP and generic damage in the other Dice Palace bosses.'

    if ($ModPath) {
        $compiled = [Mono.Cecil.ModuleDefinition]::ReadModule((Resolve-ReferencePath $ModPath))
        try {
            $compiledTypes = @(All-Types $compiled.Types)
            $adapter = $compiledTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.CreatorToolsTapFarmingBoozeHealth'
            $bridge = $compiledTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.Plugin/CreatorToolsTapFarmingDamagePatch'
            $controller = $compiledTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.CreatorToolsTapFarmingController'
            Require ($null -ne $adapter -and $null -ne $bridge -and $null -ne $controller) 'Compiled Tipsy Troop adapter/bridge is missing.'
            foreach ($name in @('TryCapture','TryRead','OwnsActor','ContainsActor')) {
                Require (@($adapter.Methods | Where-Object Name -eq $name).Count -eq 1) "Compiled Tipsy Troop adapter lacks $name."
            }
            $enabled = $bridge.Fields | Where-Object Name -eq 'BoozeLocalDamageEnabled'
            $read = $bridge.Methods | Where-Object Name -eq 'ReadBoozeLocalDamage'
            $postfix = $bridge.Methods | Where-Object Name -eq 'BoozeDamagePostfix'
            Require ($null -ne $enabled -and $enabled.IsStatic -and $enabled.FieldType.FullName -eq 'System.Boolean' -and
                $read.ReturnType.FullName -eq 'System.Single' -and $read.Parameters.Count -eq 3 -and
                $read.Parameters[0].ParameterType.FullName -eq 'DamageDealer/DamageInfo' -and
                $read.Parameters[1].ParameterType.FullName -eq 'System.Object' -and $read.Parameters[2].ParameterType.FullName -eq 'System.Single') 'Compiled Tipsy Troop local-damage guard/helper shape changed.'
            Require (@(Calls $read 'get_damage').Count -eq 1 -and @(Calls $read 'PrepareTapFarmingBoozeDamage').Count -eq 1 -and
                @($read.Body.ExceptionHandlers | Where-Object HandlerType -eq 'Finally').Count -eq 1 -and
                @($read.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Stfld' -and $_.Operand.DeclaringType.FullName -eq 'DamageDealer/DamageInfo' }).Count -eq 0 -and
                @(Calls $postfix 'ObserveTapFarmingBoozeDamage').Count -eq 1) 'Compiled Tipsy Troop helper must account once, preserve DamageInfo and observe native HP/death afterwards.'
            $prepare = $controller.Methods | Where-Object Name -eq 'PrepareBoozeDamage'
            $observe = $controller.Methods | Where-Object Name -eq 'ObserveBoozeDamage'
            Require (@(Calls $prepare 'OwnsActor').Count -eq 1 -and @(Calls $prepare 'PrepareBossDamage').Count -eq 1 -and
                @(Calls $observe 'ContainsActor').Count -eq 1 -and @(Calls $observe 'ObserveBossDamage').Count -eq 1) 'Compiled Tipsy Troop callbacks must filter living owned actors and observe defeated owned actors.'
            $discovery = @($adapter.Methods | Where-Object HasBody | ForEach-Object {
                $_.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and
                    $_.Operand.Name -in @('FindObjectOfType','FindObjectsOfType','FindObjectsOfTypeAll','GetMode') }
            })
            Require ($discovery.Count -eq 0) 'Compiled Tipsy Troop adapter must only use owned actors and existing properties.'
            Write-Output 'PASS Compiled Tipsy Troop adapter/bridge: guarded local damage, one immutable read, owned live actor filtering and post-death HP observation.'
        }
        finally { $compiled.Dispose() }
    }
}
finally { $boozeModule.Dispose() }
