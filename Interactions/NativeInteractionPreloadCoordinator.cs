using System;
using HarmonyLib;

namespace Gilomx.CupheadBossRoulette
{
    /// <summary>
    /// Serializes additive scene loads used only to retain native interaction
    /// prefabs. Unity's async scene queue can stall when more than one load is
    /// held below activation at the same time.
    /// </summary>
    internal static class NativeInteractionPreloadCoordinator
    {
        private static object owner;

        internal static bool TryAcquire(object candidate)
        {
            if (candidate == null)
                return false;
            if (owner == null)
                owner = candidate;
            return ReferenceEquals(owner, candidate);
        }

        internal static void Release(object candidate)
        {
            if (ReferenceEquals(owner, candidate))
                owner = null;
        }

        internal static bool IsCurrentGameplayScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;
            try
            {
                var level = Level.Current;
                if (level == null || level.gameObject == null)
                    return false;
                var scene = level.gameObject.scene;
                return scene.IsValid() && scene.isLoaded &&
                    string.Equals(
                        scene.name,
                        sceneName,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        internal static void InstallGlobalLifecycleGuards(
            Harmony harmony,
            System.Reflection.MethodInfo prefix,
            Action<string> logWarning,
            string sourceLabel)
        {
            if (harmony == null || prefix == null)
                return;
            var methods = new[]
            {
                AccessTools.Method(typeof(AudioManagerComponent), "Awake"),
                AccessTools.Method(typeof(AudioManagerComponent), "OnDestroy"),
                AccessTools.Method(typeof(AbstractPauseGUI), "Awake"),
                AccessTools.Method(typeof(LevelPauseGUI), "Awake"),
                AccessTools.Method(typeof(LevelPauseGUI), "OnEnable"),
                AccessTools.Method(typeof(LevelPauseGUI), "OnDisable"),
                AccessTools.Method(typeof(LevelPauseGUI), "OnDestroy"),
                AccessTools.Method(typeof(LevelHUD), "Awake"),
                AccessTools.Method(typeof(LevelHUD), "Start"),
                AccessTools.Method(typeof(LevelHUD), "OnDestroy"),
                AccessTools.Method(typeof(PlayerManager), "Awake"),
                AccessTools.Method(typeof(PlayerInput), "Awake"),
                AccessTools.Method(typeof(PlayerInput), "Start"),
                AccessTools.Method(typeof(CupheadLevelCamera), "Awake"),
                AccessTools.Method(typeof(CupheadLevelCamera), "Start"),
                AccessTools.Method(typeof(CupheadLevelCamera), "OnDestroy")
            };
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i] == null)
                {
                    if (logWarning != null)
                        logWarning(
                            "Could not isolate a global lifecycle method " +
                            "during the " + sourceLabel + " preload.");
                    continue;
                }
                harmony.Patch(
                    methods[i],
                    prefix: new HarmonyMethod(prefix));
            }
        }
    }
}
