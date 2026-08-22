using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal enum NativeZeppelinVariant
    {
        Purple,
        Green
    }

    internal sealed class NativeZeppelinCache : IDisposable
    {
        private const string HildaSceneName =
            "scene_level_flying_blimp";

        private static bool suppressPreloadLifecycle;

        private readonly MonoBehaviour coroutineHost;
        private readonly Func<bool> canPreload;
        private readonly Func<bool> canSpawn;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly List<FlyingBlimpLevelEnemy> spawnedActors =
            new List<FlyingBlimpLevelEnemy>();

        private FlyingBlimpLevelEnemy purpleTemplate;
        private FlyingBlimpLevelEnemy greenTemplate;
        private FlyingBlimpLevelBlimpLady inertParent;
        private GameObject inertParentRoot;
        private Scene preloadedScene;
        private bool preloadStarted;
        private bool preloadFailed;
        private bool disposed;

        internal NativeZeppelinCache(
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

        internal bool Ready(NativeZeppelinVariant variant)
        {
            return GetTemplate(variant) != null;
        }

        internal bool Failed
        {
            get { return preloadFailed; }
        }

        private bool AllReady
        {
            get { return purpleTemplate != null && greenTemplate != null; }
        }

        internal bool CanSpawn(NativeZeppelinVariant variant)
        {
            return Ready(variant) && Evaluate(canSpawn);
        }

        internal void Update()
        {
            if (disposed)
                return;

            RemoveDestroyedActors();
            if (!AllReady)
                CaptureFromLoadedHilda();
            if (AllReady || preloadStarted || preloadFailed ||
                coroutineHost == null || !Evaluate(canPreload))
                return;

            preloadStarted = true;
            coroutineHost.StartCoroutine(PreloadNativeAssets());
        }

        internal bool TrySpawn(
            NativeZeppelinVariant variant,
            NativeZeppelinSpawnParameters parameters,
            string donor,
            out FlyingBlimpLevelEnemy spawned,
            out string error)
        {
            spawned = null;
            error = null;
            var selectedTemplate = GetTemplate(variant);
            if (selectedTemplate == null)
            {
                error = preloadFailed
                    ? "Cuphead's native zeppelin assets could not be cached."
                    : "Cuphead's native zeppelin assets are still loading.";
                return false;
            }
            if (!Evaluate(canSpawn))
            {
                error = "No active gameplay level can receive the interaction.";
                return false;
            }

            try
            {
                var camera = FindGameplayCamera();
                if (camera == null)
                    throw new InvalidOperationException(
                        "No gameplay camera is active.");

                var cameraCenter = camera.transform.position;
                var startPoint = new Vector3(
                    cameraCenter.x, parameters.Lane, 0f);
                var stopPoint = cameraCenter.x +
                    parameters.StopDistance;

                spawned = UnityEngine.Object.Instantiate(selectedTemplate);
                spawned.gameObject.name =
                    "CreatorTools_Native" + variant + "Zeppelin";
                spawned.transform.position = new Vector3(
                    cameraCenter.x + 740f,
                    cameraCenter.y + 360f - parameters.Lane,
                    0f);
                spawned.gameObject.SetActive(true);
                spawned.Init(
                    parameters.Properties,
                    startPoint,
                    stopPoint,
                    parameters.Parryable,
                    inertParent);

                var label = spawned.gameObject.AddComponent<
                    CreatorToolsDonorLabel>();
                label.Initialize(donor);
                spawnedActors.Add(spawned);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                if (spawned != null)
                    UnityEngine.Object.Destroy(spawned.gameObject);
                spawned = null;
                return false;
            }
        }

        internal static void InstallLifecyclePatches(
            Harmony harmony,
            Action<string> logWarning)
        {
            if (harmony == null)
                return;
            var prefix = AccessTools.Method(
                typeof(NativeZeppelinCache),
                "AllowPreloadedSceneLifecycle");
            var methods = new[]
            {
                AccessTools.Method(typeof(Level), "Awake"),
                AccessTools.Method(typeof(Level), "OnEnable"),
                AccessTools.Method(typeof(Level), "OnDisable"),
                AccessTools.Method(typeof(Level), "OnDestroy"),
                AccessTools.Method(typeof(FlyingBlimpLevel), "Start"),
                AccessTools.Method(
                    typeof(FlyingBlimpLevelBlimpLady), "Awake")
            };
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i] == null || prefix == null)
                {
                    if (logWarning != null)
                        logWarning(
                            "Could not install a native interaction preload guard.");
                    continue;
                }
                harmony.Patch(
                    methods[i], prefix: new HarmonyMethod(prefix));
            }
        }

        private static bool AllowPreloadedSceneLifecycle()
        {
            return !suppressPreloadLifecycle;
        }

        private IEnumerator PreloadNativeAssets()
        {
            suppressPreloadLifecycle = true;
            SceneManager.sceneLoaded += OnSceneLoaded;

            AsyncOperation load = null;
            try
            {
                load = SceneManager.LoadSceneAsync(
                    HildaSceneName, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                FailPreload(exception.Message);
            }

            if (load == null)
            {
                FinishPreload();
                yield break;
            }

            load.allowSceneActivation = false;
            while (!disposed && load.progress < 0.9f)
                yield return null;

            if (!disposed && !AllReady)
                CaptureFromLoadedResources();

            load.allowSceneActivation = true;
            while (!load.isDone)
                yield return null;

            if (preloadedScene.IsValid() && preloadedScene.isLoaded)
            {
                if (!AllReady)
                    CaptureFromScene(preloadedScene);
                var unload = SceneManager.UnloadSceneAsync(preloadedScene);
                if (unload != null)
                    while (!unload.isDone)
                        yield return null;
            }

            if (!AllReady && !preloadFailed)
                FailPreload(
                    "The native FlyingBlimp enemy prefabs A and B were not found.");
            FinishPreload();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!suppressPreloadLifecycle ||
                !string.Equals(
                    scene.name, HildaSceneName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            preloadedScene = scene;
            DeactivateSceneRoots(scene);
            if (!AllReady)
                CaptureFromScene(scene);
        }

        private void CaptureFromLoadedHilda()
        {
            var lady = UnityEngine.Object.FindObjectOfType<
                FlyingBlimpLevelBlimpLady>();
            if (lady != null)
                CaptureTemplates(lady);
        }

        private void CaptureFromLoadedResources()
        {
            var ladies = Resources.FindObjectsOfTypeAll<
                FlyingBlimpLevelBlimpLady>();
            for (var i = 0; i < ladies.Length && !AllReady; i++)
                if (ladies[i] != null)
                {
                    var scene = ladies[i].gameObject.scene;
                    if (scene.IsValid() && scene.isLoaded &&
                        string.Equals(
                            scene.name,
                            HildaSceneName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        preloadedScene = scene;
                        DeactivateSceneRoots(scene);
                    }
                    CaptureTemplates(ladies[i]);
                }
        }

        private void CaptureFromScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && !AllReady; i++)
            {
                var ladies = roots[i].GetComponentsInChildren<
                    FlyingBlimpLevelBlimpLady>(true);
                for (var j = 0; j < ladies.Length && !AllReady; j++)
                    CaptureTemplates(ladies[j]);
            }
        }

        private void CaptureTemplates(FlyingBlimpLevelBlimpLady lady)
        {
            CaptureTemplate(
                lady, NativeZeppelinVariant.Purple, "enemyPrefabA");
            CaptureTemplate(
                lady, NativeZeppelinVariant.Green, "enemyPrefabB");
        }

        private bool CaptureTemplate(
            FlyingBlimpLevelBlimpLady lady,
            NativeZeppelinVariant variant,
            string fieldName)
        {
            if (lady == null || Ready(variant))
                return Ready(variant);
            try
            {
                var prefabField = AccessTools.Field(
                    typeof(FlyingBlimpLevelBlimpLady), fieldName);
                var source = prefabField == null
                    ? null
                    : prefabField.GetValue(lady) as FlyingBlimpLevelEnemy;
                if (source == null)
                    return false;

                EnsureInertParent();
                var sourceWasActive = source.gameObject.activeSelf;
                if (sourceWasActive)
                    source.gameObject.SetActive(false);
                try
                {
                    SetTemplate(
                        variant,
                        UnityEngine.Object.Instantiate(source));
                }
                finally
                {
                    if (sourceWasActive && source != null)
                        source.gameObject.SetActive(true);
                }

                var captured = GetTemplate(variant);
                if (captured == null)
                    return false;
                captured.gameObject.name =
                    "CreatorTools_Native" + variant +
                    "Zeppelin_Template";
                captured.gameObject.SetActive(false);
                var parentField = AccessTools.Field(
                    typeof(FlyingBlimpLevelEnemy), "parent");
                if (parentField != null)
                    parentField.SetValue(captured, inertParent);
                UnityEngine.Object.DontDestroyOnLoad(captured.gameObject);
                if (logInfo != null)
                    logInfo(
                        "Prefab nativo del mini zepelin " +
                        variant.ToString().ToLowerInvariant() +
                        " guardado para todos los niveles.");
                return true;
            }
            catch (Exception exception)
            {
                if (logWarning != null)
                    logWarning(
                        "Could not cache Cuphead's native " + variant +
                        " zeppelin: " +
                        exception.Message);
                return false;
            }
        }

        private void EnsureInertParent()
        {
            if (inertParent != null)
                return;
            inertParentRoot = new GameObject(
                "CreatorTools_NativeZeppelin_Parent");
            inertParentRoot.SetActive(false);
            inertParent = inertParentRoot.AddComponent<
                FlyingBlimpLevelBlimpLady>();
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
            if (AllReady)
                preloadFailed = false;
        }

        private void FailPreload(string error)
        {
            preloadFailed = true;
            if (logWarning != null)
                logWarning(
                    "Native zeppelin preload failed: " + error);
        }

        private static Camera FindGameplayCamera()
        {
            var main = Camera.main;
            if (main != null && main.enabled)
                return main;
            var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            for (var i = 0; i < cameras.Length; i++)
                if (cameras[i] != null && cameras[i].enabled &&
                    cameras[i].orthographic)
                    return cameras[i];
            return null;
        }

        private void RemoveDestroyedActors()
        {
            for (var i = spawnedActors.Count - 1; i >= 0; i--)
                if (spawnedActors[i] == null)
                    spawnedActors.RemoveAt(i);
        }

        private static bool Evaluate(Func<bool> condition)
        {
            if (condition == null)
                return false;
            try
            {
                return condition();
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            disposed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            suppressPreloadLifecycle = false;
            for (var i = 0; i < spawnedActors.Count; i++)
                if (spawnedActors[i] != null)
                    UnityEngine.Object.Destroy(
                        spawnedActors[i].gameObject);
            spawnedActors.Clear();
            if (purpleTemplate != null)
                UnityEngine.Object.Destroy(purpleTemplate.gameObject);
            if (greenTemplate != null)
                UnityEngine.Object.Destroy(greenTemplate.gameObject);
            if (inertParentRoot != null)
                UnityEngine.Object.Destroy(inertParentRoot);
            purpleTemplate = null;
            greenTemplate = null;
            inertParent = null;
            inertParentRoot = null;
        }

        private FlyingBlimpLevelEnemy GetTemplate(
            NativeZeppelinVariant variant)
        {
            return variant == NativeZeppelinVariant.Green
                ? greenTemplate
                : purpleTemplate;
        }

        private void SetTemplate(
            NativeZeppelinVariant variant,
            FlyingBlimpLevelEnemy value)
        {
            if (variant == NativeZeppelinVariant.Green)
                greenTemplate = value;
            else
                purpleTemplate = value;
        }
    }
}
