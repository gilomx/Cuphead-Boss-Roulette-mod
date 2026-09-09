using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeDevilFireCircleSpawn
    {
        internal DevilLevelPitchforkSpinnerProjectile Actor;
        internal GameObject ScaleRoot;
    }

    internal sealed class NativeDevilFireCircleCache : IDisposable
    {
        private const string DevilSceneName = "scene_level_devil";

        private static readonly System.Reflection.FieldInfo CenterPrefabField =
            AccessTools.Field(typeof(DevilLevelSittingDevil), "spinnerProjectilePrefab");
        private static bool suppressPreloadLifecycle;

        private readonly MonoBehaviour coroutineHost;
        private readonly Func<bool> canPreload;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<NativeDevilFireCircleSpawn> spawnedActors =
            new List<NativeDevilFireCircleSpawn>();

        private DevilLevelPitchforkSpinnerProjectile template;
        private DevilLevelPitchforkOrbitingProjectile orbitTemplate;
        private DevilLevelSittingDevil inertParent;
        private GameObject inertParentRoot;
        private const string InertParentName = "CreatorTools_DevilFireCircle_Parent";
        private static readonly FieldInfo OrbitPrefabField =
            AccessTools.Field(typeof(DevilLevelSittingDevil), "spinnerOrbitingProjectilePrefab");
        private Scene preloadedScene;
        private bool preloadStarted;
        private bool preloadFailed;
        private bool disposed;

        internal NativeDevilFireCircleCache(
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
            get { return template != null && orbitTemplate != null && inertParent != null; }
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
                CaptureFromLoadedDevil();
            if (Ready || preloadStarted || preloadFailed ||
                coroutineHost == null || !Evaluate(canPreload) ||
                NativeInteractionPreloadCoordinator.
                    IsCurrentGameplayScene(DevilSceneName))
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
            out NativeDevilFireCircleSpawn spawned,
            out string error)
        {
            spawned = null;
            error = null;
            if (!Ready)
            {
                error = preloadFailed
                    ? "Cuphead's native Devil fire circle asset " +
                        "could not be cached."
                    : "Cuphead's native Devil fire circle asset " +
                        "is still loading.";
                return false;
            }
            if (!Evaluate(canSpawn))
            {
                error = "No active gameplay level can receive the interaction.";
                return false;
            }
            DevilLevelPitchforkSpinnerProjectile actor = null;
            GameObject scaleRoot = null;
            try
            {
                if (!HasLivePlayer())
                    throw new InvalidOperationException(
                        "No active player can be targeted by the fire circle.");

                var mode = Level.CurrentMode;
                if (mode != Level.Mode.Easy && mode != Level.Mode.Normal && mode != Level.Mode.Hard)
                    mode = Level.Mode.Normal;
                var properties = LevelProperties.Devil.GetMode(mode).CurrentState;
                var pitchfork = properties.pitchfork;
                var spinner = properties.pitchforkFiveFlameSpinner;
                var camera = Camera.main;
                if (camera == null || !camera.orthographic)
                    throw new InvalidOperationException("No gameplay camera is active.");
                var cameraScale = Mathf.Max(0.01f, camera.orthographicSize / 360f);
                var position = new Vector2(camera.transform.position.x,
                    camera.transform.position.y + pitchfork.spawnCenterY * cameraScale);

                // GroundHomingMovement reads and writes local coordinates.
                // Keep its parent at world origin with unit scale so those
                // coordinates match the player's world coordinates everywhere.
                scaleRoot = new GameObject("CreatorTools_DevilFireCircle_Group");
                var state = scaleRoot.AddComponent<DevilFireCircleInteractionState>();
                state.CameraScale = cameraScale;
                template.gameObject.SetActive(true);
                try
                {
                    actor = template.Create(position, spinner.maxSpeed * cameraScale,
                        spinner.acceleration * cameraScale, spinner.attackDuration,
                        inertParent, pitchfork.dormantDuration);
                }
                finally { template.gameObject.SetActive(false); }
                if (actor == null)
                    throw new InvalidOperationException("Cuphead did not create the native fire circle.");
                actor.gameObject.name = "CreatorTools_NativeDevilFireCircle";
                actor.transform.SetParent(scaleRoot.transform, true);
                state.Initialize(actor, pitchfork.dormantDuration + spinner.attackDuration);
                CreatorToolsInteractionPresentation.MatchGameplayCameraScale(actor.gameObject, logWarning);

                var rotation = Rand.PosOrNeg() * spinner.rotationSpeed;
                var angles = new DevilLevelPitchforkProjectileSpawner(4, spinner.angleOffset).getSpawnAngles();
                // Four blue flames + one pink center, exactly as LevelInit.
                foreach (var angle in angles)
                {
                    var orbit = orbitTemplate.Create(actor, angle, rotation,
                        pitchfork.spawnRadius * cameraScale, inertParent,
                        pitchfork.dormantDuration);
                    // Orbit.Create only assigns fields: activate after parenting
                    // and positioning, before its native Start enables the wait.
                    orbit.transform.SetParent(actor.transform, false);
                    orbit.transform.position = position + MathUtils.AngleToDirection(angle) *
                        pitchfork.spawnRadius * cameraScale;
                    orbit.gameObject.name = "CreatorTools_DevilFireCircle_Orbit";
                    CreatorToolsInteractionPresentation.MarkInheritedGameplayCameraScale(
                        orbit.gameObject, cameraScale);
                    orbit.gameObject.SetActive(true);
                }
                foreach (var animator in actor.GetComponentsInChildren<Animator>())
                    animator.Update(0f);
                CreatorToolsInteractionPresentation.PrepareActor(actor.gameObject,
                    actor.GetComponent<SpriteRenderer>(), donor, logWarning);
                spawned = new NativeDevilFireCircleSpawn
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
                typeof(NativeDevilFireCircleCache),
                "AllowPreloadedSceneLifecycle");
            NativeInteractionPreloadCoordinator.InstallGlobalLifecycleGuards(
                harmony,
                prefix,
                logWarning,
                "The Devil");
            var patched = new HashSet<MethodBase>();
            foreach (var name in new[] { "Awake", "OnEnable", "OnDisable", "OnDestroy" })
            {
                var method = AccessTools.Method(typeof(Level), name);
                if (method != null && patched.Add(method))
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix));
            }
            // Devil Awake methods create switches, subscribe to singletons and
            // initialize other phases. Isolate all declared scene lifecycles.
            foreach (var type in typeof(DevilLevel).Assembly.GetTypes())
            {
                if (!type.Name.StartsWith("DevilLevel", StringComparison.Ordinal) ||
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
            DevilFireCircleInteractionPatches.Install(harmony);
        }

        private static bool AllowPreloadedSceneLifecycle(object __instance)
        {
            var component = __instance as Component;
            if (component != null && component.gameObject.name == InertParentName)
                return false;
            return !suppressPreloadLifecycle ||
                !BelongsToScene(__instance, DevilSceneName);
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
                    DevilSceneName, LoadSceneMode.Additive);
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
                scene = SceneManager.GetSceneByName(DevilSceneName);
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
                    "The native Devil fire circle prefab was not found.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!suppressPreloadLifecycle ||
                !string.Equals(
                    scene.name,
                    DevilSceneName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            preloadedScene = scene;
            DeactivateSceneRoots(scene);
            if (!Ready)
                CaptureFromScene(scene);
        }

        private void CaptureFromLoadedDevil()
        {
            var devils = Resources.FindObjectsOfTypeAll<DevilLevelSittingDevil>();
            for (var i = 0; i < devils.Length && !Ready; i++)
                CaptureTemplate(devils[i]);
        }

        private void CaptureFromLoadedResources()
        {
            var devils = Resources.FindObjectsOfTypeAll<DevilLevelSittingDevil>();
            for (var i = 0; i < devils.Length && !Ready; i++)
            {
                var devil = devils[i];
                if (devil == null)
                    continue;
                var scene = devil.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded &&
                    string.Equals(
                        scene.name,
                        DevilSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    preloadedScene = scene;
                    DeactivateSceneRoots(scene);
                }
                CaptureTemplate(devil);
            }
        }

        private void CaptureFromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && !Ready; i++)
            {
                var devils = roots[i].GetComponentsInChildren<
                    DevilLevelSittingDevil>(true);
                for (var j = 0; j < devils.Length && !Ready; j++)
                    CaptureTemplate(devils[j]);
            }
        }

        private bool CaptureTemplate(DevilLevelSittingDevil devil)
        {
            if (devil == null || Ready)
                return Ready;
            try
            {
                var source = CenterPrefabField.GetValue(devil) as DevilLevelPitchforkSpinnerProjectile;
                var orbitSource = OrbitPrefabField.GetValue(devil) as DevilLevelPitchforkOrbitingProjectile;
                if (source == null || orbitSource == null)
                    return false;
                inertParentRoot = new GameObject(InertParentName);
                inertParentRoot.SetActive(false);
                inertParent = inertParentRoot.AddComponent<DevilLevelSittingDevil>();
                UnityEngine.Object.DontDestroyOnLoad(inertParentRoot);
                template = CaptureInactive(source, "CreatorTools_DevilFireCircle_Template");
                orbitTemplate = CaptureInactive(orbitSource, "CreatorTools_DevilFireCircle_OrbitTemplate");
                preloadFailed = false;
                if (logInfo != null)
                    logInfo(
                        "Circulo de fuego nativo del Diablo guardado " +
                        "para todos los niveles.");
                return true;
            }
            catch (Exception exception)
            {
                if (template != null)
                    UnityEngine.Object.Destroy(template.gameObject);
                template = null;
                if (orbitTemplate != null) UnityEngine.Object.Destroy(orbitTemplate.gameObject);
                if (inertParentRoot != null) UnityEngine.Object.Destroy(inertParentRoot);
                orbitTemplate = null;
                inertParent = null;
                inertParentRoot = null;
                Warn(logWarning,
                    "Could not cache Cuphead's native Devil " +
                    "fire circle: " + exception.Message);
                return false;
            }
        }

        private static T CaptureInactive<T>(T source, string name) where T : Component
        {
            var wasActive = source.gameObject.activeSelf;
            source.gameObject.SetActive(false);
            T clone;
            try { clone = UnityEngine.Object.Instantiate(source); }
            finally { source.gameObject.SetActive(wasActive); }
            clone.gameObject.name = name;
            clone.gameObject.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(clone.gameObject);
            return clone;
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
                "Native Devil fire circle preload failed: " + error);
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

        private static void DestroySpawn(NativeDevilFireCircleSpawn spawn)
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
            // Only the native homing coroutine advances GetNext; a validation call here would
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
            if (orbitTemplate != null) UnityEngine.Object.Destroy(orbitTemplate.gameObject);
            if (inertParentRoot != null) UnityEngine.Object.Destroy(inertParentRoot);
            template = null;
            orbitTemplate = null;
            inertParent = null;
            inertParentRoot = null;
        }
    }
}
