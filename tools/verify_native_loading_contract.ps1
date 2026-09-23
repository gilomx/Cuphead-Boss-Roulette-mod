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
    Write-Output 'Native loading contract passed: fade completes before lazy scene loading, completion is awaited, and immediate transitions stay covered.'
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

    $plugin = $mod.Types | Where-Object Name -eq 'Plugin'
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
    # an otherwise valid preparation iterator. Check its exception constructors
    # against the actual game libraries, including generated iterator/closure IL.
    $inkTypes = @(SelfAndNestedTypes $ink) + @($mod.Types | Where-Object Name -eq 'IncrementalAssetPreparation')
    $runtimeModules = @{}
    try {
        foreach ($type in $inkTypes) {
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
                    Require ($null -ne $nativeType) "Ink preparation references unavailable Cuphead runtime type $($constructor.DeclaringType.FullName) in $($method.FullName)"
                    Require (@($nativeType.Methods | Where-Object FullName -eq $constructor.FullName).Count -eq 1) "Ink preparation references an unavailable native constructor: $($constructor.FullName)"
                }
            }
        }
    }
    finally { foreach ($runtimeModule in $runtimeModules.Values) { $runtimeModule.Dispose() } }
    Write-Output 'Compiled ink compatibility contract passed: exception constructors, including iterator bodies, exist in Cuphead legacy Mono.'
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
