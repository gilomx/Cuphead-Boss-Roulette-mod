using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeBaronessHeadTossCache : IDisposable
    {
        private const string BaronessSceneName = "scene_level_baroness";
        private const string ChaseStateName = "Castle_Chase";
        private const string TossParameterName = "Toss";
        private const float BodyFractionOutsideRightEdge = 0.24f;
        private const float OffscreenGapPixels = 190f;
        private const float BottomEdgeInsetPixels = -70f;

        private static readonly FieldInfo CastlePhaseTwoField =
            AccessTools.Field(typeof(BaronessLevelCastle), "baronessPhase2");
        private static readonly FieldInfo CastlePhaseOneField =
            AccessTools.Field(typeof(BaronessLevelCastle), "baronessPhase1");
        private static readonly FieldInfo FollowProjectileField =
            AccessTools.Field(
                typeof(BaronessLevelBaroness),
                "baronessFollowProjectile");
        private static readonly FieldInfo TossPointField =
            AccessTools.Field(typeof(BaronessLevelBaroness),
                "baronessTossPoint");

        private static bool suppressPreloadLifecycle;
        private static bool suppressTemplateLifecycle;

        private readonly MonoBehaviour coroutineHost;
        private readonly Func<bool> canPreload;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<BaronessHeadTossInteractionState> spawnedStates =
            new List<BaronessHeadTossInteractionState>();

        private GameObject baronessTemplate;
        private BaronessLevelFollowingProjectile headTemplate;
        private Vector3 tossLocalPosition;
        private Scene preloadedScene;
        private bool preloadStarted;
        private bool preloadFailed;
        private bool disposed;

        internal NativeBaronessHeadTossCache(
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
            get
            {
                return baronessTemplate != null && headTemplate != null;
            }
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
                CaptureFromLoadedBaroness();
            if (Ready || preloadStarted || preloadFailed ||
                coroutineHost == null || !Evaluate(canPreload) ||
                NativeInteractionPreloadCoordinator.
                    IsCurrentGameplayScene(BaronessSceneName))
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
            out BaronessHeadTossInteractionState state,
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
                            ? "Cuphead's native Baroness head toss could not be cached."
                            : "Cuphead's native Baroness head toss is still loading.");
                if (!Evaluate(canSpawn))
                    throw new InvalidOperationException(
                        "No active gameplay level can receive the interaction.");

                var spawnProperties = ResolveProperties();

                stateRoot = new GameObject(
                    "CreatorTools_BaronessHeadToss_State");
                state = stateRoot.AddComponent<
                    BaronessHeadTossInteractionState>();

                suppressTemplateLifecycle = true;
                try
                {
                    bodyRoot = UnityEngine.Object.Instantiate(
                        baronessTemplate);
                }
                finally
                {
                    suppressTemplateLifecycle = false;
                }
                if (bodyRoot == null)
                    throw new InvalidOperationException(
                        "Cuphead did not create the native Baroness visual.");
                bodyRoot.name = "CreatorTools_NativeBaronessHeadToss";
                bodyRoot.SetActive(false);
                bodyRoot.transform.SetParent(stateRoot.transform, true);

                var castle = bodyRoot.GetComponent<BaronessLevelCastle>();
                var animator = bodyRoot.GetComponent<Animator>();
                if (castle == null || animator == null)
                    throw new InvalidOperationException(
                        "The cached Baroness visual is missing its native controller.");

                DisableNativeBody(bodyRoot);
                EnablePhaseTwoBody(castle);
                ApplyBaronessRendererMask(bodyRoot, false);
                bodyRoot.SetActive(true);

                animator.enabled = true;
                animator.fireEvents = false;
                animator.Rebind();
                animator.Play(ChaseStateName, 0, 0f);
                animator.SetBool(TossParameterName, true);
                animator.Update(0f);
                ApplyBaronessRendererMask(bodyRoot, false);

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
                var bodyRenderers = bodyRoot.GetComponentsInChildren<
                    SpriteRenderer>(true);
                var clonedTossPoint = FindTossPoint(castle);
                CreatorToolsInteractionPresentation.PrepareActor(
                    bodyRoot,
                    FindBaronessLabelAnchor(bodyRenderers),
                    donor,
                    logWarning);
                CreatorToolsInteractionPresentation.SetGiftImage(
                    bodyRoot, giftImagePath, logWarning);

                state.Initialize(
                    bodyRoot,
                    castle,
                    animator,
                    bodyRenderers,
                    headTemplate,
                    spawnProperties,
                    clonedTossPoint,
                    tossLocalPosition,
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
                typeof(NativeBaronessHeadTossCache),
                "AllowBaronessLifecycle");
            if (prefix == null)
            {
                Warn(logWarning,
                    "Could not install the Baroness head toss preload guard.");
                return;
            }

            NativeInteractionPreloadCoordinator.InstallGlobalLifecycleGuards(
                harmony,
                prefix,
                logWarning,
                "Baroness Von Bon Bon");

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

            var assemblyTypes = typeof(BaronessLevel).Assembly.GetTypes();
            var lifecycleNames = new[]
            {
                "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy"
            };
            for (var i = 0; i < assemblyTypes.Length; i++)
            {
                var type = assemblyTypes[i];
                if (type == null ||
                    !type.Name.StartsWith(
                        "BaronessLevel", StringComparison.Ordinal) ||
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
                    "Could not isolate a Baroness lifecycle method: " +
                    exception.Message);
            }
        }

        private static bool AllowBaronessLifecycle(object __instance)
        {
            if (suppressTemplateLifecycle)
                return false;
            if (BelongsToInteraction(__instance))
                return false;
            return !suppressPreloadLifecycle ||
                !BelongsToScene(__instance, BaronessSceneName);
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
                    BaronessSceneName, LoadSceneMode.Additive);
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
                scene = SceneManager.GetSceneByName(BaronessSceneName);
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
                    "The native Baroness head toss assets were not found.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!suppressPreloadLifecycle ||
                !string.Equals(
                    scene.name,
                    BaronessSceneName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            preloadedScene = scene;
            DeactivateSceneRoots(scene);
            if (!Ready)
                CaptureFromScene(scene);
        }

        private void CaptureFromLoadedBaroness()
        {
            var castles = Resources.FindObjectsOfTypeAll<
                BaronessLevelCastle>();
            for (var i = 0; i < castles.Length && !Ready; i++)
            {
                var castle = castles[i];
                if (castle == null || castle.gameObject == null)
                    continue;
                var scene = castle.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;
                CaptureTemplate(castle, FindPhaseOne(scene));
            }
        }

        private void CaptureFromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            BaronessLevelCastle castle = null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (castle == null)
                    castle = roots[i].GetComponentInChildren<
                        BaronessLevelCastle>(true);
            }
            CaptureTemplate(castle, FindPhaseOne(scene));
        }

        private bool CaptureTemplate(
            BaronessLevelCastle castle,
            BaronessLevelBaroness phaseOne)
        {
            if (Ready)
                return true;
            if (castle == null || phaseOne == null ||
                FollowProjectileField == null || TossPointField == null)
                return false;

            try
            {
                var sourceHead = FollowProjectileField.GetValue(phaseOne) as
                    BaronessLevelFollowingProjectile;
                var sourceTossPoint = TossPointField.GetValue(phaseOne) as
                    Transform;
                if (sourceHead == null || sourceTossPoint == null)
                    return false;

                GameObject bodyClone;
                BaronessLevelFollowingProjectile headClone;
                suppressTemplateLifecycle = true;
                try
                {
                    bodyClone = UnityEngine.Object.Instantiate(
                        castle.gameObject);
                    headClone = UnityEngine.Object.Instantiate(sourceHead);
                }
                finally
                {
                    suppressTemplateLifecycle = false;
                }
                if (bodyClone == null || headClone == null)
                {
                    if (bodyClone != null)
                        UnityEngine.Object.Destroy(bodyClone);
                    if (headClone != null)
                        UnityEngine.Object.Destroy(headClone.gameObject);
                    return false;
                }

                bodyClone.name =
                    "CreatorTools_NativeBaronessHeadToss_Template";
                bodyClone.SetActive(false);
                if (bodyClone.GetComponent<
                        CreatorToolsBaronessHeadTossMarker>() == null)
                    bodyClone.AddComponent<
                        CreatorToolsBaronessHeadTossMarker>();
                headClone.gameObject.name =
                    "CreatorTools_NativeBaronessFollowingHead_Template";
                headClone.gameObject.SetActive(false);

                baronessTemplate = bodyClone;
                headTemplate = headClone;
                tossLocalPosition = castle.transform.InverseTransformPoint(
                    sourceTossPoint.position);
                UnityEngine.Object.DontDestroyOnLoad(baronessTemplate);
                UnityEngine.Object.DontDestroyOnLoad(headTemplate.gameObject);
                preloadFailed = false;
                if (logInfo != null)
                    logInfo(
                        "Ataque nativo de la cabeza de la Baronesa " +
                        "guardado sin las capas visibles del castillo.");
                return true;
            }
            catch (Exception exception)
            {
                DestroyTemplates();
                Warn(logWarning,
                    "Could not cache Cuphead's native Baroness head toss: " +
                    exception.Message);
                return false;
            }
        }

        private static BaronessLevelBaroness FindPhaseOne(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var candidates = roots[i].GetComponentsInChildren<
                    BaronessLevelBaroness>(true);
                for (var j = 0; j < candidates.Length; j++)
                    if (candidates[j] != null &&
                        string.Equals(
                            candidates[j].gameObject.name,
                            "BaronessPhase1",
                            StringComparison.Ordinal))
                        return candidates[j];
            }
            return null;
        }

        private static LevelProperties.Baroness ResolveProperties()
        {
            var mode = Level.CurrentMode;
            if (mode != Level.Mode.Easy && mode != Level.Mode.Normal &&
                mode != Level.Mode.Hard)
                mode = Level.Mode.Normal;
            var result = LevelProperties.Baroness.GetMode(mode);
            if (result == null || result.CurrentState == null ||
                result.CurrentState.baronessVonBonbon == null)
                throw new InvalidOperationException(
                    "Cuphead's native Baroness head properties are unavailable.");
            return result;
        }

        private static void DisableNativeBody(GameObject bodyRoot)
        {
            var behaviours = bodyRoot.GetComponentsInChildren<
                MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] != null &&
                    !(behaviours[i] is
                        CreatorToolsBaronessHeadTossMarker))
                    behaviours[i].enabled = false;

            var colliders = bodyRoot.GetComponentsInChildren<
                Collider2D>(true);
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

        private static void EnablePhaseTwoBody(BaronessLevelCastle castle)
        {
            var phaseTwo = CastlePhaseTwoField == null
                ? null
                : CastlePhaseTwoField.GetValue(castle) as Transform;
            if (phaseTwo != null)
                phaseTwo.gameObject.SetActive(true);
        }

        private static Transform FindTossPoint(BaronessLevelCastle castle)
        {
            if (castle == null || CastlePhaseOneField == null ||
                TossPointField == null)
                return null;
            var phaseOne = CastlePhaseOneField.GetValue(castle) as
                BaronessLevelBaroness;
            return phaseOne == null
                ? null
                : TossPointField.GetValue(phaseOne) as Transform;
        }

        private static void ApplyBaronessRendererMask(
            GameObject bodyRoot, bool hidden)
        {
            var renderers = bodyRoot.GetComponentsInChildren<
                SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;
                var name = renderer.gameObject.name;
                renderer.enabled = !hidden &&
                    (string.Equals(
                        name, "BaronessPhase2", StringComparison.Ordinal) ||
                     string.Equals(
                        name, "BaronessPhase2Top", StringComparison.Ordinal));
            }
        }

        private static SpriteRenderer FindBaronessLabelAnchor(
            SpriteRenderer[] renderers)
        {
            SpriteRenderer fallback = null;
            for (var i = 0; renderers != null && i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;
                if (fallback == null)
                    fallback = renderer;
                if (string.Equals(
                        renderer.gameObject.name,
                        "BaronessPhase2Top",
                        StringComparison.Ordinal))
                    return renderer;
            }
            return fallback;
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
            var camera = BaronessHeadTossInteractionState.
                FindGameplayCamera();
            var visible = BaronessHeadTossInteractionState.
                VisibleBounds(bodyRoot);
            if (camera == null || !visible.HasValue)
                return;

            var distance = Mathf.Abs(
                camera.transform.position.z - bodyRoot.transform.position.z);
            var bottomLeft = camera.ViewportToWorldPoint(
                new Vector3(0f, 0f, distance));
            var topRight = camera.ViewportToWorldPoint(
                new Vector3(1f, 1f, distance));
            var bounds = visible.Value;
            var targetRight = topRight.x +
                bounds.size.x * BodyFractionOutsideRightEdge;
            var targetBottom = bottomLeft.y +
                BottomEdgeInsetPixels * cameraScale;
            attackPosition = bodyRoot.transform.position + new Vector3(
                targetRight - bounds.max.x,
                targetBottom - bounds.min.y,
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
                        CreatorToolsBaronessHeadTossMarker>() != null)
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
                "Native Baroness head toss preload failed: " + error);
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
            if (baronessTemplate != null)
                UnityEngine.Object.Destroy(baronessTemplate);
            if (headTemplate != null)
                UnityEngine.Object.Destroy(headTemplate.gameObject);
            baronessTemplate = null;
            headTemplate = null;
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
