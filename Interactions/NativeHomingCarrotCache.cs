using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeHomingCarrotCache : IDisposable
    {
        private const string VeggiesSceneName = "scene_level_veggies";

        private static readonly System.Reflection.FieldInfo HomingPrefabField =
            AccessTools.Field(typeof(VeggiesLevelCarrot), "homingPrefab");
        private static readonly System.Reflection.FieldInfo ParentField =
            AccessTools.Field(
                typeof(VeggiesLevelCarrotHomingProjectile), "parent");
        private static bool suppressPreloadLifecycle;

        private readonly MonoBehaviour coroutineHost;
        private readonly Func<bool> canPreload;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<VeggiesLevelCarrotHomingProjectile>
            spawnedActors =
                new List<VeggiesLevelCarrotHomingProjectile>();

        private VeggiesLevelCarrotHomingProjectile template;
        private VeggiesLevelCarrot inertParent;
        private GameObject inertParentRoot;
        private Scene preloadedScene;
        private bool preloadStarted;
        private bool preloadFailed;
        private bool disposed;

        internal NativeHomingCarrotCache(
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
            get { return template != null && inertParent != null; }
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

            RemoveDestroyedActors();
            if (!Ready)
                CaptureFromLoadedVeggies();
            if (Ready || preloadStarted || preloadFailed ||
                coroutineHost == null || !Evaluate(canPreload) ||
                NativeInteractionPreloadCoordinator.
                    IsCurrentGameplayScene(VeggiesSceneName))
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
            NativeHomingCarrotSpawnParameters parameters,
            string donor,
            out VeggiesLevelCarrotHomingProjectile spawned,
            out string error)
        {
            spawned = null;
            error = null;
            if (!Ready)
            {
                error = preloadFailed
                    ? "Cuphead's native homing carrot asset could not be cached."
                    : "Cuphead's native homing carrot asset is still loading.";
                return false;
            }
            if (!Evaluate(canSpawn))
            {
                error = "No active gameplay level can receive the interaction.";
                return false;
            }
            if (parameters == null)
            {
                error = "No homing carrot spawn parameters were supplied.";
                return false;
            }

            try
            {
                var target = PlayerManager.GetNext();
                if (target == null)
                    throw new InvalidOperationException(
                        "No active player can be targeted by the homing carrot.");

                spawned = template.Create(
                    target,
                    inertParent,
                    parameters.Position,
                    parameters.Speed,
                    parameters.RotationSpeed,
                    parameters.Health);
                if (spawned == null)
                    throw new InvalidOperationException(
                        "Cuphead did not create the native homing carrot.");

                spawned.gameObject.name =
                    "CreatorTools_NativeHomingCarrot";
                spawned.gameObject.SetActive(true);

                var cameraScale = CreatorToolsInteractionPresentation.
                    MatchGameplayCameraScale(
                        spawned.gameObject,
                        logWarning);
                MoveFullyAboveUpperEdge(
                    spawned.gameObject,
                    parameters.Position.y,
                    16f * cameraScale);

                CreatorToolsInteractionPresentation.PrepareActor(
                    spawned.gameObject,
                    FindLabelAnchor(spawned.gameObject),
                    donor,
                    logWarning);
                spawnedActors.Add(spawned);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                if (spawned != null)
                    UnityEngine.Object.Destroy(spawned.gameObject);
                spawned = null;
                return false;
            }
        }

        internal void ClearSpawnedActors()
        {
            for (var i = 0; i < spawnedActors.Count; i++)
                if (spawnedActors[i] != null)
                    UnityEngine.Object.Destroy(
                        spawnedActors[i].gameObject);
            spawnedActors.Clear();
        }

        internal static void InstallLifecyclePatches(
            Harmony harmony,
            Action<string> logWarning)
        {
            if (harmony == null)
                return;

            var prefix = AccessTools.Method(
                typeof(NativeHomingCarrotCache),
                "AllowPreloadedSceneLifecycle");
            NativeInteractionPreloadCoordinator.InstallGlobalLifecycleGuards(
                harmony,
                prefix,
                logWarning,
                "Root Pack");
            var methods = new[]
            {
                AccessTools.Method(typeof(Level), "Awake"),
                AccessTools.Method(typeof(Level), "OnEnable"),
                AccessTools.Method(typeof(Level), "OnDisable"),
                AccessTools.Method(typeof(Level), "OnDestroy"),
                AccessTools.Method(typeof(VeggiesLevel), "Start"),
                AccessTools.Method(typeof(VeggiesLevel), "OnDestroy"),
                AccessTools.Method(typeof(VeggiesLevelCarrot), "Start"),
                AccessTools.Method(typeof(VeggiesLevelCarrot), "OnLevelEnd")
            };
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i] == null || prefix == null)
                {
                    if (logWarning != null)
                        logWarning(
                            "Could not install the homing carrot preload guard.");
                    continue;
                }
                harmony.Patch(
                    methods[i], prefix: new HarmonyMethod(prefix));
            }
        }

        private static bool AllowPreloadedSceneLifecycle(object __instance)
        {
            return !suppressPreloadLifecycle ||
                !BelongsToScene(__instance, VeggiesSceneName);
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
                    VeggiesSceneName, LoadSceneMode.Additive);
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
                scene = SceneManager.GetSceneByName(VeggiesSceneName);
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
                    "The native Root Pack homing carrot prefab was not found.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!suppressPreloadLifecycle ||
                !string.Equals(
                    scene.name,
                    VeggiesSceneName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            preloadedScene = scene;
            DeactivateSceneRoots(scene);
            if (!Ready)
                CaptureFromScene(scene);
        }

        private void CaptureFromLoadedVeggies()
        {
            var carrots = Resources.FindObjectsOfTypeAll<
                VeggiesLevelCarrot>();
            for (var i = 0; i < carrots.Length && !Ready; i++)
                CaptureTemplate(carrots[i]);
        }

        private void CaptureFromLoadedResources()
        {
            var carrots = Resources.FindObjectsOfTypeAll<
                VeggiesLevelCarrot>();
            for (var i = 0; i < carrots.Length && !Ready; i++)
            {
                var carrot = carrots[i];
                if (carrot == null)
                    continue;
                var scene = carrot.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded &&
                    string.Equals(
                        scene.name,
                        VeggiesSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    preloadedScene = scene;
                    DeactivateSceneRoots(scene);
                }
                CaptureTemplate(carrot);
            }
        }

        private void CaptureFromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && !Ready; i++)
            {
                var carrots = roots[i].GetComponentsInChildren<
                    VeggiesLevelCarrot>(true);
                for (var j = 0; j < carrots.Length && !Ready; j++)
                    CaptureTemplate(carrots[j]);
            }
        }

        private bool CaptureTemplate(VeggiesLevelCarrot carrot)
        {
            if (carrot == null || Ready)
                return Ready;
            try
            {
                var source = HomingPrefabField == null
                    ? null
                    : HomingPrefabField.GetValue(carrot) as
                        VeggiesLevelCarrotHomingProjectile;
                if (source == null)
                    return false;

                EnsureInertParent();
                var sourceWasActive = source.gameObject.activeSelf;
                if (sourceWasActive)
                    source.gameObject.SetActive(false);
                try
                {
                    template = UnityEngine.Object.Instantiate(source);
                }
                finally
                {
                    if (sourceWasActive && source != null)
                        source.gameObject.SetActive(true);
                }

                if (template == null)
                    return false;
                template.gameObject.name =
                    "CreatorTools_NativeHomingCarrot_Template";
                template.gameObject.SetActive(false);
                if (ParentField != null)
                    ParentField.SetValue(template, inertParent);
                UnityEngine.Object.DontDestroyOnLoad(template.gameObject);
                preloadFailed = false;
                if (logInfo != null)
                    logInfo(
                        "Prefab nativo de la zanahoria teledirigida " +
                        "guardado para todos los niveles.");
                return true;
            }
            catch (Exception exception)
            {
                if (template != null)
                    UnityEngine.Object.Destroy(template.gameObject);
                template = null;
                if (logWarning != null)
                    logWarning(
                        "Could not cache Cuphead's native homing carrot: " +
                        exception.Message);
                return false;
            }
        }

        private void EnsureInertParent()
        {
            if (inertParent != null)
                return;
            inertParentRoot = new GameObject(
                "CreatorTools_NativeHomingCarrot_Parent");
            inertParentRoot.SetActive(false);
            inertParent = inertParentRoot.AddComponent<VeggiesLevelCarrot>();
            UnityEngine.Object.DontDestroyOnLoad(inertParentRoot);
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
            if (logWarning != null)
                logWarning(
                    "Native homing carrot preload failed: " + error);
        }

        private void RemoveDestroyedActors()
        {
            for (var i = spawnedActors.Count - 1; i >= 0; i--)
                if (spawnedActors[i] == null)
                    spawnedActors.RemoveAt(i);
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

        public void Dispose()
        {
            disposed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            suppressPreloadLifecycle = false;
            NativeInteractionPreloadCoordinator.Release(this);
            ClearSpawnedActors();
            if (template != null)
                UnityEngine.Object.Destroy(template.gameObject);
            if (inertParentRoot != null)
                UnityEngine.Object.Destroy(inertParentRoot);
            template = null;
            inertParent = null;
            inertParentRoot = null;
        }
    }
}
