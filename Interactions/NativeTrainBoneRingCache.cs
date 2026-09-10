using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeTrainBoneRingSpawn
    {
        internal TrainLevelEngineBossDropperProjectile Actor;
        internal GameObject ScaleRoot;
    }

    internal sealed class NativeTrainBoneRingCache : IDisposable
    {
        private const string TrainSceneName = "scene_level_train";

        private static readonly System.Reflection.FieldInfo BoneRingPrefabField =
            AccessTools.Field(typeof(TrainLevelEngineBoss), "dropperPrefab");
        private static bool suppressPreloadLifecycle;

        private readonly MonoBehaviour coroutineHost;
        private readonly Func<bool> canPreload;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<NativeTrainBoneRingSpawn> spawnedActors =
            new List<NativeTrainBoneRingSpawn>();

        private TrainLevelEngineBossDropperProjectile template;
        private Scene preloadedScene;
        private bool preloadStarted;
        private bool preloadFailed;
        private bool disposed;

        internal NativeTrainBoneRingCache(
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
            if (Ready || preloadStarted || preloadFailed ||
                coroutineHost == null || !Evaluate(canPreload))
                return;

            if (!NativeInteractionPreloadCoordinator.TryAcquire(this))
                return;

            try
            {
                // Search/copy only in a safe preload window, and only for the
                // cache that owns the serialized queue.
                CaptureFromLoadedTrain();
                if (Ready || NativeInteractionPreloadCoordinator.
                    IsCurrentGameplayScene(TrainSceneName))
                {
                    NativeInteractionPreloadCoordinator.Release(this);
                    return;
                }
                preloadStarted = true;
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
            NativeTrainBoneRingSpawnParameters parameters,
            string donor,
            out NativeTrainBoneRingSpawn spawned,
            out string error)
        {
            spawned = null;
            error = null;
            if (!Ready)
            {
                error = preloadFailed
                    ? "Cuphead's native Phantom Express bone ring asset " +
                        "could not be cached."
                    : "Cuphead's native Phantom Express bone ring asset " +
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
                error = "No Phantom Express bone ring spawn parameters " +
                    "were supplied.";
                return false;
            }

            TrainLevelEngineBossDropperProjectile actor = null;
            GameObject scaleRoot = null;
            try
            {
                if (!HasLivePlayer())
                    throw new InvalidOperationException(
                        "No active player can be targeted by the bone ring.");

                // Create starts go_cr and scale_cr inside Init, so the clone
                // must be active. Restore the persistent template immediately;
                // neither Start nor a rendered frame runs on that template.
                var templateWasActive = template.gameObject.activeSelf;
                if (!templateWasActive)
                    template.gameObject.SetActive(true);
                try
                {
                    actor = template.Create(
                        parameters.Position,
                        parameters.UpSpeed,
                        parameters.HorizontalSpeed,
                        parameters.Gravity);
                }
                finally
                {
                    if (!templateWasActive && template != null)
                        template.gameObject.SetActive(false);
                }
                if (actor == null)
                    throw new InvalidOperationException(
                        "Cuphead did not create the native bone ring.");

                actor.gameObject.name =
                    "CreatorTools_NativeTrainBoneRing";
                actor.gameObject.SetActive(true);
                var cameraScale = CreatorToolsInteractionPresentation.
                    GetGameplayCameraScale();
                var bodyScale = CreatorToolsInteractionPresentation.
                    MatchGameplayBodyScale(actor.gameObject, logWarning);
                scaleRoot = WrapScaleWithoutChangingNativeAnimation(
                    actor,
                    bodyScale);
                CreatorToolsInteractionPresentation.
                    MarkInheritedGameplayCameraScale(
                        actor.gameObject,
                        bodyScale);
                MoveFullyAboveCamera(actor, parameters.Position.y,
                    bodyScale, cameraScale);
                var state = scaleRoot.AddComponent<TrainBoneRingInteractionState>();
                state.Initialize(actor, bodyScale, logWarning);
                CreatorToolsInteractionPresentation.PrepareActor(
                    actor.gameObject,
                    FindLabelAnchor(actor.gameObject),
                    donor,
                    logWarning);
                var label = actor.gameObject.GetComponent<CreatorToolsDonorLabel>();
                if (label != null)
                    label.FollowAnimatedBody(actor.GetComponent<SpriteRenderer>(), null, false, null);
                spawned = new NativeTrainBoneRingSpawn
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
                typeof(NativeTrainBoneRingCache),
                "AllowPreloadedSceneLifecycle");
            NativeInteractionPreloadCoordinator.InstallGlobalLifecycleGuards(
                harmony,
                prefix,
                logWarning,
                "Phantom Express");
            var patched = new HashSet<MethodBase>();
            foreach (var name in new[] { "Awake", "OnEnable", "OnDisable", "OnDestroy" })
            {
                var method = AccessTools.Method(typeof(Level), name);
                if (method != null && patched.Add(method))
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix));
            }
            // Train Awake methods create switches, subscribe to singletons and
            // initialize other phases. Isolate all declared scene lifecycles.
            foreach (var type in typeof(TrainLevel).Assembly.GetTypes())
            {
                if ((!type.Name.StartsWith("TrainLevel", StringComparison.Ordinal) &&
                     type != typeof(AbstractTrainLevelSkeletonPart)) ||
                    !typeof(Component).IsAssignableFrom(type))
                    continue;
                foreach (var name in new[] { "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy" })
                {
                    var method = type.GetMethod(name,
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                        null, Type.EmptyTypes, null);
                    if (method != null && patched.Add(method))
                        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                }
            }
            TrainBoneRingInteractionState.InstallPatches(harmony);
        }

        private static bool AllowPreloadedSceneLifecycle(object __instance)
        {
            return !suppressPreloadLifecycle ||
                !BelongsToScene(__instance, TrainSceneName);
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
                    TrainSceneName, LoadSceneMode.Additive);
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
            while (!load.isDone ||
                NativeInteractionPreloadCoordinator.HasPendingNativeAssetLoads)
                yield return null;

            var scene = preloadedScene;
            if (!scene.IsValid() || !scene.isLoaded)
                scene = SceneManager.GetSceneByName(TrainSceneName);
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
                    "The native Phantom Express bone ring prefab was not found.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!suppressPreloadLifecycle ||
                !string.Equals(
                    scene.name,
                    TrainSceneName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            preloadedScene = scene;
            DeactivateSceneRoots(scene);
            if (!Ready)
                CaptureFromScene(scene);
        }

        private void CaptureFromLoadedTrain()
        {
            var engines = Resources.FindObjectsOfTypeAll<TrainLevelEngineBoss>();
            for (var i = 0; i < engines.Length && !Ready; i++)
                CaptureTemplate(engines[i]);
        }

        private void CaptureFromLoadedResources()
        {
            var engines = Resources.FindObjectsOfTypeAll<TrainLevelEngineBoss>();
            for (var i = 0; i < engines.Length && !Ready; i++)
            {
                var engine = engines[i];
                if (engine == null)
                    continue;
                var scene = engine.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded &&
                    string.Equals(
                        scene.name,
                        TrainSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    preloadedScene = scene;
                    DeactivateSceneRoots(scene);
                }
                CaptureTemplate(engine);
            }
        }

        private void CaptureFromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && !Ready; i++)
            {
                var engines = roots[i].GetComponentsInChildren<
                    TrainLevelEngineBoss>(true);
                for (var j = 0; j < engines.Length && !Ready; j++)
                    CaptureTemplate(engines[j]);
            }
        }

        private bool CaptureTemplate(TrainLevelEngineBoss engine)
        {
            if (engine == null || Ready)
                return Ready;
            try
            {
                var source = BoneRingPrefabField == null
                    ? null
                    : BoneRingPrefabField.GetValue(engine) as
                        TrainLevelEngineBossDropperProjectile;
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
                    "CreatorTools_NativeTrainBoneRing_Template";
                template.gameObject.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(template.gameObject);
                preloadFailed = false;
                if (logInfo != null)
                    logInfo(
                        "Prefab nativo del aro de huesos del tren guardado " +
                        "para todos los niveles.");
                return true;
            }
            catch (Exception exception)
            {
                if (template != null)
                    UnityEngine.Object.Destroy(template.gameObject);
                template = null;
                Warn(logWarning,
                    "Could not cache Cuphead's native Phantom Express " +
                    "bone ring: " + exception.Message);
                return false;
            }
        }

        private static GameObject WrapScaleWithoutChangingNativeAnimation(
            TrainLevelEngineBossDropperProjectile actor,
            float bodyScale)
        {
            var actorTransform = actor.transform;
            var worldPosition = actorTransform.position;
            var worldRotation = actorTransform.rotation;
            var scaledNative = actorTransform.localScale;
            var nativeScale = new Vector3(
                scaledNative.x / bodyScale,
                scaledNative.y / bodyScale,
                scaledNative.z);

            var scaleRoot = new GameObject(
                "CreatorTools_TrainBoneRing_ScaleRoot");
            scaleRoot.transform.position = worldPosition;
            scaleRoot.transform.rotation = Quaternion.identity;
            scaleRoot.transform.localScale = new Vector3(
                bodyScale,
                bodyScale,
                1f);
            actorTransform.SetParent(scaleRoot.transform, false);
            actorTransform.localPosition = Vector3.zero;
            actorTransform.rotation = worldRotation;
            actorTransform.localScale = nativeScale;
            return scaleRoot;
        }

        private static void MoveFullyAboveCamera(
            TrainLevelEngineBossDropperProjectile actor,
            float topBoundary,
            float bodyScale,
            float cameraScale)
        {
            var renderer = actor.GetComponent<SpriteRenderer>();
            // Init starts at half scale and grows to one. Reserve the full
            // native sprite below its pivot before letting the ring fall in.
            var lowerExtent = renderer == null || renderer.sprite == null
                ? 100f * bodyScale
                : Mathf.Max(0f, -renderer.sprite.bounds.min.y) * bodyScale;
            var position = actor.transform.position;
            position.y = topBoundary + lowerExtent + 16f * cameraScale;
            actor.transform.position = position;
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
                "Native Phantom Express bone ring preload failed: " + error);
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

        private static void DestroySpawn(NativeTrainBoneRingSpawn spawn)
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

        private static bool HasLivePlayer()
        {
            // Only native go_cr advances GetNext; a validation call here would
            // otherwise skip every other target in co-op.
            foreach (var player in UnityEngine.Object.FindObjectsOfType<AbstractPlayerController>())
                if (player != null && player.gameObject.activeInHierarchy && !player.IsDead)
                    return true;
            return false;
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
