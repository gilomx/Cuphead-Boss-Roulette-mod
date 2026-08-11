using HarmonyLib;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private static bool loggedDjimmiSuppression;

        private void InstallRouletteDjimmiGuardPatch()
        {
            var activatedCurrentRegion = AccessTools.Method(
                typeof(PlayerData), "DjimmiActivatedCurrentRegion");
            var suppressPostfix = AccessTools.Method(
                typeof(Plugin), "SuppressDjimmiForRouletteBattlePostfix");
            if (activatedCurrentRegion != null && suppressPostfix != null)
                harmony.Patch(activatedCurrentRegion,
                    postfix: new HarmonyMethod(suppressPostfix));
            else
                Logger.LogWarning(
                    "Could not install the roulette Djimmi wish guard.");
        }

        private static void SuppressDjimmiForRouletteBattlePostfix(
            ref bool __result)
        {
            var plugin = activeInstance;
            if (!__result || plugin == null ||
                !plugin.loanedLoadoutsActive)
                return;

            __result = false;
            if (loggedDjimmiSuppression)
                return;

            loggedDjimmiSuppression = true;
            plugin.Logger.LogInfo(
                "Djimmi wish suppressed for a roulette battle.");
        }
    }
}