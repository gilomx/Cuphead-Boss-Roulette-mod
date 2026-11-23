#requires -Version 7.2
[CmdletBinding()]
param([string]$CupheadDir, [string]$CecilPath, [string]$ModPath)

# Inspect only metadata and IL. The board, every mini and the final fight
# cannot be identified safely by the DicePalaceMain level enum alone.
. (Join-Path $PSScriptRoot 'verify_native_tap_farming_contract.ps1') -CupheadDir $CupheadDir -CecilPath $CecilPath
$diceModule = [Mono.Cecil.ModuleDefinition]::ReadModule($nativePath)
try {
    $diceTypes = @(All-Types $diceModule.Types)
    function Dice-Type([string]$Name) { $diceTypes | Where-Object FullName -eq $Name }
    function Dice-Method([string]$Type, [string]$Name) {
        $methods = @((Dice-Type $Type).Methods | Where-Object Name -eq $Name)
        Require ($methods.Count -eq 1) "Native Dice lifecycle method changed: $Type.$Name."
        return $methods[0]
    }
    function Dice-Call($Method, [string]$Type, [string]$Name) {
        @(Calls $Method $Name | Where-Object { $_.Operand.DeclaringType.FullName -eq $Type })
    }

    $main = Dice-Type 'DicePalaceMainLevel'
    $kingField = $main.Fields | Where-Object Name -eq 'kingDice'
    Require ($main.BaseType.FullName -eq 'AbstractDicePalaceLevel' -and
        $null -ne $kingField -and !$kingField.IsStatic -and
        $kingField.FieldType.FullName -eq 'DicePalaceMainLevelKingDice') 'Native Dice final-fight owner changed.'
    $kingStart = Dice-Method 'DicePalaceMainLevelKingDice' 'Start'
    Require (@(Calls $kingStart 'set_enabled' | Where-Object {
        $_.Previous.OpCode.Code -eq 'Ldc_I4_0'
    }).Count -eq 2) 'Native Dice board must start with its boss damage receiver and collider disabled.'
    $battle = Dice-Method 'DicePalaceMainLevelKingDice' 'StartKingDiceBattle'
    $battleMarker = @(Calls $battle 'SetBool' | Where-Object {
        $_.Operand.DeclaringType.FullName -eq 'UnityEngine.Animator' -and
        $_.Previous.OpCode.Code -eq 'Ldc_I4_1' -and
        $_.Previous.Previous.OpCode.Code -eq 'Ldstr' -and
        $_.Previous.Previous.Operand -eq 'IsBattling'
    })
    Require ($battleMarker.Count -eq 1) 'Native Dice final fight no longer sets animator IsBattling=true.'
    $endBoard = Dice-Method 'DicePalaceMainLevelGameManager' 'EndBoardGame'
    Require (@(Dice-Call $endBoard 'DicePalaceMainLevelKingDice' 'StartKingDiceBattle').Count -eq 1 -and
        @(Dice-Call $endBoard 'DicePalaceMainLevelGameInfo' 'CleanUpRetry').Count -eq 1 -and
        @(Calls $endBoard 'LoadLevel').Count -eq 0 -and
        @(Calls $endBoard '_OnLevelStart').Count -eq 0 -and
        @(Calls $battle '_OnLevelStart').Count -eq 0) 'Dice final combat must begin inside the existing board Level without another level-start event.'
    $startBattle = Dice-Method 'Level/<startBattle_cr>c__Iterator2' 'MoveNext'
    $started = @(Dice-Call $startBattle 'Level' 'set_Started')
    $startedHook = @(Dice-Call $startBattle 'Level' '_OnLevelStart')
    Require ($started.Count -eq 1 -and $started[0].Previous.OpCode.Code -eq 'Ldc_I4_1' -and
        $startedHook.Count -eq 1 -and $started[0].Offset -lt $startedHook[0].Offset -and
        @(Calls $startBattle 'get_IsDicePalaceMain').Count -gt 0) 'Native Dice board level-start lifecycle changed; Started alone must not identify final combat.'

    $outbound = Dice-Method 'DicePalaceMainLevelGameManager/<start_mini_boss_cr>c__Iterator2' 'MoveNext'
    Require (@(Dice-Call $outbound 'DicePalaceMainLevelGameInfo' 'SetPlayersStats').Count -eq 1 -and
        @(Dice-Call $outbound 'SceneLoader' 'LoadLevel').Count -eq 1 -and
        @(Calls $outbound '_OnLevelEnd').Count -eq 0 -and
        @(Calls $outbound '_OnPreWin').Count -eq 0) 'Native Dice board-to-mini handoff changed.'
    foreach ($entry in @(
        @{ Type='DicePalaceMainLevel'; Base='AbstractDicePalaceLevel' },
        @{ Type='AbstractDicePalaceLevel'; Base='Level' }
    )) {
        $destroy = Dice-Method $entry.Type 'OnDestroy'
        Require (@(Dice-Call $destroy $entry.Base 'OnDestroy').Count -eq 1 -and
            @(Calls $destroy '_OnLevelEnd').Count -eq 0) "Native Dice scene destruction changed: $($entry.Type)."
    }
    $load = Dice-Method 'SceneLoader/<load_cr>c__Iterator1' 'MoveNext'
    $sceneLoads = @(Dice-Call $load 'UnityEngine.SceneManagement.SceneManager' 'LoadSceneAsync')
    Require ($sceneLoads.Count -eq 2 -and @($sceneLoads | Where-Object {
        $_.Operand.Parameters.Count -ne 1 -or
        $_.Operand.Parameters[0].ParameterType.FullName -ne 'System.String'
    }).Count -eq 0) 'Dice handoffs must replace the scene (Single), rather than retaining the prior Main or mini Level.'
    $currentDestroy = @(Dice-Call (Dice-Method 'Level' 'OnDestroy') 'Level' 'set_Current')
    $currentAwake = @(Dice-Call (Dice-Method 'Level' 'Awake') 'Level' 'set_Current')
    $wonAwake = @(Dice-Call (Dice-Method 'Level' 'Awake') 'Level' 'set_Won')
    Require ($currentDestroy.Count -eq 1 -and $currentDestroy[0].Previous.OpCode.Code -eq 'Ldnull' -and
        $currentAwake.Count -eq 1 -and $wonAwake.Count -eq 1 -and
        $wonAwake[0].Previous.OpCode.Code -eq 'Ldc_I4_0') 'Native Current/Won reset across Dice scene loads changed.'

    $win = Dice-Method 'Level' 'zHack_OnWin'
    $won = @(Dice-Call $win 'Level' 'set_Won')
    $ended = @(Dice-Call $win 'Level' '_OnLevelEnd')
    $prewin = @(Dice-Call $win 'Level' '_OnPreWin')
    Require ($won.Count -eq 1 -and $won[0].Previous.OpCode.Code -eq 'Ldc_I4_1' -and
        $ended.Count -eq 1 -and $prewin.Count -eq 1 -and
        $won[0].Offset -lt $ended[0].Offset -and $ended[0].Offset -lt $prewin[0].Offset) 'Native Dice win order changed: Won, level end, then pre-win.'
    $endCallers = @($diceTypes | ForEach-Object {
        foreach ($method in $_.Methods | Where-Object HasBody) {
            if (@(Dice-Call $method 'Level' '_OnLevelEnd').Count -gt 0) { $method.FullName }
        }
    } | Sort-Object)
    Require (($endCallers -join '|') -eq (@(
        'System.Void Level::_OnLose()', 'System.Void Level::zHack_OnWin()'
    ) -join '|')) 'Native level-end call sites changed; review board destruction, pause exits and retries.'
    $endWin = Dice-Method 'LevelEnd/<win_cr>c__Iterator0' 'MoveNext'
    $miniStats = @(Dice-Call $endWin 'DicePalaceMainLevelGameInfo' 'SetPlayersStats')
    $miniMainGuard = @(Dice-Call $endWin 'Level' 'get_IsDicePalaceMain')
    Require ($miniStats.Count -eq 1 -and $miniMainGuard.Count -eq 1 -and
        $miniMainGuard[0].Next.OpCode.Code -eq 'Brtrue' -and
        $miniMainGuard[0].Next.Operand.Offset -gt $miniStats[0].Offset -and
        @(Dice-Call $endWin 'SceneLoader' 'ReloadLevel').Count -eq 1) 'Native mini victory must preserve player stats and return through ReloadLevel.'

    $mainValue = ((Dice-Type 'DicePalaceLevels').Fields | Where-Object Name -eq 'DicePalaceMain').Constant
    $reload = Dice-Method 'SceneLoader' 'ReloadLevel'
    $reloadMain = @(Dice-Call $reload 'SceneLoader' 'LoadDicePalaceLevel')
    Require ($reloadMain.Count -eq 1 -and
        $reloadMain[0].Previous.OpCode.Code -eq 'Ldc_I4' -and
        $reloadMain[0].Previous.Operand -eq $mainValue -and
        @(Dice-Call $reload 'Level' 'get_IsDicePalace').Count -eq 1) 'Native Dice reload/retry must load Main, including when retrying a mini.'
    foreach ($entry in @(
        @{ Type='LevelPauseGUI'; Method='Restart'; Target='ReloadLevel'; CleanupFirst=$false },
        @{ Type='LevelGameOverGUI'; Method='Retry'; Target='ReloadLevel'; CleanupFirst=$true },
        @{ Type='LevelPauseGUI'; Method='Exit'; Target='LoadLastMap'; CleanupFirst=$true }
    )) {
        $method = Dice-Method $entry.Type $entry.Method
        $cleanup = @(Dice-Call $method 'DicePalaceMainLevelGameInfo' 'CleanUpRetry')
        $target = @(Dice-Call $method 'SceneLoader' $entry.Target)
        Require ($cleanup.Count -eq 1 -and $target.Count -eq 1 -and
            (($cleanup[0].Offset -lt $target[0].Offset) -eq $entry.CleanupFirst) -and
            @(Calls $method '_OnLevelEnd').Count -eq 0) "Native Dice reset/exit sequence changed: $($entry.Type).$($entry.Method)."
    }
    $infoAwake = Dice-Method 'DicePalaceMainLevelGameInfo' 'Awake'
    $cleanupRetry = Dice-Method 'DicePalaceMainLevelGameInfo' 'CleanUpRetry'
    Require (@(Calls $infoAwake 'DontDestroyOnLoad').Count -eq 1 -and
        @(Calls $cleanupRetry 'Destroy').Count -eq 0) 'Native Dice route state must persist across scene handoffs and reset without destruction on retry/final start.'
    Write-Output 'PASS Native Dice lifecycle: board/final IsBattling marker, Single scene handoffs, Won/end/pre-win order, new Main on mini victory/retry, and pause/map cleanup.'

    if ($ModPath) {
        $compiled = [Mono.Cecil.ModuleDefinition]::ReadModule((Resolve-ReferencePath $ModPath))
        try {
            $compiledTypes = @(All-Types $compiled.Types)
            $plugin = $compiledTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.Plugin'
            $controller = $compiledTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.CreatorToolsTapFarmingController'
            $interactions = $compiledTypes | Where-Object FullName -eq 'Gilomx.CupheadBossRoulette.CreatorToolsInteractionController'
            Require ($null -ne $plugin -and $null -ne $controller -and $null -ne $interactions) 'Compiled Dice lifecycle controller/bridge is missing.'
            $endBridge = $plugin.Methods | Where-Object Name -eq 'CreatorToolsInteractionLevelEndedPrefix'
            $destroyBridge = $plugin.Methods | Where-Object Name -eq 'CreatorToolsInteractionLevelDestroyedPrefix'
            $projectWin = @(Calls $endBridge 'PeskyBattleLevelPreWin')
            $projectEnd = @(Calls $endBridge 'PeskyBattleLevelEnded')
            Require (@(Calls $endBridge 'get_Won').Count -eq 1 -and
                $projectWin.Count -eq 1 -and $projectEnd.Count -eq 1 -and
                $projectWin[0].Offset -lt $projectEnd[0].Offset -and
                @(Calls $destroyBridge 'PeskyBattleLevelEnded').Count -eq 1 -and
                @(Calls $destroyBridge 'EndGameplayLevel').Count -eq 1) 'Compiled Dice bridge must project a native win before end, and report scene destruction without a native level-end event.'
            foreach ($name in @('OnLevelStarted', 'OnLevelEnded', 'OnMapEntered')) {
                Require (@($controller.Methods | Where-Object Name -eq $name).Count -eq 1) "Compiled Dice lifecycle entry point is missing: $name."
            }
            $observeMain = @($controller.Methods | Where-Object Name -eq 'ObserveDicePalaceMainState')
            Require ($observeMain.Count -eq 1 -and $observeMain[0].Parameters.Count -eq 2 -and
                $observeMain[0].Parameters[0].ParameterType.FullName -eq 'System.Int32' -and
                $observeMain[0].Parameters[1].ParameterType.FullName -eq 'System.Boolean') 'Compiled Dice native battle-state observer is missing.'
            $mainWrites = @($observeMain[0].Body.Instructions | Where-Object {
                $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'dicePalaceMainActive'
            })
            Require ($mainWrites.Count -eq 1 -and $mainWrites[0].Previous.OpCode.Code -eq 'Ldc_I4_1') 'Compiled Dice final state must activate only through its native battle-state observer.'
            $startController = $controller.Methods | Where-Object Name -eq 'OnLevelStarted'
            Require (@($startController.Body.Instructions | Where-Object {
                $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'dicePalaceMainActive' -and
                $_.Previous.OpCode.Code -ne 'Ldc_I4_0'
            }).Count -eq 0) 'Compiled Dice level-start must classify Main as board until IsBattling becomes true.'

            $notice = $interactions.Methods | Where-Object Name -eq 'RefreshTapFarmingBattleNoticeMainThread'
            $nativeBool = @(Calls $notice 'GetBool' | Where-Object {
                $_.Operand.DeclaringType.FullName -eq 'UnityEngine.Animator' -and
                $_.Previous.OpCode.Code -eq 'Ldstr' -and $_.Previous.Operand -eq 'IsBattling'
            })
            $observe = @(Calls $notice 'ObserveDicePalaceMainState')
            Require ($nativeBool.Count -eq 1 -and $observe.Count -eq 1 -and
                $nativeBool[0].Offset -lt $observe[0].Offset) 'Compiled Dice observer must read the native IsBattling animator flag.'
            # The observer receives the raw native flag, independent of players,
            # Started and the separate external battle-notice gating.
            function Dice-LocalKey($Instruction) {
                $code = [string]$Instruction.OpCode.Code
                if ($code -match '^(Ldloc|Stloc)_([0-3])$') { return [int]$Matches[2] }
                if ($code -in @('Ldloc', 'Ldloc_S', 'Stloc', 'Stloc_S')) { return [int]$Instruction.Operand.Index }
                return -1
            }
            $nativeStore = $nativeBool[0].Next
            if ($nativeStore.OpCode.Code -in @('Br', 'Br_S')) { $nativeStore = $nativeStore.Operand }
            $nativeLocal = Dice-LocalKey $nativeStore
            Require ($nativeLocal -ge 0 -and [string]$nativeStore.OpCode.Code -like 'Stloc*' -and
                [string]$observe[0].Previous.OpCode.Code -like 'Ldloc*' -and
                (Dice-LocalKey $observe[0].Previous) -eq $nativeLocal) 'Compiled Dice observer must receive the raw IsBattling value before battle-notice/player gating.'
            Write-Output 'PASS Compiled Dice lifecycle: raw native IsBattling observer, board/final classification, pre-win before end, destruction handoff and map reset.'
        }
        finally { $compiled.Dispose() }
    }
}
finally { $diceModule.Dispose() }
