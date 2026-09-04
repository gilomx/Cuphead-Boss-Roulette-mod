using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeDragonFireballsCache : IDisposable
    {
        private const string DragonSceneName = "scene_level_dragon";
        private const string IdleStateName = "Idle";
        private const string MeteorTriggerName = "OnMeteor";
        private const float BodyFractionOutsideRightEdge = 0.24f;
        private const float BodyViewportCenterY = 0.5f;
        private const float OffscreenGapPixels = 190f;

        private static readonly FieldInfo MeteorPrefabField =
            AccessTools.Field(typeof(DragonLevelDragon), "meteorPrefab");
        private static readonly FieldInfo MouthRootField =
            AccessTools.Field(typeof(DragonLevelDragon), "mouthRoot");

        private static bool suppressPreloadLifecycle;
        private static bool suppressTemplateLifecycle;

        private readonly MonoBehaviour coroutineHost;
        private readonly Func<bool> canPreload;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<DragonFireballsInteractionState> spawnedStates =
            new List<DragonFireballsInteractionState>();

        private GameObject dragonTemplate;
        private DragonLevelMeteor meteorTemplate;
        private Vector3 mouthLocalPosition;
        private Scene preloadedScene;
        private bool preloadStarted;
        private bool preloadFailed;
        private bool disposed;

        internal NativeDragonFireballsCache(
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
            get { return dragonTemplate != null && meteorTemplate != null; }
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
                CaptureFromLoadedDragon();
            if (Ready || preloadStarted || preloadFailed ||
                coroutineHost == null || !Evaluate(canPreload) ||
                NativeInteractionPreloadCoordinator.
                    IsCurrentGameplayScene(DragonSceneName))
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
            string donor,
            string giftImagePath,
            out DragonFireballsInteractionState state,
            out string error)
        {
            state = null;
            error = null;
            GameObject stateRoot = null;
            GameObject bodyRoot = null;
            try
            {
                if (!Ready)
                    throw new InvalidOperationException(
                        preloadFailed
                            ? "Cuphead's native Dragon fireball attack could not be cached."
                            : "Cuphead's native Dragon fireball attack is still loading.");
                if (!Evaluate(canSpawn))
                    throw new InvalidOperationException(
                        "No active gameplay level can receive the interaction.");

                var meteorProperties = ResolveMeteorProperties();
                stateRoot = new GameObject(
                    "CreatorTools_DragonFireballs_State");
                state = stateRoot.AddComponent<
                    DragonFireballsInteractionState>();

                suppressTemplateLifecycle = true;
                try
                {
                    bodyRoot = UnityEngine.Object.Instantiate(dragonTemplate);
                }
                finally
                {
                    suppressTemplateLifecycle = false;
                }
                if (bodyRoot == null)
                    throw new InvalidOperationException(
                        "Cuphead did not create the native Dragon visual.");

                bodyRoot.name = "CreatorTools_NativeDragonFireballs";
                bodyRoot.SetActive(false);
                bodyRoot.transform.SetParent(stateRoot.transform, true);
                var dragon = bodyRoot.GetComponent<DragonLevelDragon>();
                var animator = bodyRoot.GetComponent<Animator>();
                if (dragon == null || animator == null)
                    throw new InvalidOperationException(
                        "The cached Dragon visual is missing its native controller.");

                DisableNativeBody(bodyRoot);
                bodyRoot.SetActive(true);
                animator.enabled = true;
                animator.fireEvents = false;
                animator.Rebind();
                animator.Play(IdleStateName, 0, 0f);
                animator.ResetTrigger(MeteorTriggerName);
                animator.SetTrigger(MeteorTriggerName);
                animator.Update(0f);
                DisableNonSpriteRenderers(bodyRoot);

                var bodyRenderers = bodyRoot.GetComponentsInChildren<
                    SpriteRenderer>(true);
                var bodyRendererVisibility = SnapshotRendererVisibility(
                    bodyRenderers);
                var cameraScale = CreatorToolsInteractionPresentation.
                    MatchGameplayCameraScale(bodyRoot, logWarning);
                Vector3 offscreenPosition;
                Vector3 attackPosition;
                CalculateBodyPositions(
                    bodyRoot,
                    cameraScale,
                    out offscreenPosition,
                    out attackPosition);
                bodyRoot.transform.position = offscreenPosition;

                CreatorToolsInteractionPresentation.PrepareActor(
                    bodyRoot,
                    FindLabelAnchor(bodyRenderers),
                    donor,
                    logWarning);
                CreatorToolsInteractionPresentation.SetGiftImage(
                    bodyRoot, giftImagePath, logWarning);

                state.Initialize(
                    bodyRoot,
                    animator,
                    bodyRenderers,
                    bodyRendererVisibility,
                    FindMouthRoot(dragon),
                    meteorTemplate,
                    meteorProperties,
                    mouthLocalPosition,
                    offscreenPosition,
                    attackPosition,
                    cameraScale,
                    donor,
                    giftImagePath,
                    logWarning);
                spawnedStates.Add(state);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                if (stateRoot != null)
                    UnityEngine.Object.Destroy(stateRoot);
                else if (bodyRoot != null)
                    UnityEngine.Object.Destroy(bodyRoot);
                state = null;
                return false;
            }
        }

        internal void ClearSpawnedActors()
        {
            for (var i = 0; i < spawnedStates.Count; i++)
                if (spawnedStates[i] != null)
                    UnityEngine.Object.Destroy(spawnedStates[i].gameObject);
            spawnedStates.Clear();
        }

        internal static void InstallLifecyclePatches(
            Harmony harmony,
            Action<string> logWarning)
        {
            if (harmony == null)
                return;

            var prefix = AccessTools.Method(
                typeof(NativeDragonFireballsCache),
                "AllowDragonLifecycle");
            if (prefix == null)
            {
                Warn(logWarning,
                    "Could not install the Dragon fireball preload guard.");
                return;
            }

            NativeInteractionPreloadCoordinator.InstallGlobalLifecycleGuards(
                harmony,
                prefix,
                logWarning,
                "Grim Matchstick");

            var patched = new HashSet<MethodBase>();
            PatchLifecycleMethod(
                harmony, prefix,
                AccessTools.Method(typeof(Level), "Awake"),
                patched, logWarning);
            PatchLifecycleMethod(
                harmony, prefix,
                AccessTools.Method(typeof(Level), "OnEnable"),
                patched, logWarning);
            PatchLifecycleMethod(
                harmony, prefix,
                AccessTools.Method(typeof(Level), "OnDisable"),
                patched, logWarning);
            PatchLifecycleMethod(
                harmony, prefix,
                AccessTools.Method(typeof(Level), "OnDestroy"),
                patched, logWarning);

            var assemblyTypes = typeof(DragonLevel).Assembly.GetTypes();
            var lifecycleNames = new[]
            {
                "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy"
            };
            for (var i = 0; i < assemblyTypes.Length; i++)
            {
                var type = assemblyTypes[i];
                if (type == null ||
                    !type.Name.StartsWith(
                        "DragonLevel", StringComparison.Ordinal) ||
                    !typeof(Component).IsAssignableFrom(type))
                    continue;

                for (var j = 0; j < lifecycleNames.Length; j++)
                {
                    var method = type.GetMethod(
                        lifecycleNames[j],
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                        null,
                        Type.EmptyTypes,
                        null);
                    PatchLifecycleMethod(
                        harmony, prefix, method, patched, logWarning);
                }
            }
        }

        private static void PatchLifecycleMethod(
            Harmony harmony,
            MethodInfo prefix,
            MethodBase method,
            HashSet<MethodBase> patched,
            Action<string> logWarning)
        {
            if (method == null || patched.Contains(method))
                return;
            try
            {
                harmony.Patch(
                    method, prefix: new HarmonyMethod(prefix));
                patched.Add(method);
            }
            catch (Exception exception)
            {
                Warn(logWarning,
                    "Could not isolate a Dragon lifecycle method: " +
                    exception.Message);
            }
        }

        private static bool AllowDragonLifecycle(object __instance)
        {
            if (suppressTemplateLifecycle)
                return false;
            if (BelongsToInteraction(__instance))
                return false;
            return !suppressPreloadLifecycle ||
                !BelongsToScene(__instance, DragonSceneName);
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
                    DragonSceneName, LoadSceneMode.Additive);
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

            load.allowSceneActivation = true;
            while (!load.isDone)
                yield return null;

            var scene = preloadedScene;
            if (!scene.IsValid() || !scene.isLoaded)
                scene = SceneManager.GetSceneByName(DragonSceneName);
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
                    "The native Dragon fireball assets were not found.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!suppressPreloadLifecycle ||
                !string.Equals(
                    scene.name,
                    DragonSceneName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            preloadedScene = scene;
            DeactivateSceneRoots(scene);
            if (!Ready)
                CaptureFromScene(scene);
        }

        private void CaptureFromLoadedDragon()
        {
            var dragons = Resources.FindObjectsOfTypeAll<DragonLevelDragon>();
            for (var i = 0; i < dragons.Length && !Ready; i++)
            {
                var dragon = dragons[i];
                if (dragon == null || dragon.gameObject == null)
                    continue;
                var scene = dragon.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;
                CaptureTemplate(dragon);
            }
        }

        private void CaptureFromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            DragonLevelDragon dragon = null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && dragon == null; i++)
                dragon = roots[i].GetComponentInChildren<
                    DragonLevelDragon>(true);
            CaptureTemplate(dragon);
        }

        private bool CaptureTemplate(DragonLevelDragon dragon)
        {
            if (Ready)
                return true;
            if (dragon == null || MeteorPrefabField == null ||
                MouthRootField == null)
                return false;

            try
            {
                var sourceMeteor = MeteorPrefabField.GetValue(dragon) as
                    DragonLevelMeteor;
                var sourceMouth = MouthRootField.GetValue(dragon) as Transform;
                if (sourceMeteor == null || sourceMouth == null)
                    return false;

                GameObject bodyClone;
                DragonLevelMeteor meteorClone;
                suppressTemplateLifecycle = true;
                try
                {
                    bodyClone = UnityEngine.Object.Instantiate(
                        dragon.gameObject);
                    meteorClone = UnityEngine.Object.Instantiate(sourceMeteor);
                }
                finally
                {
                    suppressTemplateLifecycle = false;
                }
                if (bodyClone == null || meteorClone == null)
                {
                    if (bodyClone != null)
                        UnityEngine.Object.Destroy(bodyClone);
                    if (meteorClone != null)
                        UnityEngine.Object.Destroy(meteorClone.gameObject);
                    return false;
                }

                bodyClone.name =
                    "CreatorTools_NativeDragonFireballs_Template";
                bodyClone.SetActive(false);
                if (bodyClone.GetComponent<
                        CreatorToolsDragonFireballsMarker>() == null)
                    bodyClone.AddComponent<
                        CreatorToolsDragonFireballsMarker>();
                meteorClone.gameObject.name =
                    "CreatorTools_NativeDragonFireball_Template";
                meteorClone.gameObject.SetActive(false);

                dragonTemplate = bodyClone;
                meteorTemplate = meteorClone;
                mouthLocalPosition = dragon.transform.InverseTransformPoint(
                    sourceMouth.position);
                UnityEngine.Object.DontDestroyOnLoad(dragonTemplate);
                UnityEngine.Object.DontDestroyOnLoad(meteorTemplate.gameObject);
                preloadFailed = false;
                if (logInfo != null)
                    logInfo(
                        "Ataque nativo de bolas de fuego de Fósforo " +
                        "guardado con el cuerpo del dragón sin colisiones.");
                return true;
            }
            catch (Exception exception)
            {
                DestroyTemplates();
                Warn(logWarning,
                    "Could not cache Cuphead's native Dragon fireball attack: " +
                    exception.Message);
                return false;
            }
        }

        private static LevelProperties.Dragon.Meteor ResolveMeteorProperties()
        {
            var mode = Level.CurrentMode;
            if (mode != Level.Mode.Easy && mode != Level.Mode.Normal &&
                mode != Level.Mode.Hard)
                mode = Level.Mode.Normal;
            var properties = LevelProperties.Dragon.GetMode(mode);
            if (properties == null || properties.CurrentState == null ||
                properties.CurrentState.meteor == null)
                throw new InvalidOperationException(
                    "Cuphead's native Dragon meteor properties are unavailable.");
            return properties.CurrentState.meteor;
        }

        private static void DisableNativeBody(GameObject bodyRoot)
        {
            var behaviours = bodyRoot.GetComponentsInChildren<
                MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] != null &&
                    !(behaviours[i] is CreatorToolsDragonFireballsMarker))
                    behaviours[i].enabled = false;

            var colliders = bodyRoot.GetComponentsInChildren<Collider2D>(true);
            for (var i = 0; i < colliders.Length; i++)
                if (colliders[i] != null)
                    colliders[i].enabled = false;

            var bodies = bodyRoot.GetComponentsInChildren<Rigidbody2D>(true);
            for (var i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null)
                    continue;
                bodies[i].velocity = Vector2.zero;
                bodies[i].angularVelocity = 0f;
                bodies[i].isKinematic = true;
            }
        }

        private static void DisableNonSpriteRenderers(GameObject bodyRoot)
        {
            var renderers = bodyRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i] != null &&
                    !(renderers[i] is SpriteRenderer))
                    renderers[i].enabled = false;
        }

        private static bool[] SnapshotRendererVisibility(
            SpriteRenderer[] renderers)
        {
            var result = new bool[renderers == null ? 0 : renderers.Length];
            for (var i = 0; i < result.Length; i++)
                result[i] = renderers[i] != null && renderers[i].enabled &&
                    renderers[i].gameObject.activeInHierarchy;
            return result;
        }

        private static Transform FindMouthRoot(DragonLevelDragon dragon)
        {
            return dragon == null || MouthRootField == null
                ? null
                : MouthRootField.GetValue(dragon) as Transform;
        }

        private static SpriteRenderer FindLabelAnchor(
            SpriteRenderer[] renderers)
        {
            SpriteRenderer best = null;
            var bestArea = -1f;
            for (var i = 0; renderers != null && i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sprite == null ||
                    !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                var size = renderer.sprite.bounds.size;
                var area = Mathf.Abs(size.x * size.y);
                if (area <= bestArea)
                    continue;
                best = renderer;
                bestArea = area;
            }
            return best;
        }

        private static void CalculateBodyPositions(
            GameObject bodyRoot,
            float cameraScale,
            out Vector3 offscreenPosition,
            out Vector3 attackPosition)
        {
            attackPosition = bodyRoot == null
                ? Vector3.zero
                : bodyRoot.transform.position;
            offscreenPosition = attackPosition +
                Vector3.right * 400f * Mathf.Max(0.01f, cameraScale);
            var camera = DragonFireballsInteractionState.FindGameplayCamera();
            var visible = BaronessHeadTossInteractionState.
                VisibleBounds(bodyRoot);
            if (camera == null || !visible.HasValue)
                return;

            var distance = Mathf.Abs(
                camera.transform.position.z - bodyRoot.transform.position.z);
            var center = camera.ViewportToWorldPoint(new Vector3(
                0.5f, BodyViewportCenterY, distance));
            var topRight = camera.ViewportToWorldPoint(
                new Vector3(1f, 1f, distance));
            var bounds = visible.Value;
            var targetRight = topRight.x +
                bounds.size.x * BodyFractionOutsideRightEdge;
            attackPosition = bodyRoot.transform.position + new Vector3(
                targetRight - bounds.max.x,
                center.y - bounds.center.y,
                0f);
            var visibleBodyWidth = bounds.size.x *
                (1f - BodyFractionOutsideRightEdge);
            offscreenPosition = attackPosition + Vector3.right *
                (visibleBodyWidth + OffscreenGapPixels * cameraScale);
        }

        private static bool BelongsToInteraction(object instance)
        {
            var component = instance as Component;
            if (component == null || component.gameObject == null)
                return false;
            var cursor = component.transform;
            while (cursor != null)
            {
                if (cursor.GetComponent<
                        CreatorToolsDragonFireballsMarker>() != null)
                    return true;
                cursor = cursor.parent;
            }
            return false;
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
            suppressTemplateLifecycle = false;
            NativeInteractionPreloadCoordinator.Release(this);
            if (Ready)
                preloadFailed = false;
        }

        private void FailPreload(string error)
        {
            preloadFailed = true;
            Warn(logWarning,
                "Native Dragon fireball preload failed: " + error);
        }

        private void RemoveDestroyedStates()
        {
            for (var i = spawnedStates.Count - 1; i >= 0; i--)
                if (spawnedStates[i] == null)
                    spawnedStates.RemoveAt(i);
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

        private void DestroyTemplates()
        {
            if (dragonTemplate != null)
                UnityEngine.Object.Destroy(dragonTemplate);
            if (meteorTemplate != null)
                UnityEngine.Object.Destroy(meteorTemplate.gameObject);
            dragonTemplate = null;
            meteorTemplate = null;
        }

        public void Dispose()
        {
            disposed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            suppressPreloadLifecycle = false;
            suppressTemplateLifecycle = false;
            NativeInteractionPreloadCoordinator.Release(this);
            ClearSpawnedActors();
            DestroyTemplates();
        }
    }
}
