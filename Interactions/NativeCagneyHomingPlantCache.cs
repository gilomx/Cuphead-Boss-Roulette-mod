using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeCagneyHomingPlantCache : IDisposable
    {
        private const string FlowerSceneName = "scene_level_flower";

        private static readonly System.Reflection.FieldInfo EnemySeedPrefabField =
            AccessTools.Field(typeof(FlowerLevelFlower), "enemySeedPrefab");
        private static bool suppressPreloadLifecycle;

        private readonly MonoBehaviour coroutineHost;
        private readonly Func<bool> canPreload;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<CagneyHomingPlantInteractionState> spawnedStates =
            new List<CagneyHomingPlantInteractionState>();

        private GameObject seedTemplate;
        private FlowerLevelFlower inertParent;
        private GameObject inertParentRoot;
        private Scene preloadedScene;
        private bool preloadStarted;
        private bool preloadFailed;
        private bool disposed;

        internal NativeCagneyHomingPlantCache(
            MonoBehaviour coroutineHost,
            Func<bool> canPreload,
            Func<bool> canSpawn,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.coroutineHost = coroutineHost;
            this.canPreload = canPreload;
            this.canSpawn = canSpawn;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool Ready
        {
            get { return seedTemplate != null && inertParent != null; }
        }

        internal bool Failed
        {
            get { return preloadFailed; }
        }

        internal bool CanSpawn
        {
            get { return Ready && Evaluate(canSpawn); }
        }

        internal void Update()
        {
            if (disposed)
                return;

            RemoveDestroyedStates();
            if (!Ready)
                CaptureFromLoadedFlower();
            if (Ready || preloadStarted || preloadFailed ||
                coroutineHost == null || !Evaluate(canPreload) ||
                NativeInteractionPreloadCoordinator.
                    IsCurrentGameplayScene(FlowerSceneName))
                return;

            if (!NativeInteractionPreloadCoordinator.TryAcquire(this))
                return;

            preloadStarted = true;
            try
            {
                coroutineHost.StartCoroutine(PreloadNativeAssets());
            }
            catch (Exception exception)
            {
                preloadStarted = false;
                FailPreload(exception.Message);
                FinishPreload();
            }
        }

        internal bool TrySpawn(
            NativeCagneyHomingPlantSpawnParameters parameters,
            string donor,
            out CagneyHomingPlantInteractionState state,
            out string error)
        {
            state = null;
            error = null;
            GameObject stateRoot = null;
            GameObject seedObject = null;
            try
            {
                if (!Ready)
                    throw new InvalidOperationException(
                        preloadFailed
                            ? "Cuphead's native Cagney seed asset could not be cached."
                            : "Cuphead's native Cagney seed asset is still loading.");
                if (!Evaluate(canSpawn))
                    throw new InvalidOperationException(
                        "No active gameplay level can receive the interaction.");
                if (parameters == null || parameters.Properties == null)
                    throw new InvalidOperationException(
                        "No Cagney seed spawn parameters were supplied.");

                seedObject = UnityEngine.Object.Instantiate(seedTemplate);
                if (seedObject == null)
                    throw new InvalidOperationException(
                        "Cuphead did not create the native Cagney seed.");
                seedObject.name = "CreatorTools_NativeCagneyBlueSeed";
                seedObject.transform.position = parameters.Position;

                stateRoot = new GameObject(
                    "CreatorTools_CagneyHomingPlant_State");
                state = stateRoot.AddComponent<
                    CagneyHomingPlantInteractionState>();
                var marker = seedObject.AddComponent<
                    CreatorToolsCagneySeedMarker>();
                marker.State = state;

                seedObject.SetActive(true);
                var seed = seedObject.GetComponent<FlowerLevelEnemySeed>();
                if (seed == null)
                    throw new InvalidOperationException(
                        "The cached Cagney seed has no native controller.");
                seed.OnSeedSpawn(
                    parameters.Properties,
                    inertParent,
                    'A',
                    true);

                var cameraScale = CreatorToolsInteractionPresentation.
                    MatchGameplayCameraScale(seedObject, logWarning);
                MoveFullyAboveUpperEdge(
                    seedObject,
                    parameters.Position.y,
                    16f * cameraScale);
                CreatorToolsInteractionPresentation.PrepareActor(
                    seedObject,
                    FindLabelAnchor(seedObject),
                    donor,
                    logWarning);
                var seedLabel = seedObject.GetComponent<
                    CreatorToolsDonorLabel>();
                if (seedLabel != null)
                    seedLabel.Hide();
                state.Initialize(
                    seed,
                    seedLabel,
                    donor,
                    cameraScale,
                    parameters.UseVirtualGroundOnly,
                    logWarning);
                spawnedStates.Add(state);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                if (stateRoot != null)
                    UnityEngine.Object.Destroy(stateRoot);
                else if (seedObject != null)
                    UnityEngine.Object.Destroy(seedObject);
                state = null;
                return false;
            }
        }

        internal void ClearSpawnedActors()
        {
            for (var i = 0; i < spawnedStates.Count; i++)
                if (spawnedStates[i] != null)
                    UnityEngine.Object.Destroy(
                        spawnedStates[i].gameObject);
            spawnedStates.Clear();
        }

        internal static void InstallLifecyclePatches(
            Harmony harmony,
            Action<string> logWarning)
        {
            if (harmony == null)
                return;

            var lifecyclePrefix = AccessTools.Method(
                typeof(NativeCagneyHomingPlantCache),
                "AllowPreloadedSceneLifecycle");
            NativeInteractionPreloadCoordinator.InstallGlobalLifecycleGuards(
                harmony,
                lifecyclePrefix,
                logWarning,
                "Cagney Carnation");
            var lifecycleMethods = new[]
            {
                AccessTools.Method(typeof(Level), "Awake"),
                AccessTools.Method(typeof(Level), "OnEnable"),
                AccessTools.Method(typeof(Level), "OnDisable"),
                AccessTools.Method(typeof(Level), "OnDestroy"),
                AccessTools.Method(typeof(FlowerLevel), "Start"),
                AccessTools.Method(typeof(FlowerLevel), "OnDestroy"),
                AccessTools.Method(typeof(FlowerLevelFlower), "LevelInit"),
                AccessTools.Method(typeof(FlowerLevelFlower), "Update"),
                AccessTools.Method(typeof(FlowerLevelFlower), "OnDestroy")
            };
            for (var i = 0; i < lifecycleMethods.Length; i++)
            {
                if (lifecycleMethods[i] == null || lifecyclePrefix == null)
                {
                    Warn(logWarning,
                        "Could not install the Cagney preload guard.");
                    continue;
                }
                harmony.Patch(
                    lifecycleMethods[i],
                    prefix: new HarmonyMethod(lifecyclePrefix));
            }

            Patch(
                harmony,
                AccessTools.Method(
                    typeof(FlowerLevelEnemySeed), "OnCollisionGround"),
                "AllowCatalogSeedGround",
                null,
                logWarning,
                "Cagney seed ground guard");
            Patch(
                harmony,
                AccessTools.Method(
                    typeof(FlowerLevelEnemySeed), "OnSpawnPlant"),
                "BeforeCatalogPlantSpawn",
                "AfterCatalogPlantSpawn",
                logWarning,
                "Cagney seed transition tracker");
        }

        private static void Patch(
            Harmony harmony,
            System.Reflection.MethodBase original,
            string prefixName,
            string postfixName,
            Action<string> logWarning,
            string label)
        {
            var prefix = string.IsNullOrEmpty(prefixName)
                ? null
                : AccessTools.Method(
                    typeof(NativeCagneyHomingPlantCache), prefixName);
            var postfix = string.IsNullOrEmpty(postfixName)
                ? null
                : AccessTools.Method(
                    typeof(NativeCagneyHomingPlantCache), postfixName);
            if (original == null ||
                (!string.IsNullOrEmpty(prefixName) && prefix == null) ||
                (!string.IsNullOrEmpty(postfixName) && postfix == null))
            {
                Warn(logWarning, "Could not install the " + label + ".");
                return;
            }
            harmony.Patch(
                original,
                prefix: prefix == null ? null : new HarmonyMethod(prefix),
                postfix: postfix == null ? null : new HarmonyMethod(postfix));
        }

        private static bool AllowPreloadedSceneLifecycle(object __instance)
        {
            return !suppressPreloadLifecycle ||
                !BelongsToScene(__instance, FlowerSceneName);
        }

        private static bool AllowCatalogSeedGround(
            FlowerLevelEnemySeed __instance)
        {
            if (__instance == null)
                return true;
            var marker = __instance.GetComponent<
                CreatorToolsCagneySeedMarker>();
            return marker == null || marker.State == null ||
                !marker.State.SuppressNativeGround;
        }

        private static void BeforeCatalogPlantSpawn(
            FlowerLevelEnemySeed __instance,
            out HashSet<int> __state)
        {
            __state = null;
            if (__instance == null ||
                __instance.GetComponent<CreatorToolsCagneySeedMarker>() == null)
                return;
            __state = new HashSet<int>();
            var plants = Resources.FindObjectsOfTypeAll<FlowerLevelVenusSpawn>();
            for (var i = 0; i < plants.Length; i++)
                if (plants[i] != null)
                    __state.Add(plants[i].GetInstanceID());
        }

        private static void AfterCatalogPlantSpawn(
            FlowerLevelEnemySeed __instance,
            HashSet<int> __state)
        {
            if (__instance == null)
                return;
            var marker = __instance.GetComponent<
                CreatorToolsCagneySeedMarker>();
            if (marker == null || marker.State == null)
                return;

            var plants = Resources.FindObjectsOfTypeAll<FlowerLevelVenusSpawn>();
            FlowerLevelVenusSpawn best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < plants.Length; i++)
            {
                var candidate = plants[i];
                if (candidate == null ||
                    !candidate.gameObject.activeInHierarchy ||
                    (__state != null &&
                     __state.Contains(candidate.GetInstanceID())))
                    continue;
                var distance = Vector3.SqrMagnitude(
                    candidate.transform.position -
                    __instance.transform.position);
                if (distance >= bestDistance)
                    continue;
                best = candidate;
                bestDistance = distance;
            }
            marker.State.AttachPlant(best);
        }

        private IEnumerator PreloadNativeAssets()
        {
            suppressPreloadLifecycle = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            var routine = PreloadNativeAssetsCore();
            try
            {
                while (true)
                {
                    var moveNext = false;
                    object current = null;
                    try
                    {
                        moveNext = routine.MoveNext();
                        if (moveNext)
                            current = routine.Current;
                    }
                    catch (Exception exception)
                    {
                        FailPreload(exception.ToString());
                    }
                    if (!moveNext)
                        yield break;
                    yield return current;
                }
            }
            finally
            {
                var disposable = routine as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
                FinishPreload();
            }
        }

        private IEnumerator PreloadNativeAssetsCore()
        {
            AsyncOperation load = null;
            try
            {
                load = SceneManager.LoadSceneAsync(
                    FlowerSceneName, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                FailPreload(exception.Message);
            }
            if (load == null)
                yield break;

            load.allowSceneActivation = false;
            while (!disposed && load.progress < 0.9f)
                yield return null;
            if (!disposed && !Ready)
                CaptureFromLoadedResources();

            load.allowSceneActivation = true;
            while (!load.isDone)
                yield return null;

            var scene = preloadedScene;
            if (!scene.IsValid() || !scene.isLoaded)
                scene = SceneManager.GetSceneByName(FlowerSceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                DeactivateSceneRoots(scene);
                if (!disposed && !Ready)
                    CaptureFromScene(scene);
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                    while (!unload.isDone)
                        yield return null;
            }

            if (!disposed && !Ready && !preloadFailed)
                FailPreload(
                    "The native Cagney blue seed prefab was not found.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!suppressPreloadLifecycle ||
                !string.Equals(
                    scene.name,
                    FlowerSceneName,
                    StringComparison.OrdinalIgnoreCase))
                return;
            preloadedScene = scene;
            DeactivateSceneRoots(scene);
            if (!Ready)
                CaptureFromScene(scene);
        }

        private void CaptureFromLoadedFlower()
        {
            var flowers = Resources.FindObjectsOfTypeAll<FlowerLevelFlower>();
            for (var i = 0; i < flowers.Length && !Ready; i++)
                CaptureTemplate(flowers[i]);
        }

        private void CaptureFromLoadedResources()
        {
            var flowers = Resources.FindObjectsOfTypeAll<FlowerLevelFlower>();
            for (var i = 0; i < flowers.Length && !Ready; i++)
            {
                var flower = flowers[i];
                if (flower == null)
                    continue;
                var scene = flower.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded &&
                    string.Equals(
                        scene.name,
                        FlowerSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    preloadedScene = scene;
                    DeactivateSceneRoots(scene);
                }
                CaptureTemplate(flower);
            }
        }

        private void CaptureFromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && !Ready; i++)
            {
                var flowers = roots[i].GetComponentsInChildren<
                    FlowerLevelFlower>(true);
                for (var j = 0; j < flowers.Length && !Ready; j++)
                    CaptureTemplate(flowers[j]);
            }
        }

        private bool CaptureTemplate(FlowerLevelFlower flower)
        {
            if (flower == null || Ready)
                return Ready;
            try
            {
                var source = EnemySeedPrefabField == null
                    ? null
                    : EnemySeedPrefabField.GetValue(flower) as GameObject;
                if (source == null ||
                    source.GetComponent<FlowerLevelEnemySeed>() == null)
                    return false;

                EnsureInertParent();
                var sourceWasActive = source.activeSelf;
                if (sourceWasActive)
                    source.SetActive(false);
                try
                {
                    seedTemplate = UnityEngine.Object.Instantiate(source);
                }
                finally
                {
                    if (sourceWasActive && source != null)
                        source.SetActive(true);
                }

                if (seedTemplate == null)
                    return false;
                seedTemplate.name =
                    "CreatorTools_NativeCagneyBlueSeed_Template";
                seedTemplate.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(seedTemplate);
                preloadFailed = false;
                if (logInfo != null)
                    logInfo(
                        "Prefab nativo de la semilla azul de Cagney " +
                        "guardado para todos los niveles.");
                return true;
            }
            catch (Exception exception)
            {
                if (seedTemplate != null)
                    UnityEngine.Object.Destroy(seedTemplate);
                seedTemplate = null;
                Warn(logWarning,
                    "Could not cache Cuphead's native Cagney seed: " +
                    exception.Message);
                return false;
            }
        }

        private void EnsureInertParent()
        {
            if (inertParent != null)
                return;
            inertParentRoot = new GameObject(
                "CreatorTools_NativeCagneySeed_Parent");
            inertParentRoot.SetActive(false);
            inertParent = inertParentRoot.AddComponent<FlowerLevelFlower>();
            UnityEngine.Object.DontDestroyOnLoad(inertParentRoot);
        }

        private static bool BelongsToScene(object instance, string sceneName)
        {
            var component = instance as Component;
            if (component == null || component.gameObject == null)
                return false;
            var scene = component.gameObject.scene;
            return scene.IsValid() && string.Equals(
                scene.name,
                sceneName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void DeactivateSceneRoots(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
                if (roots[i] != null)
                    roots[i].SetActive(false);
        }

        private void FinishPreload()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            suppressPreloadLifecycle = false;
            NativeInteractionPreloadCoordinator.Release(this);
            if (Ready)
                preloadFailed = false;
        }

        private void FailPreload(string error)
        {
            preloadFailed = true;
            Warn(logWarning,
                "Native Cagney seed preload failed: " + error);
        }

        private void RemoveDestroyedStates()
        {
            for (var i = spawnedStates.Count - 1; i >= 0; i--)
                if (spawnedStates[i] == null)
                    spawnedStates.RemoveAt(i);
        }

        private static SpriteRenderer FindLabelAnchor(GameObject actor)
        {
            if (actor == null)
                return null;
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer fallback = null;
            SpriteRenderer best = null;
            var bestArea = -1f;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;
                if (fallback == null ||
                    (renderer.enabled && renderer.gameObject.activeInHierarchy))
                    fallback = renderer;
                if (renderer.sprite == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                    continue;
                var size = renderer.sprite.bounds.size;
                var area = Mathf.Abs(size.x * size.y);
                if (area <= bestArea)
                    continue;
                best = renderer;
                bestArea = area;
            }
            return best == null ? fallback : best;
        }

        private static void MoveFullyAboveUpperEdge(
            GameObject actor,
            float upperBoundaryY,
            float margin)
        {
            if (actor == null)
                return;
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            var lowestY = float.MaxValue;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sprite == null ||
                    !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                lowestY = Mathf.Min(lowestY, renderer.bounds.min.y);
            }
            var targetLowestY = upperBoundaryY + Mathf.Max(1f, margin);
            if (lowestY == float.MaxValue)
            {
                var position = actor.transform.position;
                position.y = targetLowestY + Mathf.Max(80f, margin * 5f);
                actor.transform.position = position;
                return;
            }
            var adjustment = targetLowestY - lowestY;
            if (adjustment > 0f)
                actor.transform.position += Vector3.up * adjustment;
        }

        private static bool Evaluate(Func<bool> condition)
        {
            if (condition == null)
                return false;
            try { return condition(); }
            catch { return false; }
        }

        private static void Warn(Action<string> logWarning, string message)
        {
            if (logWarning != null)
                logWarning(message);
        }

        public void Dispose()
        {
            disposed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            suppressPreloadLifecycle = false;
            NativeInteractionPreloadCoordinator.Release(this);
            ClearSpawnedActors();
            if (seedTemplate != null)
                UnityEngine.Object.Destroy(seedTemplate);
            if (inertParentRoot != null)
                UnityEngine.Object.Destroy(inertParentRoot);
            seedTemplate = null;
            inertParent = null;
            inertParentRoot = null;
        }
    }
}
