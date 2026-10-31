#requires -Version 7.0
param(
    [string]$CupheadDir = 'C:\Program Files (x86)\Steam\steamapps\common\Cuphead',
    [string]$ModPath = (Join-Path $PSScriptRoot '..\bin\Release\net35\Gilomx.CupheadBossRoulette.dll'),
    [string]$CecilPath = "$env:USERPROFILE\.nuget\packages\mono.cecil\0.10.4\lib\net40\Mono.Cecil.dll"
)

# Read native and compiled mod IL. Verify loading order without launching Unity
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
function SelfAndNestedTypes($type) {
    $type
    foreach ($nested in $type.NestedTypes) { SelfAndNestedTypes $nested }
}
try {
    $loader = $module.Types | Where-Object Name -eq 'SceneLoader'
    $factory = $loader.Methods | Where-Object Name -eq 'load_cr'
    Require ($factory.ReturnType.FullName -eq 'System.Collections.IEnumerator') 'load_cr return type changed'
    Require (@(Calls $factory 'UnityEngine.SceneManagement.SceneManager' 'LoadSceneAsync').Count -eq 0) 'Iterator factory now starts native I/O eagerly'
    $done = $loader.Fields | Where-Object Name -eq 'doneLoadingSceneAsync'
    Require ($null -ne $done -and -not $done.IsStatic -and $done.FieldType.FullName -eq 'System.Boolean') 'Native completion flag changed'
    $icon = $loader.Fields | Where-Object Name -eq 'icon'
    Require ($null -ne $icon -and -not $icon.IsStatic -and $icon.FieldType.FullName -eq 'UnityEngine.UI.Image') 'Native clock image changed; the preparation notice must attach to the real loading icon'

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
    $assetWait = @(Calls $loadMove 'AssetBundleLoader' 'get_loadCounter')
    Require ($assetWait.Count -eq 1 -and $assetWait[0].Next.OpCode.Code -eq 'Ldc_I4_0') 'Native asset request counter wait changed'
    Require ($assetWait[0].Next.Next.OpCode.Code -in @('Bgt', 'Bgt_S')) 'Native loader no longer waits while assets are pending'
    $sceneWait = @(Calls $loadMove 'UnityEngine.AsyncOperation' 'get_isDone')[-1]
    Require ($sceneWait.Next.Operand.Offset -eq $assetWait[0].Next.Next.Operand.Offset) 'Native scene/asset waits no longer share a completion loop'

    # Transition.None still covers the old scene, rather than bypassing the
    # loading screen on retries or programmatic native door transitions.
    $fadeMove = ($loader.NestedTypes | Where-Object Name -like '<in_cr>*').Methods | Where-Object Name -eq 'MoveNext'
    $cover = @(Calls $fadeMove 'SceneLoader' 'SetFaderAlpha')
    Require ($cover.Count -eq 1 -and $cover[0].Previous.Operand -eq 1) 'Immediate transition no longer covers the scene'

    $veggies = $module.Types | Where-Object Name -eq 'VeggiesLevel'
    $prefabsField = $veggies.Fields | Where-Object Name -eq 'prefabs'
    Require ($null -ne $prefabsField) 'Root Pack level prefab table changed'
    $prefabsType = $prefabsField.FieldType.Resolve()
    $carrotField = $prefabsType.Fields | Where-Object Name -eq 'carrot'
    Require ($null -ne $carrotField -and $carrotField.FieldType.FullName -eq 'VeggiesLevelCarrot') 'Root Pack carrot prefab entry changed'
    $carrotType = $module.Types | Where-Object Name -eq 'VeggiesLevelCarrot'
    $homingField = $carrotType.Fields | Where-Object Name -eq 'homingPrefab'
    Require ($null -ne $homingField -and $homingField.FieldType.FullName -eq 'VeggiesLevelCarrotHomingProjectile') 'Root Pack homing projectile prefab entry changed'
    $homingType = $module.Types | Where-Object Name -eq 'VeggiesLevelCarrotHomingProjectile'
    $homingCreate = @($homingType.Methods | Where-Object {
        $_.Name -eq 'Create' -and $_.Parameters.Count -in @(6, 7)
    })
    Require ($homingCreate.Count -ge 1) 'Root Pack homing projectile Create signature changed'
    Write-Output 'Native loading contract passed: fade completes before lazy scene loading, completion is awaited, and immediate transitions stay covered.'

    $level = $module.Types | Where-Object Name -eq 'Level'
    $battleStart = ($level.NestedTypes | Where-Object Name -like '<startBattle_cr>*').Methods | Where-Object Name -eq 'MoveNext'
    $readyComplete = @($battleStart.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq 'readyComplete'
    })
    $started = @(Calls $battleStart 'Level' 'set_Started')
    $levelStart = @(Calls $battleStart 'Level' '_OnLevelStart')
    Require ($readyComplete.Count -eq 1 -and $started.Count -eq 1 -and $levelStart.Count -eq 1 -and
        $readyComplete[0].Next.OpCode.Code -in @('Brfalse', 'Brfalse_S') -and
        $readyComplete[0].Offset -lt $started[0].Offset -and
        $started[0].Previous.OpCode.Code -eq 'Ldc_I4_1' -and
        $started[0].Offset -lt $levelStart[0].Offset) 'Native Started must become true after the ready animation and before the gameplay start callback'
    $startedGetter = $level.Methods | Where-Object Name -eq 'get_Started'
    Require (@($startedGetter.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq '<Started>k__BackingField'
    }).Count -eq 1) 'Native Started no longer exposes the native gameplay flag'
    Write-Output 'Native interaction start contract passed: Started follows the ready animation and precedes the gameplay start hook.'

    $levelGui = $module.Types | Where-Object Name -eq 'LevelGUI'
    $currentLevelGui = $levelGui.Methods | Where-Object Name -eq 'get_Current'
    $nativePause = $levelGui.Fields | Where-Object Name -eq 'pause'
    Require ($currentLevelGui.IsPublic -and $currentLevelGui.IsStatic -and
        $currentLevelGui.ReturnType.FullName -eq 'LevelGUI') 'Native LevelGUI must register its current owner publicly'
    Require ($null -ne $nativePause -and !$nativePause.IsStatic -and
        $nativePause.FieldType.FullName -eq 'LevelPauseGUI' -and
        @($nativePause.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'UnityEngine.SerializeField' }).Count -eq 1) 'Native LevelGUI must retain its serialized pause menu'
    foreach ($methodName in @('Awake', 'OnDestroy')) {
        $method = $levelGui.Methods | Where-Object Name -eq $methodName
        $register = @(Calls $method 'LevelGUI' 'set_Current')
        $expectedLoad = if ($methodName -eq 'Awake') { 'Ldarg_0' } else { 'Ldnull' }
        Require ($register.Count -eq 1 -and $register[0].Previous.OpCode.Code -eq $expectedLoad) "Native LevelGUI owner registration changed in $methodName"
    }
    $guiDestroyed = $levelGui.Methods | Where-Object Name -eq 'OnDestroy'
    Require (@($guiDestroyed.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Stfld' -and $_.Operand.FullName -eq $nativePause.FullName -and
        $_.Previous.OpCode.Code -eq 'Ldnull'
    }).Count -eq 1) 'Native LevelGUI must clear its serialized pause reference on destruction'
    Write-Output 'Native pause owner contract passed: LevelGUI.Current owns a serialized menu and clears native references on destruction.'

    $gameOver = $module.Types | Where-Object Name -eq 'LevelGameOverGUI'
    $currentGameOver = $gameOver.Fields | Where-Object Name -eq 'Current'
    Require ($null -ne $currentGameOver -and $currentGameOver.IsPublic -and
        $currentGameOver.IsStatic -and $currentGameOver.FieldType.FullName -eq 'LevelGameOverGUI') 'Native defeat GUI must expose its current instance'
    foreach ($methodName in @('Awake', 'OnDestroy')) {
        $method = $gameOver.Methods | Where-Object Name -eq $methodName
        $stores = @($method.Body.Instructions | Where-Object {
            $_.OpCode.Code -eq 'Stsfld' -and $_.Operand.FullName -eq $currentGameOver.FullName
        })
        $expectedLoad = if ($methodName -eq 'Awake') { 'Ldarg_0' } else { 'Ldnull' }
        Require ($stores.Count -eq 1 -and $stores[0].Previous.OpCode.Code -eq $expectedLoad) "Native defeat GUI no longer maintains Current in $methodName"
    }
    $players = ($module.Types | Where-Object Name -eq 'PlayerManager').Methods | Where-Object Name -eq 'GetAllPlayers'
    Require ($players.ReturnType.FullName.StartsWith('System.Collections.Generic.Dictionary`2/ValueCollection', [StringComparison]::Ordinal) -and
        @($players.Body.Instructions | Where-Object OpCode -Match 'newobj|newarr').Count -eq 0 -and
        @($players.Body.Instructions | Where-Object {
            $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'get_Values'
        }).Count -eq 1) 'Native player lookup must expose its live dictionary values without materializing an array'
    Write-Output 'Native HUD and player registry contract passed: live instances replace scene discovery and retain native lifetime handling.'

    $runtimeCore = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $CupheadDir 'Cuphead_Data/Managed/mscorlib.dll'))
    try {
        $nativeBuilder = $runtimeCore.GetType('System.Text.StringBuilder')
        Require (@($nativeBuilder.Methods | Where-Object {
            $_.IsPublic -and $_.FullName -eq 'System.Void System.Text.StringBuilder::CopyTo(System.Int32,System.Char[],System.Int32,System.Int32)'
        }).Count -eq 1) 'Reusable JSON snapshots require the native StringBuilder.CopyTo overload'
    }
    finally { $runtimeCore.Dispose() }

    # Catalog zeppelins share prepared settings by difficulty. Their native
    # methods may read the selected state but must never damage, advance or
    # modify that shared configuration, including in generated coroutines.
    $zeppelinEnemy = $module.Types | Where-Object Name -eq 'FlyingBlimpLevelEnemy'
    $zeppelinConfigCalls = @(foreach ($type in (SelfAndNestedTypes $zeppelinEnemy)) {
        foreach ($method in ($type.Methods | Where-Object HasBody)) {
            foreach ($instruction in $method.Body.Instructions) {
                $operand = $instruction.Operand
                if ($operand -is [Mono.Cecil.MethodReference] -and
                    $operand.DeclaringType.Name -eq 'AbstractLevelProperties`3') {
                    Require ($operand.Name -eq 'get_CurrentState') "Native zeppelin mutates shared boss properties in $($method.FullName)"
                    $instruction
                }
                if ($instruction.OpCode.Code -eq 'Stfld' -and
                    $operand -is [Mono.Cecil.FieldReference] -and
                    $operand.DeclaringType.FullName -like 'LevelProperties/FlyingBlimp*') {
                    throw "Native zeppelin writes shared configuration in $($method.FullName)"
                }
            }
        }
    })
    Require ($zeppelinConfigCalls.Count -eq 2) 'Native zeppelin configuration access changed; review sharing prepared settings'
    Write-Output 'Native zeppelin settings contract passed: actor methods and coroutines only read the prepared configuration.'
}
finally {
    $module.Dispose()
}

$mod = [Mono.Cecil.ModuleDefinition]::ReadModule($ModPath)
try {
    $coordinator = $mod.Types | Where-Object Name -eq 'NativeInteractionPreloadCoordinator'
    $nativePending = $coordinator.Methods | Where-Object Name -eq 'get_HasPendingNativeAssetLoads'
    $counter = @(Calls $nativePending 'AssetBundleLoader' 'get_loadCounter')
    Require ($counter.Count -eq 1 -and $counter[0].Next.OpCode.Code -eq 'Ldc_I4_0' -and
        $counter[0].Next.Next.OpCode.Code -eq 'Cgt') 'Mod must use the native pending asset counter'
    $busy = $coordinator.Methods | Where-Object Name -eq 'get_IsBusy'
    Require (@(Calls $busy $coordinator.FullName 'get_HasPendingNativeAssetLoads').Count -eq 1) 'Loading barrier does not drain native assets'
    $cacheCount = 0
    foreach ($cache in $mod.Types | Where-Object Name -like 'Native*Cache') {
        $iterator = $cache.NestedTypes | Where-Object Name -like '<PreloadNativeAssetsCore>*'
        if ($null -eq $iterator) { continue }
        $move = $iterator.Methods | Where-Object Name -eq 'MoveNext'
        $pending = @(Calls $move $coordinator.FullName 'get_HasPendingNativeAssetLoads')
        $unload = @(Calls $move 'UnityEngine.SceneManagement.SceneManager' 'UnloadSceneAsync')
        $activation = @(Calls $move 'UnityEngine.AsyncOperation' 'get_isDone')[0]
        Require ($pending.Count -eq 1 -and $unload.Count -eq 1) "Missing asset wait in $($cache.Name)"
        Require ($activation.Offset -lt $pending[0].Offset -and $pending[0].Offset -lt $unload[0].Offset) "Source scene unload races assets in $($cache.Name)"
        Require ($activation.Next.Operand.Offset -eq $pending[0].Next.Operand.Offset) "Scene and asset requests must both finish in $($cache.Name)"
        Require ($pending[0].Next.OpCode.Code -in @('Brtrue', 'Brtrue_S')) "Pending assets no longer yield in $($cache.Name)"
        $cacheCount++
    }
    Require ($cacheCount -eq 10) 'Expected all ten native source scene caches'
    Write-Output 'Compiled catalog contract passed: all ten caches await native resources before unloading; the final loading barrier also drains asset requests.'

    $carrotCache = $mod.Types | Where-Object Name -eq 'NativeHomingCarrotCache'
    $captureCarrot = $carrotCache.Methods | Where-Object Name -eq 'CaptureTemplateFromLevel'
    $carrotFields = @($captureCarrot.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldsfld' -and
        $_.Operand -is [Mono.Cecil.FieldReference] -and
        $_.Operand.Name -in @('VeggiesPrefabsField', 'CarrotPrefabField')
    } | ForEach-Object { $_.Operand.Name } | Sort-Object -Unique)
    Require ($carrotFields.Count -eq 2) 'Homing carrot preload must read the serialized Root Pack prefab table'
    $createNative = $carrotCache.Methods | Where-Object Name -eq 'CreateNativeProjectile'
    $reflectionInvoke = @($createNative.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.Name -eq 'Invoke' -and
        $_.Operand.DeclaringType.FullName -in @('System.Reflection.MethodBase', 'System.Reflection.MethodInfo')
    })
    Require ($reflectionInvoke.Count -eq 1) 'Homing carrot spawn must support both native Create signatures'
    Write-Output 'Compiled homing carrot contract passed: preload reaches the serialized carrot prefab before boss lifecycle runs.'

    $plugin = $mod.Types | Where-Object Name -eq 'Plugin'
    $pauseQuery = $plugin.Methods | Where-Object Name -eq 'TryGetActiveLevelPauseMenu'
    Require (@(Calls $pauseQuery 'LevelGUI' 'get_Current').Count -eq 1 -and
        @($pauseQuery.Body.Instructions | Where-Object {
            $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'TryGetActive' -and
            $_.Operand.DeclaringType.Name -eq 'CreatorToolsNativePauseMenuCache`2'
        }).Count -eq 1) 'Pause queries must use the native registered GUI and owner-reference cache'
    Require (@($plugin.Methods | Where-Object Name -eq 'DiscoverNativePauseMenus').Count -eq 0) 'Pause queries must not retain global candidate discovery'
    $pauseResolve = $plugin.Methods | Where-Object Name -eq 'ResolveNativePauseMenu'
    Require (@(Calls $pauseResolve 'System.Reflection.FieldInfo' 'GetValue').Count -eq 1 -and
        @($pauseResolve.Body.Instructions | Where-Object {
            $_.OpCode.Code -eq 'Ldsfld' -and $_.Operand.Name -eq 'NativePauseMenuField'
        }).Count -ge 1) 'Native pause field must use its cached reflection metadata only when the owner/menu changes'
    foreach ($callback in @('OnCreatorToolsInteractionPaused', 'OnCreatorToolsInteractionUnpaused')) {
        $method = $plugin.Methods | Where-Object Name -eq $callback
        Require (@(Calls $method $plugin.FullName 'InvalidateNativePauseMenuCandidates').Count -eq 1) "Native pause reference cache must invalidate immediately in $callback"
    }
    Write-Output 'Compiled native pause owner contract passed: current GUI lookup, warm reference reuse and immediate pause/unpause invalidation.'
    $gameOverLayer = $plugin.Methods | Where-Object Name -eq 'FindActiveGameOverHudLayer'
    Require (@($gameOverLayer.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldsfld' -and $_.Operand.FullName -eq 'LevelGameOverGUI LevelGameOverGUI::Current'
    }).Count -eq 1 -and @($gameOverLayer.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -like 'FindObjectsOfType*'
    }).Count -eq 0) 'Defeat HUD must use the native current GUI without a global scene scan'
    $positions = $mod.Types | Where-Object Name -eq 'CreatorToolsInteractionSpawnPositions'
    $appendPositions = $positions.Methods | Where-Object Name -eq 'AppendTo'
    Require (@(Calls $appendPositions 'PlayerManager' 'GetAllPlayers').Count -eq 1) 'Spawn reservation positions must read native registered players'
    foreach ($executorName in @('HomingCarrot', 'CagneyHomingPlant', 'FrogsFirefly', 'RobotHomingBomb', 'TrainBoneRing', 'BeppiBalloonDog')) {
        $executor = $mod.Types | Where-Object Name -eq ($executorName + 'InteractionExecutor')
        $reserved = $executor.Methods | Where-Object Name -eq 'ReservedPositions'
        Require (@(Calls $reserved $positions.FullName 'AppendTo').Count -eq 1 -and
            @($reserved.Body.Instructions | Where-Object {
                $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -like 'FindObjectsOfType*'
            }).Count -eq 0) "Spawn reservations still scan the entire scene in $($executor.Name)"
    }
    Write-Output 'Compiled HUD and spawn discovery contract passed: native defeat GUI and registered actors/players are wired into all six spawn planners.'
    $snapshot = $mod.Types | Where-Object Name -eq 'CreatorToolsJsonSnapshot'
    $completeSnapshot = $snapshot.Methods | Where-Object Name -eq 'Complete'
    Require (@(Calls $completeSnapshot 'System.Text.StringBuilder' 'CopyTo').Count -eq 1 -and
        @(Calls $completeSnapshot 'System.Text.StringBuilder' 'ToString').Count -eq 0) 'Reusable JSON snapshots must copy their output without sharing Mono mutable builder storage'
    $interactionController = $mod.Types | Where-Object Name -eq 'CreatorToolsInteractionController'
    foreach ($projection in @('BuildInteractionState', 'BuildPeskyState')) {
        $method = $interactionController.Methods | Where-Object Name -eq $projection
        Require (@(Calls $method $snapshot.FullName 'Begin').Count -eq 1 -and
            @(Calls $method $snapshot.FullName 'Complete').Count -eq 1 -and
            @(Calls $method 'System.Text.StringBuilder' 'ToString').Count -eq 0) "Panel projection must reuse its immutable snapshot safely: $projection"
    }
    Write-Output 'Compiled JSON reuse contract passed: both panel projections preserve immutable output without Mono builder copy-on-write.'
    foreach ($projection in @(
        @{ Method = 'PublishInteractionState'; Gate = 'TryBeginInteractionStatePublication'; Build = 'BuildInteractionState' },
        @{ Method = 'PublishPeskyState'; Gate = 'TryBeginPeskyStatePublication'; Build = 'BuildPeskyState' }
    )) {
        $method = $interactionController.Methods | Where-Object { $_.Name -eq $projection.Method -and $_.Parameters.Count -eq 3 }
        $gate = @(Calls $method 'Gilomx.CupheadBossRoulette.CreatorToolsServer' $projection.Gate)
        $build = @(Calls $method $interactionController.FullName $projection.Build)
        Require ($gate.Count -eq 1 -and $build.Count -eq 1 -and
            $gate[0].Offset -lt $build[0].Offset -and $gate[0].Previous.OpCode.Code -eq 'Ldarg_3' -and
            $gate[0].Next.OpCode.Code -in @('Brtrue', 'Brtrue_S') -and
            $gate[0].Next.Next.OpCode.Code -eq 'Ret') "Audience gate must precede only heavy JSON, preserving urgent control publication: $($projection.Method)"
    }
    $refreshPanel = $interactionController.Methods | Where-Object Name -eq 'RefreshPanelState'
    foreach ($publishName in @('PublishInteractionState', 'PublishPeskyState')) {
        $forced = @(Calls $refreshPanel $interactionController.FullName $publishName)
        Require ($forced.Count -eq 1 -and $forced[0].Previous.OpCode.Code -eq 'Ldc_I4_1') 'Focus snapshots must force both panel publications without an audience lease'
    }
    $focusPanel = $plugin.Methods | Where-Object Name -eq 'OnApplicationFocus'
    Require (@(Calls $focusPanel $interactionController.FullName 'RefreshPanelState').Count -eq 1) 'Leaving gameplay must capture panel state before Unity can stop background updates'
    $updateInteractions = $interactionController.Methods | Where-Object Name -eq 'UpdateCore'
    Require (@(Calls $updateInteractions $interactionController.FullName 'ProcessReadyQueues').Count -eq 1 -and
        @(Calls $updateInteractions $interactionController.FullName 'UpdatePeskyChallenges').Count -eq 1) 'Gameplay dispatch must remain independent of UI publication demand'
    Write-Output 'Compiled panel audience contract passed: heavy JSON is demand-paced, urgent ACKs bypass pacing, focus captures state, and gameplay continues independently.'
    $dashboardController = $mod.Types | Where-Object Name -eq 'CreatorToolsDashboardController'
    $dashboardUpdate = $dashboardController.Methods | Where-Object Name -eq 'Update'
    $dueSimulations = @(Calls $dashboardUpdate $dashboardController.FullName 'ProcessDueSimulations')
    $dashboardGate = @(Calls $dashboardUpdate 'Gilomx.CupheadBossRoulette.CreatorToolsServer' 'get_DashboardStateIsOnDemand')
    $dashboardBuild = @(Calls $dashboardUpdate $dashboardController.FullName 'BuildState')
    Require ($dueSimulations.Count -eq 1 -and $dashboardGate.Count -eq 1 -and $dashboardBuild.Count -eq 1 -and
        $dueSimulations[0].Offset -lt $dashboardGate[0].Offset -and
        $dashboardGate[0].Offset -lt $dashboardBuild[0].Offset) 'On-demand dashboard JSON must not suspend scheduled event processing'
    $dashboardReader = $dashboardController.Methods | Where-Object Name -eq 'GetStateJson'
    Require (@(Calls $dashboardReader 'System.Threading.Monitor' 'Enter').Count -eq 1 -and
        @(Calls $dashboardReader $dashboardController.FullName 'BuildState').Count -eq 1 -and
        @(Calls $dashboardReader $dashboardController.FullName 'ProcessDueSimulations').Count -eq 0) 'Dashboard readers must synchronize the managed model without executing simulations'
    foreach ($method in @($dashboardReader, ($dashboardController.Methods | Where-Object Name -eq 'BuildState'))) {
        Require (@($method.Body.Instructions | Where-Object {
            $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -like 'UnityEngine.*'
        }).Count -eq 0) 'HTTP dashboard projections must not access Unity'
    }
    $serverSetup = $plugin.Methods | Where-Object Name -eq 'StartCreatorToolsServer'
    Require (@(Calls $serverSetup 'Gilomx.CupheadBossRoulette.CreatorToolsServer' 'SetDashboardStateReader').Count -eq 1 -and
        @(Calls $serverSetup $dashboardController.FullName 'GetStateJson').Count -eq 1) 'Runtime setup must register the synchronized dashboard reader'
    Write-Output 'Compiled dashboard demand contract passed: direct synchronized reads, no Unity access, no eager production JSON, and independent simulation processing.'
    $canSpawn = $plugin.Methods | Where-Object Name -eq 'CanSpawnCreatorToolsInteraction'
    $nativeStarted = @(Calls $canSpawn 'Level' 'get_Started')
    Require ($nativeStarted.Count -eq 1 -and
        $nativeStarted[0].Next.OpCode.Code -in @('Brtrue', 'Brtrue_S') -and
        $nativeStarted[0].Next.Next.OpCode.Code -eq 'Ldc_I4_0' -and
        $nativeStarted[0].Next.Next.Next.OpCode.Code -eq 'Ret') 'Interaction dispatch must return false while the native intro has not started gameplay'
    Write-Output 'Compiled interaction start contract passed: no actor dispatch before the native Started flag.'
    $zeppelinPattern = $mod.Types | Where-Object Name -eq 'NativeZeppelinSpawnPattern'
    $spawnPlan = $zeppelinPattern.Methods | Where-Object Name -eq 'TryCreate'
    Require (@($spawnPlan.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.Name -eq 'GetMode'
    }).Count -eq 0) 'Zeppelin spawn plans must not rebuild a full boss configuration'
    Require (@($spawnPlan.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.Name -eq 'NativeInteractionDifficultyCache`1' -and
        $_.Operand.Name -eq 'Get'
    }).Count -eq 1) 'Zeppelin spawn plans must reuse the prepared difficulty configuration'
    $zeppelinCache = $mod.Types | Where-Object Name -eq 'NativeZeppelinCache'
    $zeppelinUpdate = $zeppelinCache.Methods | Where-Object Name -eq 'Update'
    $configurationWarmup = @(Calls $zeppelinUpdate $zeppelinPattern.FullName 'PrepareProperties')
    $acquire = @(Calls $zeppelinUpdate $coordinator.FullName 'TryAcquire')
    Require ($configurationWarmup.Count -eq 1 -and $acquire.Count -eq 1 -and
        $acquire[0].Offset -lt $configurationWarmup[0].Offset) 'Zeppelin configurations must be prepared inside the covered preload window'
    Write-Output 'Compiled zeppelin settings contract passed: three difficulty configurations are prepared during loading and reused for spawns/retries.'
    # The live event must hand its result to the existing roulette and wait
    # for the native Play action; no timer/Update callback may start a load.
    $chatType = $mod.Types | Where-Object Name -eq 'CreatorToolsChatChoosesController'
    $chatLoadCallers = @(foreach ($type in (SelfAndNestedTypes $plugin)) {
        foreach ($method in ($type.Methods | Where-Object HasBody)) {
            if (@(Calls $method $chatType.FullName 'TryBeginLoad').Count -gt 0) { $method.Name }
        }
    })
    Require ($chatLoadCallers.Count -eq 1 -and $chatLoadCallers[0] -eq 'BeginResultLoad') 'Chat selection must load only from the roulette Play action'
    Require (@($plugin.Methods | Where-Object Name -eq 'DrawChatChoosesResult').Count -eq 0) 'The separate chat result card must be removed'
    $chatUpdate = $plugin.Methods | Where-Object Name -like '<UpdateChatChooses>*'
    Require (@($chatUpdate | ForEach-Object { Calls $_ $plugin.FullName 'LoadResult' }).Count -eq 0) 'Chat Update must never start gameplay'
    Require (@($chatUpdate | ForEach-Object { $_.Body.Instructions } | Where-Object {
        $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'difficulty'
    }).Count -eq 0) 'Chat result must keep the player last difficulty'
    $checklist = $plugin.Methods | Where-Object Name -eq 'DrawChecklistSettings'
    $reviewGuard = @($checklist.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq 'chatChoosesReviewCard'
    })
    Require ($reviewGuard.Count -eq 1 -and $reviewGuard[0].Next.OpCode.Code -in @('Brfalse', 'Brfalse_S') -and
        $reviewGuard[0].Next.Next.OpCode.Code -eq 'Ret') 'Chat review must hide challenge and automatic loading settings'
    Write-Output 'Compiled chat review contract passed: existing roulette, preserved difficulty, difficulty-only controls, and explicit native Play.'

    $entrance = $mod.Types | Where-Object Name -eq 'ChatChoosesCardEntrance'
    $focus = $plugin.Methods | Where-Object Name -eq 'OnApplicationFocus'
    Require (@(Calls $focus $entrance.FullName 'Suspend').Count -eq 1) 'Focus callbacks must restart the pending card wait even without Update frames'
    Require (@($chatUpdate | ForEach-Object { Calls $_ $entrance.FullName 'ShouldOpen' }).Count -eq 1) 'Chat result must wait for focused presentation before opening the roulette'
    Require (@($chatUpdate | ForEach-Object { Calls $_ $entrance.FullName 'Begin' }).Count -eq 1) 'Every prepared chat result must start a fresh presentation wait'
    $visibility = $plugin.Methods | Where-Object Name -eq 'SetVisible'
    Require (@(Calls $visibility $entrance.FullName 'get_Pending').Count -eq 1) 'Native open shortcuts must not bypass the presentation wait'
    $playAction = $plugin.Methods | Where-Object Name -eq 'BeginResultLoad'
    Require (@(Calls $playAction $entrance.FullName 'get_Pending').Count -eq 1) 'Native Play must not bypass the presentation wait'

    $applyLoadout = $plugin.Methods | Where-Object Name -eq 'ApplyLoadout'
    $planeGuard = @($applyLoadout.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq 'IsPlane'
    })
    $charmStore = @($applyLoadout.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'charm' })
    $weaponStore = @($applyLoadout.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'primaryWeapon' })
    Require ($planeGuard.Count -eq 1 -and $planeGuard[0].Next.OpCode.Code -in @('Brfalse', 'Brfalse_S') -and
        $planeGuard[0].Next.Next.OpCode.Code -eq 'Ret') 'Chat plane loadout must return before touching weapons, super or switch flags'
    Require ($charmStore.Count -eq 1 -and $weaponStore.Count -eq 1 -and
        $charmStore[0].Offset -lt $planeGuard[0].Offset -and $planeGuard[0].Offset -lt $weaponStore[0].Offset) 'Plane chat result must still apply the voted charm before preserving equipment'
    $chatProjection = $plugin.Methods | Where-Object Name -eq 'CreateChatChoosesResult'
    Require (@($chatProjection.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'GetPlayerLoadout'
    }).Count -eq 1) 'Plane chat result must derive its internal equipment from the player loadout'
    Require (@(Calls $chatProjection $plugin.FullName 'RandomNonEmptyPoolIndex').Count -eq 0) 'Plane result must never invent a random ground weapon'
    Write-Output 'Compiled focus and plane contract passed: focus delay guards opening/Play and both players preserve weapons, super and switch flags.'
    $preload = $plugin.Methods | Where-Object Name -eq 'CanPreloadNativeInteractionAssets'
    Require (@(Calls $preload 'SceneLoader' 'get_CurrentlyLoading').Count -eq 1) 'Native catalog must require a loading screen'
    Require (@($preload.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq 'creatorToolsNativePreloadWindow'
    }).Count -eq 1) 'Native catalog must require the covered preload window'
    Require (@($preload.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference]
    }).Count -eq 1) 'Map or gameplay eligibility must not permit native preloading'

    $ink = $mod.Types | Where-Object Name -eq 'InkRainChallengeRuntime'
    # Compiling for net35 can still introduce framework types stripped from
    # Cuphead's legacy Mono. Even an unexecuted throw can break JIT entry into
    # a constructor, method or iterator. Check every exception constructor in
    # the compiled plugin against the actual game libraries, including all
    # generated iterator/closure IL.
    $compiledTypes = @($mod.Types | ForEach-Object { SelfAndNestedTypes $_ })
    $runtimeModules = @{}
    try {
        foreach ($type in $compiledTypes) {
            foreach ($method in $type.Methods | Where-Object HasBody) {
                foreach ($instruction in $method.Body.Instructions) {
                    $constructor = $instruction.Operand
                    if ($instruction.OpCode.Code -ne 'Newobj' -or $constructor -isnot [Mono.Cecil.MethodReference] -or
                        $constructor.DeclaringType.FullName -notlike 'System.*Exception') { continue }
                    $scope = $constructor.DeclaringType.Scope.Name
                    if (!$runtimeModules.ContainsKey($scope)) {
                        $runtimeModules[$scope] = [Mono.Cecil.ModuleDefinition]::ReadModule(
                            (Join-Path $CupheadDir "Cuphead_Data/Managed/$scope.dll"))
                    }
                    $nativeType = $runtimeModules[$scope].GetType($constructor.DeclaringType.FullName)
                    Require ($null -ne $nativeType) "Plugin references unavailable Cuphead runtime type $($constructor.DeclaringType.FullName) in $($method.FullName)"
                    Require (@($nativeType.Methods | Where-Object FullName -eq $constructor.FullName).Count -eq 1) "Plugin references an unavailable native constructor: $($constructor.FullName)"
                }
            }
        }
    }
    finally { foreach ($runtimeModule in $runtimeModules.Values) { $runtimeModule.Dispose() } }
    Write-Output 'Compiled Mono compatibility contract passed: every exception constructor, including generated bodies, exists in Cuphead legacy Mono.'
    $ready = $ink.Methods | Where-Object Name -eq 'EnsureInkAssets'
    Require (@($ready.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference]
    }).Count -eq 0) 'Ink readiness must not perform any loading or allocation'
    Require (@($ready.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Ldfld' -and $_.Operand.Name -eq 'inkAssetsReady'
    }).Count -eq 1) 'Ink readiness must read the prepared asset flag'

    $awake = $plugin.Methods | Where-Object Name -eq 'Awake'
    Require (@(Calls $awake $plugin.FullName 'InstallTimedVisualPreparationPatch').Count -eq 1) 'Startup must install the covered visual preparation hook'
    Require (@(Calls $awake $plugin.FullName 'InstallTimedRgbShiftPatches').Count -eq 0) 'Optional render patching must not run synchronously at startup'
    $postfix = $plugin.Methods | Where-Object Name -eq 'TimedVisualPreparationPostfix'
    Require (@($postfix.Parameters | Where-Object {
        $_.Name -eq '___icon' -and $_.ParameterType.FullName -eq 'UnityEngine.UI.Image'
    }).Count -eq 1) 'Visual preparation must receive the native clock image'
    $wrap = @(Calls $postfix $plugin.FullName 'PrepareTimedVisualsBeforeLevel')
    $reset = @($postfix.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq 'Stind_I1' -and $_.Previous.OpCode.Code -eq 'Ldc_I4_0'
    })
    Require ($wrap.Count -eq 1 -and $reset.Count -eq 1 -and $reset[0].Offset -lt $wrap[0].Offset) 'Visual preparation must clear the reused native completion flag before yielding'
    $visualMove = ($plugin.NestedTypes | Where-Object Name -like '<PrepareTimedVisualsBeforeLevel>*').Methods | Where-Object Name -eq 'MoveNext'
    $prepare = @(Calls $visualMove $ink.FullName 'PrepareAssetsDuringLoading')
    $next = @(Calls $visualMove 'System.Collections.IEnumerator' 'MoveNext')
    Require ($prepare.Count -eq 1 -and $next.Count -eq 1 -and $prepare[0].Offset -lt $next[0].Offset) 'Visual assets must finish before starting native scene loading'
    Write-Output 'Compiled cold-start contract passed: no map preloads or lazy ink decoding; visual preparation stays behind the native loading fade.'
}
finally {
    $mod.Dispose()
}
