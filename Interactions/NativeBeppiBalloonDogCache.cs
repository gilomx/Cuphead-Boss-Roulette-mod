using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeBeppiBalloonDogSpawn
    {
        internal ClownLevelDogBalloon Actor;
        internal GameObject ScaleRoot;
    }

    internal sealed class NativeBeppiBalloonDogCache : IDisposable
    {
        private const string ClownSceneName = "scene_level_clown";

        private static readonly System.Reflection.FieldInfo RegularPrefabField =
            AccessTools.Field(typeof(ClownLevelClownHelium), "regularDog");
        private static bool suppressPreloadLifecycle;

        private readonly MonoBehaviour coroutineHost;
        private readonly Func<bool> canPreload;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<NativeBeppiBalloonDogSpawn> spawnedActors =
            new List<NativeBeppiBalloonDogSpawn>();

        private ClownLevelDogBalloon template;
        private ClownLevelDogBalloon pinkTemplate;
        private static readonly FieldInfo PinkPrefabField =
            AccessTools.Field(typeof(ClownLevelClownHelium), "pinkDog");
        private Scene preloadedScene;
        private bool preloadStarted;
        private bool preloadFailed;
        private bool disposed;

        internal NativeBeppiBalloonDogCache(
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
            get { return template != null && pinkTemplate != null; }
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
                CaptureFromLoadedClown();
            if (Ready || preloadStarted || preloadFailed ||
                coroutineHost == null || !Evaluate(canPreload) ||
                NativeInteractionPreloadCoordinator.
                    IsCurrentGameplayScene(ClownSceneName))
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
            bool pink,
            NativeBeppiBalloonDogSpawnParameters parameters,
            string donor,
            out NativeBeppiBalloonDogSpawn spawned,
            out string error)
        {
            spawned = null;
            error = null;
            if (!Ready || !Evaluate(canSpawn) || parameters == null)
            {
                error = "The native Beppi balloon dog is not ready for gameplay.";
                return false;
            }
            ClownLevelDogBalloon actor = null;
            GameObject scaleRoot = null;
            try
            {
                var player = PlayerManager.GetNext();
                if (player == null || player.IsDead)
                    throw new InvalidOperationException("No active player can be targeted by the balloon dog.");

                // Clone the inactive prefab, then activate the clone only.
                // Awake initializes damage; Init requires an active coroutine host.
                actor = UnityEngine.Object.Instantiate(pink ? pinkTemplate : template);
                actor.gameObject.name = "CreatorTools_NativeBeppiBalloonDog";
                actor.transform.position = parameters.Position;
                scaleRoot = new GameObject("CreatorTools_BeppiBalloonDog_ScaleRoot");
                scaleRoot.transform.localScale = new Vector3(parameters.CameraScale, parameters.CameraScale, 1f);
                actor.transform.SetParent(scaleRoot.transform, false);
                actor.transform.position = parameters.Position;
                var state = scaleRoot.AddComponent<BeppiBalloonDogInteractionState>();
                state.Initialize(actor);
                CreatorToolsInteractionPresentation.MarkInheritedGameplayCameraScale(actor.gameObject, parameters.CameraScale);
                actor.gameObject.SetActive(true);
                actor.Init(parameters.Properties.dogHP, parameters.Position,
                    parameters.Properties.dogSpeed * parameters.CameraScale,
                    player, parameters.Properties, player.transform.position.x < parameters.Position.x);

                // CalculateSin stores a perpendicular unit vector. Scaling that
                // vector preserves the native wave without changing its timing.
                var wave = (Vector3)WaveDirectionField.GetValue(actor);
                WaveDirectionField.SetValue(actor, wave * parameters.CameraScale);
                actor.GetComponent<Animator>().Update(0f);
                CreatorToolsInteractionPresentation.PrepareActor(actor.gameObject,
                    actor.GetComponent<SpriteRenderer>(), donor, logWarning);
                var label = actor.gameObject.GetComponent<CreatorToolsDonorLabel>();
                if (label != null)
                    label.FollowAnimatedBody(actor.GetComponent<SpriteRenderer>(), null, false, null);
                spawned = new NativeBeppiBalloonDogSpawn { Actor = actor, ScaleRoot = scaleRoot };
                spawnedActors.Add(spawned);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                if (scaleRoot != null) UnityEngine.Object.Destroy(scaleRoot);
                else if (actor != null) UnityEngine.Object.Destroy(actor.gameObject);
                return false;
            }
        }

        private static readonly FieldInfo WaveDirectionField =
            AccessTools.Field(typeof(ClownLevelDogBalloon), "normalized");

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
                typeof(NativeBeppiBalloonDogCache),
                "AllowPreloadedSceneLifecycle");
            NativeInteractionPreloadCoordinator.InstallGlobalLifecycleGuards(
                harmony,
                prefix,
                logWarning,
                "Beppi");
            var patched = new HashSet<MethodBase>();
            foreach (var name in new[] { "Awake", "OnEnable", "OnDisable", "OnDestroy" })
            {
                var method = AccessTools.Method(typeof(Level), name);
                if (method != null && patched.Add(method))
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix));
            }
            // Beppi Awake methods create switches, subscribe to singletons and
            // initialize other phases. Isolate all declared scene lifecycles.
            foreach (var type in typeof(ClownLevel).Assembly.GetTypes())
            {
                if (!type.Name.StartsWith("ClownLevel", StringComparison.Ordinal) ||
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
            BeppiBalloonDogInteractionState.InstallPatches(harmony);
        }

        private static bool AllowPreloadedSceneLifecycle(object __instance)
        {
            return !suppressPreloadLifecycle ||
                !BelongsToScene(__instance, ClownSceneName);
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
                    ClownSceneName, LoadSceneMode.Additive);
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
                scene = SceneManager.GetSceneByName(ClownSceneName);
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
                    "The native Beppi balloon dog prefab was not found.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!suppressPreloadLifecycle ||
                !string.Equals(
                    scene.name,
                    ClownSceneName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            preloadedScene = scene;
            DeactivateSceneRoots(scene);
            if (!Ready)
                CaptureFromScene(scene);
        }

        private void CaptureFromLoadedClown()
        {
            var clowns = Resources.FindObjectsOfTypeAll<ClownLevelClownHelium>();
            for (var i = 0; i < clowns.Length && !Ready; i++)
                CaptureTemplate(clowns[i]);
        }

        private void CaptureFromLoadedResources()
        {
            var clowns = Resources.FindObjectsOfTypeAll<ClownLevelClownHelium>();
            for (var i = 0; i < clowns.Length && !Ready; i++)
            {
                var clown = clowns[i];
                if (clown == null)
                    continue;
                var scene = clown.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded &&
                    string.Equals(
                        scene.name,
                        ClownSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    preloadedScene = scene;
                    DeactivateSceneRoots(scene);
                }
                CaptureTemplate(clown);
            }
        }

        private void CaptureFromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && !Ready; i++)
            {
                var clowns = roots[i].GetComponentsInChildren<
                    ClownLevelClownHelium>(true);
                for (var j = 0; j < clowns.Length && !Ready; j++)
                    CaptureTemplate(clowns[j]);
            }
        }

        private bool CaptureTemplate(ClownLevelClownHelium clown)
        {
            if (clown == null || Ready)
                return Ready;
            try
            {
                var source = RegularPrefabField.GetValue(clown) as ClownLevelDogBalloon;
                var pinkSource = PinkPrefabField.GetValue(clown) as ClownLevelDogBalloon;
                if (source == null || pinkSource == null)
                    return false;
                template = CaptureInactive(source, "CreatorTools_BeppiBalloonDog_Template");
                pinkTemplate = CaptureInactive(pinkSource, "CreatorTools_BeppiBalloonDog_PinkTemplate");
                preloadFailed = false;
                if (logInfo != null)
                    logInfo(
                        "Perritos globo nativos de Beppi guardados " +
                        "para todos los niveles.");
                return true;
            }
            catch (Exception exception)
            {
                if (template != null)
                    UnityEngine.Object.Destroy(template.gameObject);
                template = null;
                if (pinkTemplate != null) UnityEngine.Object.Destroy(pinkTemplate.gameObject);
                pinkTemplate = null;
                Warn(logWarning,
                    "Could not cache Cuphead's native Beppi " +
                    "balloon dog: " + exception.Message);
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
                "Native Beppi balloon dog preload failed: " + error);
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

        private static void DestroySpawn(NativeBeppiBalloonDogSpawn spawn)
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
            if (pinkTemplate != null) UnityEngine.Object.Destroy(pinkTemplate.gameObject);
            template = null;
            pinkTemplate = null;
        }
    }
}
