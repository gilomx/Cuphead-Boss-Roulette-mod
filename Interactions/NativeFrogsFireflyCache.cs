using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeFrogsFireflySpawn
    {
        internal FrogsLevelTallFirefly Actor;
        internal GameObject ScaleRoot;
    }

    internal sealed class NativeFrogsFireflyCache : IDisposable
    {
        private const string FrogsSceneName = "scene_level_frogs";
        private const float OffscreenLabelMargin = 180f;
        private const float DonorLabelVerticalOffsetPixels = -70f;

        private static readonly System.Reflection.FieldInfo FireflyPrefabField =
            AccessTools.Field(typeof(FrogsLevelTall), "fireflyPrefab");
        private static bool suppressPreloadLifecycle;

        private readonly MonoBehaviour coroutineHost;
        private readonly Func<bool> canPreload;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<NativeFrogsFireflySpawn> spawnedActors =
            new List<NativeFrogsFireflySpawn>();

        private FrogsLevelTallFirefly template;
        private Scene preloadedScene;
        private bool preloadStarted;
        private bool preloadFailed;
        private bool disposed;

        internal NativeFrogsFireflyCache(
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
            get { return template != null; }
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
                CaptureFromLoadedFrogs();
            if (Ready || preloadStarted || preloadFailed ||
                coroutineHost == null || !Evaluate(canPreload) ||
                NativeInteractionPreloadCoordinator.
                    IsCurrentGameplayScene(FrogsSceneName))
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
            NativeFrogsFireflySpawnParameters parameters,
            string donor,
            out NativeFrogsFireflySpawn spawned,
            out string error)
        {
            spawned = null;
            error = null;
            if (!Ready)
            {
                error = preloadFailed
                    ? "Cuphead's native Ribby and Croaks firefly asset " +
                        "could not be cached."
                    : "Cuphead's native Ribby and Croaks firefly asset " +
                        "is still loading.";
                return false;
            }
            if (!Evaluate(canSpawn))
            {
                error = "No active gameplay level can receive the interaction.";
                return false;
            }
            if (parameters == null)
            {
                error = "No Ribby and Croaks firefly spawn parameters " +
                    "were supplied.";
                return false;
            }

            FrogsLevelTallFirefly actor = null;
            GameObject scaleRoot = null;
            try
            {
                var player = PlayerManager.GetNext();
                if (player == null)
                    throw new InvalidOperationException(
                        "No active player can be targeted by the firefly.");

                // Firefly.Create starts its movement coroutine inside Init.
                // An inactive source produces an inactive clone and Unity
                // silently drops that coroutine before we can activate it.
                var templateWasActive = template.gameObject.activeSelf;
                if (!templateWasActive)
                    template.gameObject.SetActive(true);
                try
                {
                    actor = template.Create(
                        parameters.Position,
                        parameters.InitialTarget,
                        parameters.Speed,
                        parameters.Health,
                        parameters.FollowDelay,
                        parameters.FollowTime,
                        parameters.FollowDistance,
                        parameters.InvincibleDuration,
                        player,
                        0);
                }
                finally
                {
                    if (!templateWasActive && template != null)
                        template.gameObject.SetActive(false);
                }
                if (actor == null)
                    throw new InvalidOperationException(
                        "Cuphead did not create the native firefly.");

                actor.gameObject.name =
                    "CreatorTools_NativeFrogsFirefly";
                actor.gameObject.SetActive(true);
                var cameraScale = CreatorToolsInteractionPresentation.
                    MatchGameplayCameraScale(actor.gameObject, logWarning);
                scaleRoot = WrapScaleWithoutChangingNativeAnimation(
                    actor,
                    cameraScale);
                CreatorToolsInteractionPresentation.
                    MarkInheritedGameplayCameraScale(
                        actor.gameObject,
                        cameraScale);
                MoveFullyBeyondRightEdge(
                    actor.gameObject,
                    parameters.Position.x,
                    OffscreenLabelMargin * cameraScale);
                CreatorToolsInteractionPresentation.PrepareActor(
                    actor.gameObject,
                    FindLabelAnchor(actor.gameObject),
                    donor,
                    logWarning);
                var donorLabel = actor.gameObject.GetComponent<
                    CreatorToolsDonorLabel>();
                if (donorLabel != null)
                    donorLabel.SetVerticalOffsetPixels(
                        DonorLabelVerticalOffsetPixels);

                spawned = new NativeFrogsFireflySpawn
                {
                    Actor = actor,
                    ScaleRoot = scaleRoot
                };
                spawnedActors.Add(spawned);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                if (scaleRoot != null)
                    UnityEngine.Object.Destroy(scaleRoot);
                else if (actor != null)
                    UnityEngine.Object.Destroy(actor.gameObject);
                return false;
            }
        }

        internal void ClearSpawnedActors()
        {
            for (var i = 0; i < spawnedActors.Count; i++)
                DestroySpawn(spawnedActors[i]);
            spawnedActors.Clear();
        }

        internal static void InstallLifecyclePatches(
            Harmony harmony,
            Action<string> logWarning)
        {
            if (harmony == null)
                return;

            var prefix = AccessTools.Method(
                typeof(NativeFrogsFireflyCache),
                "AllowPreloadedSceneLifecycle");
            NativeInteractionPreloadCoordinator.InstallGlobalLifecycleGuards(
                harmony,
                prefix,
                logWarning,
                "Ribby and Croaks");
            var methods = new[]
            {
                AccessTools.Method(typeof(Level), "Awake"),
                AccessTools.Method(typeof(Level), "OnEnable"),
                AccessTools.Method(typeof(Level), "OnDisable"),
                AccessTools.Method(typeof(Level), "OnDestroy"),
                AccessTools.Method(typeof(FrogsLevelTall), "Awake"),
                AccessTools.Method(typeof(FrogsLevelTall), "Start"),
                AccessTools.Method(typeof(FrogsLevelTall), "OnDestroy")
            };
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i] == null || prefix == null)
                {
                    Warn(logWarning,
                        "Could not install the Ribby and Croaks firefly " +
                        "preload guard.");
                    continue;
                }
                harmony.Patch(
                    methods[i], prefix: new HarmonyMethod(prefix));
            }
        }

        private static bool AllowPreloadedSceneLifecycle(object __instance)
        {
            return !suppressPreloadLifecycle ||
                !BelongsToScene(__instance, FrogsSceneName);
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
                    FrogsSceneName, LoadSceneMode.Additive);
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
                scene = SceneManager.GetSceneByName(FrogsSceneName);
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
                    "The native Ribby and Croaks firefly prefab was not found.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!suppressPreloadLifecycle ||
                !string.Equals(
                    scene.name,
                    FrogsSceneName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            preloadedScene = scene;
            DeactivateSceneRoots(scene);
            if (!Ready)
                CaptureFromScene(scene);
        }

        private void CaptureFromLoadedFrogs()
        {
            var frogs = Resources.FindObjectsOfTypeAll<FrogsLevelTall>();
            for (var i = 0; i < frogs.Length && !Ready; i++)
                CaptureTemplate(frogs[i]);
        }

        private void CaptureFromLoadedResources()
        {
            var frogs = Resources.FindObjectsOfTypeAll<FrogsLevelTall>();
            for (var i = 0; i < frogs.Length && !Ready; i++)
            {
                var frog = frogs[i];
                if (frog == null)
                    continue;
                var scene = frog.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded &&
                    string.Equals(
                        scene.name,
                        FrogsSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    preloadedScene = scene;
                    DeactivateSceneRoots(scene);
                }
                CaptureTemplate(frog);
            }
        }

        private void CaptureFromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && !Ready; i++)
            {
                var frogs = roots[i].GetComponentsInChildren<
                    FrogsLevelTall>(true);
                for (var j = 0; j < frogs.Length && !Ready; j++)
                    CaptureTemplate(frogs[j]);
            }
        }

        private bool CaptureTemplate(FrogsLevelTall frog)
        {
            if (frog == null || Ready)
                return Ready;
            try
            {
                var source = FireflyPrefabField == null
                    ? null
                    : FireflyPrefabField.GetValue(frog) as
                        FrogsLevelTallFirefly;
                if (source == null)
                    return false;

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
                    "CreatorTools_NativeFrogsFirefly_Template";
                template.gameObject.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(template.gameObject);
                preloadFailed = false;
                if (logInfo != null)
                    logInfo(
                        "Prefab nativo de la luciernaga incendiada de " +
                        "Ribby y Croaks guardado para todos los niveles.");
                return true;
            }
            catch (Exception exception)
            {
                if (template != null)
                    UnityEngine.Object.Destroy(template.gameObject);
                template = null;
                Warn(logWarning,
                    "Could not cache Cuphead's native Ribby and Croaks " +
                    "firefly: " + exception.Message);
                return false;
            }
        }

        private static GameObject WrapScaleWithoutChangingNativeAnimation(
            FrogsLevelTallFirefly actor,
            float cameraScale)
        {
            var actorTransform = actor.transform;
            var worldPosition = actorTransform.position;
            var worldRotation = actorTransform.rotation;
            var scaledNative = actorTransform.localScale;
            var nativeScale = new Vector3(
                scaledNative.x / cameraScale,
                scaledNative.y / cameraScale,
                scaledNative.z);

            var scaleRoot = new GameObject(
                "CreatorTools_FrogsFirefly_ScaleRoot");
            scaleRoot.transform.position = worldPosition;
            scaleRoot.transform.rotation = Quaternion.identity;
            scaleRoot.transform.localScale = new Vector3(
                cameraScale,
                cameraScale,
                1f);
            actorTransform.SetParent(scaleRoot.transform, false);
            actorTransform.localPosition = Vector3.zero;
            actorTransform.rotation = worldRotation;
            actorTransform.localScale = nativeScale;
            return scaleRoot;
        }

        private static void MoveFullyBeyondRightEdge(
            GameObject actor,
            float rightBoundaryX,
            float margin)
        {
            if (actor == null)
                return;
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            var leftmostX = float.MaxValue;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sprite == null ||
                    !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                leftmostX = Mathf.Min(leftmostX, renderer.bounds.min.x);
            }

            var targetLeftmostX = rightBoundaryX + Mathf.Max(1f, margin);
            if (leftmostX == float.MaxValue)
            {
                var position = actor.transform.position;
                position.x = targetLeftmostX + Mathf.Max(80f, margin * 0.5f);
                actor.transform.position = position;
                return;
            }

            var adjustment = targetLeftmostX - leftmostX;
            if (adjustment > 0f)
                actor.transform.position += Vector3.right * adjustment;
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
                "Native Ribby and Croaks firefly preload failed: " + error);
        }

        private void RemoveDestroyedActors()
        {
            for (var i = spawnedActors.Count - 1; i >= 0; i--)
            {
                var spawn = spawnedActors[i];
                if (spawn != null && spawn.Actor != null)
                    continue;
                DestroySpawn(spawn);
                spawnedActors.RemoveAt(i);
            }
        }

        private static void DestroySpawn(NativeFrogsFireflySpawn spawn)
        {
            if (spawn == null)
                return;
            if (spawn.ScaleRoot != null)
                UnityEngine.Object.Destroy(spawn.ScaleRoot);
            else if (spawn.Actor != null)
                UnityEngine.Object.Destroy(spawn.Actor.gameObject);
            spawn.Actor = null;
            spawn.ScaleRoot = null;
        }

        private static bool Evaluate(Func<bool> condition)
        {
            if (condition == null)
                return false;
            try { return condition(); }
            catch { return false; }
        }

        private static void Warn(Action<string> warning, string message)
        {
            if (warning != null)
                warning(message);
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
            template = null;
        }
    }
}
