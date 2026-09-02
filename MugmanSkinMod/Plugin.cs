using BepInEx;

namespace Gilomx.MugmanSkinMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mx.gilomx.cuphead.mugmanskin";
        public const string PluginName = "Mugman Skin Mod";
        public const string PluginVersion = "0.1.0";

        private void Awake()
        {
            Logger.LogInfo(
                "Mugman Skin Mod workspace initialized. " +
                "Runtime sprite replacement will be enabled after the edited skin package is ready.");
        }
    }
}
