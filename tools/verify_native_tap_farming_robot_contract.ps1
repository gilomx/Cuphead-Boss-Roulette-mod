#requires -Version 7.2
[CmdletBinding()]
param([string]$CupheadDir, [string]$CecilPath, [string]$ModPath)

# Reuse portable native-reference/Cecil detection and the unchanged global
# damage inventory. All inspection is read-only; no native code is executed.
. (Join-Path $PSScriptRoot 'verify_native_tap_farming_contract.ps1') -CupheadDir $CupheadDir -CecilPath $CecilPath
$robotModule = [Mono.Cecil.ModuleDefinition]::ReadModule($nativePath)
try {
    $robotTypes = @(All-Types $robotModule.Types)
    function Native-Type([string]$Name) { $robotTypes | Where-Object FullName -eq $Name }
    foreach ($entry in @(
        @{ Type='RobotLevel'; Field='properties'; Target='LevelProperties/Robot' },
        @{ Type='RobotLevel'; Field='robot'; Target='RobotLevelRobot' },
        @{ Type='RobotLevelRobot'; Field='head'; Target='RobotLevelRobotHead' },
        @{ Type='RobotLevelRobot'; Field='chest'; Target='RobotLevelRobotBodyPart' },
        @{ Type='RobotLevelRobot'; Field='hatch'; Target='RobotLevelRobotHatch' },
        @{ Type='RobotLevelRobotBodyPart'; Field='properties'; Target='LevelProperties/Robot' },
        @{ Type='RobotLevelRobotBodyPart'; Field='currentHealth'; Target='System.Single[]' }
    )) {
        $field = (Native-Type $entry.Type).Fields | Where-Object Name -eq $entry.Field
        Require ($null -ne $field -and !$field.IsStatic -and $field.FieldType.FullName -eq $entry.Target) "Robot owned-health metadata changed: $($entry.Type).$($entry.Field)."
    }
    foreach ($name in @('RobotLevelRobotBodyPart','RobotLevelRobotChest')) {
        $method = (Native-Type $name).Methods | Where-Object Name -eq 'OnDamageTaken'
        Require ($method.Parameters.Count -eq 1 -and $method.Parameters[0].ParameterType.FullName -eq 'DamageDealer/DamageInfo' -and
            $method.Body.Variables.Count -gt 0 -and $method.Body.Variables[0].VariableType.FullName -eq 'System.Single' -and
            $method.Body.ExceptionHandlers.Count -eq 0) "Robot local HP callback shape changed: $name."
        $getter = @(Calls $method 'get_damage')
        Require ($getter.Count -eq 1 -and $getter[0].Previous.OpCode.Code -eq 'Ldarg_1' -and
            $getter[0].Next.OpCode.Code -eq 'Sub' -and $getter[0].Next.Next.OpCode.Code -eq 'Stind_R4') "Robot local HP subtraction changed: $name."
        Require (@(Calls $method 'DealDamage' | Where-Object {
            $_.Operand.DeclaringType.Name -eq 'AbstractLevelProperties`3'
        }).Count -eq 0) "Robot local HP now also forwards generic damage: $name."
    }
    $head = Native-Type 'RobotLevelRobotHead'
    Require ($head.BaseType.FullName -eq 'RobotLevelRobotBodyPart' -and
        @($head.Methods | Where-Object Name -eq 'OnDamageTaken').Count -eq 0) 'Robot antenna no longer inherits the shared primary HP callback.'
    foreach ($name in @('RobotLevelRobotHatch','RobotLevelRobotChest')) {
        $method = (Native-Type $name).Methods | Where-Object Name -eq 'OnDamageTaken'
        Require (@(Calls $method 'OnDamageTaken' | Where-Object {
            $_.Operand.DeclaringType.FullName -eq 'RobotLevelRobotBodyPart'
        }).Count -eq 1) "Robot primary HP callback routing changed: $name."
    }
    $intro = (Native-Type 'RobotLevelRobot').Methods | Where-Object Name -eq 'IntroEnded'
    Require (@(Calls $intro 'InitBodyPart').Count -eq 3) 'Robot local HP is no longer initialized for all three owned parts at intro completion.'
    $start = (Native-Type 'RobotLevel').Methods | Where-Object Name -eq 'Start'
    foreach ($pool in @('health','chestHP','hatchGateHealth','heartHP')) {
        Require (@($start.Body.Instructions | Where-Object {
            $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq $pool -and
            $_.Operand.DeclaringType.FullName -like 'LevelProperties/Robot/*'
        }).Count -eq 1) "Robot timeline's original local HP pool changed: $pool."
    }

    # Interpret only constructor literals in the factory IL, never GetMode
    # itself. Nested constructor arguments retain their native parameter names.
    $factory = (Native-Type 'LevelProperties/Robot').Methods | Where-Object Name -eq 'GetMode'
    $stack = [Collections.Generic.List[object]]::new()
    $locals = @{}
    $factoryStates = [Collections.Generic.List[object]]::new()
    function Pop-RobotLiteral {
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
            '^Stloc_([0-2])$' { $locals[[int]$Matches[1]] = Pop-RobotLiteral; break }
            '^Ldloc_([0-2])$' { $stack.Add($locals[[int]$Matches[1]]); break }
            '^Dup$' { $stack.Add($stack[$stack.Count - 1]); break }
            '^Newarr$' { $null = Pop-RobotLiteral; $stack.Add(@{ kind='array' }); break }
            '^Stelem_Ref$' { $null=Pop-RobotLiteral; $null=Pop-RobotLiteral; $null=Pop-RobotLiteral; break }
            '^Brfalse$' { $null=Pop-RobotLiteral; break }
            '^Beq$' { $null=Pop-RobotLiteral; $null=Pop-RobotLiteral; break }
            '^Br$' { break }
            '^Newobj$' {
                $ctor=$instruction.Operand
                $arguments=@{}
                for ($i=$ctor.Parameters.Count-1; $i -ge 0; $i--) {
                    $arguments[$ctor.Parameters[$i].Name] = Pop-RobotLiteral
                }
                if ($ctor.DeclaringType.FullName -eq 'LevelProperties/Robot/State') {
                    $factoryStates.Add(@{ total=$locals[0]; args=$arguments })
                }
                $stack.Add(@{ type=$ctor.DeclaringType.FullName; args=$arguments })
                break
            }
            '^Callvirt$' {
                for ($i=0; $i -lt $instruction.Operand.Parameters.Count; $i++) { $null=Pop-RobotLiteral }
                $null=Pop-RobotLiteral
                if ($instruction.Operand.ReturnType.FullName -ne 'System.Void') { $stack.Add(@{ kind='return' }) }
                break
            }
            '^Ret$' { break }
            default { throw "Robot factory IL needs a new read-only audit: $instruction" }
        }
    }
    foreach ($expected in @(
        @{ Generic=500; Count=1; Main=1250; Total=1250; Flying=0; Inventor=0 },
        @{ Generic=1400; Count=3; Main=675; Total=1935; Flying=140; Inventor=1120 },
        @{ Generic=1800; Count=3; Main=840; Total=2460; Flying=180; Inventor=1440 }
    )) {
        $modeStates = @($factoryStates | Where-Object { $_.total -eq $expected.Generic })
        Require ($modeStates.Count -eq $expected.Count -and $modeStates[0].args.stateName -eq 0) 'Robot original phase count/name changed.'
        $main = $modeStates[0].args
        $local = $main.hose.args.health + $main.orb.args.chestHP + $main.shotBot.args.hatchGateHealth + $main.heart.args.heartHP
        Require ($local -eq $expected.Main) 'Robot original local phase HP changed.'
        $flying=0d; $inventor=0d
        if ($modeStates.Count -eq 3) {
            Require ($modeStates[1].args.stateName -eq 2 -and $modeStates[2].args.stateName -eq 3) 'Robot flying/inventor phase order changed.'
            $flying = $expected.Generic * ([double]$modeStates[1].args.healthTrigger - [double]$modeStates[2].args.healthTrigger)
            $inventor = $expected.Generic * [double]$modeStates[2].args.healthTrigger
        }
        Require ([Math]::Abs($flying-$expected.Flying) -lt .001 -and
            [Math]::Abs($inventor-$expected.Inventor) -lt .001 -and
            [Math]::Abs($local+$flying+$inventor-$expected.Total) -lt .001) 'Robot original native phase health changed.'
    }
    Write-Output 'PASS Robot native contract: owned local HP, exact primary/heart reads, initialization/routing and original Simple/Normal/Expert totals 1250/1935/2460.'
}
finally { $robotModule.Dispose() }

if ($ModPath) {
    $mod = [Mono.Cecil.ModuleDefinition]::ReadModule($ModPath)
    try {
        $modTypes = @(All-Types $mod.Types)
        $adapter = $modTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.CreatorToolsTapFarmingRobotHealth'
        $bridge = $modTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.Plugin/CreatorToolsTapFarmingDamagePatch'
        Require ($null -ne $adapter -and $null -ne $bridge) 'Compiled Robot health adapter/bridge is missing.'
        foreach ($method in $adapter.Methods) {
            if (!$method.HasBody) { continue }
            Require (@($method.Body.Instructions | Where-Object {
                $_.Operand -is [Mono.Cecil.MethodReference] -and
                $_.Operand.Name -in @('GetMode','FindObjectsOfType','FindObjectsOfTypeAll')
            }).Count -eq 0) 'Robot adapter constructs properties or scans global actors.'
        }
        $read = $bridge.Methods | Where-Object Name -eq 'ReadRobotLocalDamage'
        Require (@(Calls $read 'PrepareTapFarmingBossDamage').Count -eq 1 -and
            @(Calls $read 'set_damage').Count -eq 0 -and
            @($read.Body.ExceptionHandlers | Where-Object HandlerType -eq 'Finally').Count -eq 1) 'Robot local absorption bypasses the shared reserve path, mutates shared damage or leaks its guard.'
        Require (@($bridge.Methods | Where-Object Name -eq 'RobotLocalDamageTranspiler').Count -eq 1) 'Robot exact local subtraction transpiler is missing.'
        Write-Output 'PASS Compiled Robot adapter/bridge: no factories/global discovery, shared damage accounting, unchanged DamageInfo and exception cleanup.'
    }
    finally { $mod.Dispose() }
}
