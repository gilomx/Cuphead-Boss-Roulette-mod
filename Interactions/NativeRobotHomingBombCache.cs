using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeRobotHomingBombSpawn
    {
        internal RobotLevelHatchBombBot Actor;
        internal GameObject ScaleRoot;
    }

    internal sealed class NativeRobotHomingBombCache : IDisposable
    {
        private const string RobotSceneName = "scene_level_robot";
        private const float OffscreenLabelMargin = 180f;

        private static readonly System.Reflection.FieldInfo BombPrefabField =
            AccessTools.Field(typeof(RobotLevelRobotBodyPart), "secondary");
        private static bool suppressPreloadLifecycle;

        private readonly MonoBehaviour coroutineHost;
        private readonly Func<bool> canPreload;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<NativeRobotHomingBombSpawn> spawnedActors =
            new List<NativeRobotHomingBombSpawn>();

        private RobotLevelHatchBombBot template;
        private Scene preloadedScene;
        private bool preloadStarted;
        private bool preloadFailed;
        private bool disposed;

        internal NativeRobotHomingBombCache(
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
                CaptureFromLoadedRobot();
            if (Ready || preloadStarted || preloadFailed ||
                coroutineHost == null || !Evaluate(canPreload) ||
                NativeInteractionPreloadCoordinator.
                    IsCurrentGameplayScene(RobotSceneName))
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
            NativeRobotHomingBombSpawnParameters parameters,
            string donor,
            out NativeRobotHomingBombSpawn spawned,
            out string error)
        {
            spawned = null;
            error = null;
            if (!Ready)
            {
                error = preloadFailed
                    ? "Cuphead's native Dr. Kahl homing bomb asset " +
                        "could not be cached."
                    : "Cuphead's native Dr. Kahl homing bomb asset " +
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
                error = "No Dr. Kahl homing bomb spawn parameters " +
                    "were supplied.";
                return false;
            }

            RobotLevelHatchBombBot actor = null;
            GameObject scaleRoot = null;
            try
            {
                var player = PlayerManager.GetNext();
                if (player == null)
                    throw new InvalidOperationException(
                        "No active player can be targeted by the homing bomb.");

                // HomingProjectile.Create only configures the clone. Movement
                // starts in Start, so initialize HP/damage before activation.
                // Keep the persistent template inactive throughout the call.
                var bomb = parameters.Properties;
                actor = template.Create(
                    parameters.Position,
                    180f,
                    bomb.initialBombMovementSpeed,
                    bomb.bombHomingSpeed,
                    bomb.bombRotationSpeed,
                    bomb.bombLifeTime,
                    parameters.InitialMovementDuration,
                    4f,
                    player) as RobotLevelHatchBombBot;
                if (actor == null)
                    throw new InvalidOperationException(
                        "Cuphead did not create the native homing bomb.");

                actor.gameObject.name =
                    "CreatorTools_NativeRobotHomingBomb";
                actor.InitBombBot(bomb);
                actor.transform.right = Vector3.down;
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
                spawned = new NativeRobotHomingBombSpawn
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
                typeof(NativeRobotHomingBombCache),
                "AllowPreloadedSceneLifecycle");
            NativeInteractionPreloadCoordinator.InstallGlobalLifecycleGuards(
                harmony,
                prefix,
                logWarning,
                "Dr. Kahl");
            var methods = new[]
            {
                AccessTools.Method(typeof(Level), "Awake"),
                AccessTools.Method(typeof(Level), "OnEnable"),
                AccessTools.Method(typeof(Level), "OnDisable"),
                AccessTools.Method(typeof(Level), "OnDestroy"),
                AccessTools.Method(typeof(RobotLevel), "Start"),
                AccessTools.Method(typeof(RobotLevel), "OnDestroy"),
                AccessTools.Method(typeof(RobotLevelRobot), "Awake"),
                AccessTools.Method(typeof(RobotLevelRobotBodyPart), "Awake"),
                AccessTools.Method(typeof(RobotLevelRobotBodyPart), "OnDestroy"),
                AccessTools.Method(typeof(RobotLevelHelihead), "Awake"),
                AccessTools.Method(typeof(RobotLevelHelihead), "OnDestroy"),
                AccessTools.Method(typeof(RobotLevelGem), "OnDestroy")
            };
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i] == null || prefix == null)
                {
                    Warn(logWarning,
                        "Could not install the Dr. Kahl homing bomb " +
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
                !BelongsToScene(__instance, RobotSceneName);
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
                    RobotSceneName, LoadSceneMode.Additive);
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
                scene = SceneManager.GetSceneByName(RobotSceneName);
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
                    "The native Dr. Kahl homing bomb prefab was not found.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!suppressPreloadLifecycle ||
                !string.Equals(
                    scene.name,
                    RobotSceneName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            preloadedScene = scene;
            DeactivateSceneRoots(scene);
            if (!Ready)
                CaptureFromScene(scene);
        }

        private void CaptureFromLoadedRobot()
        {
            var hatches = Resources.FindObjectsOfTypeAll<RobotLevelRobotHatch>();
            for (var i = 0; i < hatches.Length && !Ready; i++)
                CaptureTemplate(hatches[i]);
        }

        private void CaptureFromLoadedResources()
        {
            var hatches = Resources.FindObjectsOfTypeAll<RobotLevelRobotHatch>();
            for (var i = 0; i < hatches.Length && !Ready; i++)
            {
                var hatch = hatches[i];
                if (hatch == null)
                    continue;
                var scene = hatch.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded &&
                    string.Equals(
                        scene.name,
                        RobotSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    preloadedScene = scene;
                    DeactivateSceneRoots(scene);
                }
                CaptureTemplate(hatch);
            }
        }

        private void CaptureFromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && !Ready; i++)
            {
                var hatches = roots[i].GetComponentsInChildren<
                    RobotLevelRobotHatch>(true);
                for (var j = 0; j < hatches.Length && !Ready; j++)
                    CaptureTemplate(hatches[j]);
            }
        }

        private bool CaptureTemplate(RobotLevelRobotHatch hatch)
        {
            if (hatch == null || Ready)
                return Ready;
            try
            {
                var prefab = BombPrefabField == null
                    ? null
                    : BombPrefabField.GetValue(hatch) as GameObject;
                var source = prefab == null
                    ? null
                    : prefab.GetComponent<RobotLevelHatchBombBot>();
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
                    "CreatorTools_NativeRobotHomingBomb_Template";
                template.gameObject.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(template.gameObject);
                preloadFailed = false;
                if (logInfo != null)
                    logInfo(
                        "Prefab nativo de la bomba teledirigida del Dr. Kahl " +
                        "guardado para todos los niveles.");
                return true;
            }
            catch (Exception exception)
            {
                if (template != null)
                    UnityEngine.Object.Destroy(template.gameObject);
                template = null;
                Warn(logWarning,
                    "Could not cache Cuphead's native Dr. Kahl " +
                    "homing bomb: " + exception.Message);
                return false;
            }
        }

        private static GameObject WrapScaleWithoutChangingNativeAnimation(
            RobotLevelHatchBombBot actor,
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
                "CreatorTools_RobotHomingBomb_ScaleRoot");
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
                "Native Dr. Kahl homing bomb preload failed: " + error);
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

        private static void DestroySpawn(NativeRobotHomingBombSpawn spawn)
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
