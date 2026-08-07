using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mx.gilomx.cuphead.bossroulette";
        public const string PluginName = "Gilomx Boss Roulette";
        public const string PluginVersion = "0.5.122";

        private const float DesignWidth = 1280f;
        private const float DesignHeight = 720f;
        // TEMPORARY TEST SELECTOR. Keep non-None while developing a challenge.
        // Compatible bosses are still chosen randomly.
        private static readonly ModifierId ForcedTestChallenge =
            ModifierId.None;
        // Dormant test selector: alternate cursed/divine relic each spin.
        private static readonly bool ForceRelicTestSequence = false;
        // Dormant test selector: exercise both restricted plane weapons.
        private static readonly bool ForcePlaneRelicChallengeTestSequence =
            false;
        // TEMPORARY VISUAL TEST. It does not change either player's real meter.
        private static readonly bool ForceFiveSuperCardsForHudTest = false;
        // Dormant boss-test selector. Keep false in normal builds.
        private static readonly bool ForceTestBoss = false;
        private static readonly Levels[] ForcedTestBossSequence =
        {
            Levels.Saltbaker,
            Levels.Devil
        };
        // Dormant localization test shortcut. Keep false in normal builds.
        private const bool EnableLanguageTestShortcut = false;
        private static readonly KeyboardShortcut LanguageTestLeftShortcut =
            new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl);
        private static readonly KeyboardShortcut LanguageTestRightShortcut =
            new KeyboardShortcut(KeyCode.F8, KeyCode.RightControl);

        private const float BlackAndWhiteEntryDelay = 1.5f;
        private const float BlackAndWhiteFadeInDuration = 1.25f;
        private const float BlackAndWhiteFadeOutDuration = 0.9f;
        private const float SpinAudioVolume = 0.45f;
        private const float SelectionStopAudioVolume = 0.45f;
        private static readonly Color Ink = new Color(0.075f, 0.065f, 0.055f);
        private static readonly Color Red = new Color(0.67f, 0.12f, 0.10f);
        private static readonly Color Cream = new Color(0.94f, 0.87f, 0.70f);
        private static readonly Color Gold = new Color(0.94f, 0.72f, 0.19f);

        private sealed class LoadoutSnapshot
        {
            private readonly Weapon primaryWeapon;
            private readonly Weapon secondaryWeapon;
            private readonly Super super;
            private readonly Charm charm;
            private readonly bool hasSecondaryRegularWeapon;
            private readonly bool hasSecondaryShmupWeapon;
            private readonly bool mustNotifyRegularWeapon;
            private readonly bool mustNotifyShmupWeapon;

            private LoadoutSnapshot(
                Weapon primaryWeapon,
                Weapon secondaryWeapon,
                Super super,
                Charm charm,
                bool hasSecondaryRegularWeapon,
                bool hasSecondaryShmupWeapon,
                bool mustNotifyRegularWeapon,
                bool mustNotifyShmupWeapon)
            {
                this.primaryWeapon = primaryWeapon;
                this.secondaryWeapon = secondaryWeapon;
                this.super = super;
                this.charm = charm;
                this.hasSecondaryRegularWeapon = hasSecondaryRegularWeapon;
                this.hasSecondaryShmupWeapon = hasSecondaryShmupWeapon;
                this.mustNotifyRegularWeapon = mustNotifyRegularWeapon;
                this.mustNotifyShmupWeapon = mustNotifyShmupWeapon;
            }

            internal static LoadoutSnapshot Capture(PlayerId playerId)
            {
                var loadout = PlayerData.Data.Loadouts.GetPlayerLoadout(playerId);
                if (loadout == null)
                    return null;

                return new LoadoutSnapshot(
                    loadout.primaryWeapon,
                    loadout.secondaryWeapon,
                    loadout.super,
                    loadout.charm,
                    loadout.HasEquippedSecondaryRegularWeapon,
                    loadout.HasEquippedSecondarySHMUPWeapon,
                    loadout.MustNotifySwitchRegularWeapon,
                    loadout.MustNotifySwitchSHMUPWeapon);
            }

            internal void Restore(PlayerId playerId)
            {
                var loadout = PlayerData.Data.Loadouts.GetPlayerLoadout(playerId);
                if (loadout == null)
                    return;

                loadout.primaryWeapon = primaryWeapon;
                loadout.secondaryWeapon = secondaryWeapon;
                loadout.super = super;
                loadout.charm = charm;
                loadout.HasEquippedSecondaryRegularWeapon = hasSecondaryRegularWeapon;
                loadout.HasEquippedSecondarySHMUPWeapon = hasSecondaryShmupWeapon;
                loadout.MustNotifySwitchRegularWeapon = mustNotifyRegularWeapon;
                loadout.MustNotifySwitchSHMUPWeapon = mustNotifyShmupWeapon;
            }
        }

        private readonly System.Random random = new System.Random();
        private int forcedRelicTestSpin;
        private int forcedPlaneRelicChallengeTestSpin;
        private int forcedBossTestSpin;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly List<int> availableBossIndices = new List<int>();
        private readonly List<int> availableWeaponIndices = new List<int>();
        private readonly List<int> availableSuperIndices = new List<int>();
        private readonly List<int> availableCharmIndices = new List<int>();
        private readonly float[] pulseUntil = new float[6];
        private ConfigEntry<KeyboardShortcut> toggleShortcut;
        private ConfigEntry<KeyboardShortcut> spinShortcut;
        private ConfigEntry<bool> autoLoad;
        private ConfigEntry<Level.Mode> difficultySetting;
        private ConfigEntry<bool> challengeSetting;
        private ConfigEntry<float> loadDelay;
        private ModLocalization modLocalization;
        private GameTheme theme;
        private AudioSource audioSource;
        private AudioSource effectsAudioSource;
        private AudioClip spinClip;
        private AudioClip selectionClip;
        private AudioClip openClip;
        private AudioClip closeClip;
        private AudioClip battleHudImpactClip;
        private AssetBundle blackAndWhiteShaderBundle;
        private Shader blackAndWhiteTransitionShader;
        private Shader battleHudSaturationShader;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle bossStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle buttonActiveStyle;

        private Font stylesFont;
        private bool visible;
        private float cardVisibility;
        private int navigationIndex = 3;
        private float cardRoll;
        private bool uglyMode;
        private bool running;
        private bool pendingLoad;
        private bool resultReady;
        private MapEquipUI nativeMapEquipUi;
        private static Plugin activeInstance;
        private Harmony harmony;
        private int suppressMapPauseUntilFrame = -1;
        private bool rightTriggerWasHeld;
        private float spinStartedAt;
        private float loadAt;
        private int ticker;
        private int revealed;
        private Level.Mode difficulty = Level.Mode.Normal;
        private RouletteResult result = new RouletteResult();
        private RouletteStatus status = RouletteStatus.Ready;
        private ModifierId activeChallenge = ModifierId.None;
        private int activeChallengeBoss = -1;
        private float blackAndWhiteBlend;
        private float blackAndWhiteTransitionStartedAt = -1f;
        private float blackAndWhiteTransitionFrom;
        private float blackAndWhiteTransitionTo;
        private float blackAndWhiteTransitionDelay;
        private float blackAndWhiteTransitionDuration;
        private int blackAndWhiteLevelInstanceId = -1;
        private bool blackAndWhiteFadeOutStarted;
        private bool blackAndWhiteNativeBaseActive;
        private readonly List<BlackAndWhiteSaturationEffect>
            blackAndWhiteEffects =
                new List<BlackAndWhiteSaturationEffect>();
        private float nextBlackAndWhiteEffectScanAt;
        private bool blackAndWhiteRenderFailureLogged;
        private bool soloMiniRestartPending;
        private bool dlcAvailabilityKnown;
        private bool dlcEnabledForRoulette;
        private LoadoutSnapshot originalPlayerOneLoadout;
        private LoadoutSnapshot originalPlayerTwoLoadout;
        private bool loanedLoadoutsActive;
        private bool loanedBattleSeen;
        private bool returnToMapAfterRouletteFinalBossWin;
        private bool rouletteReturnDestinationPending;
        private Levels rouletteReturnLevel;
        private Scenes rouletteReturnMap;
        private static int curseRelicRuntimeSetupDepth;
        private bool languageTestOriginalCaptured;
        private Localization.Languages languageTestOriginalLanguage;
        private int languageTestCycleIndex = -1;
        private float languageTestNoticeUntil;

        private string AssetsDirectory
        {
            get { return Path.Combine(Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath, "assets"); }
        }

        private string L(ModText id)
        {
            return modLocalization == null ? id.ToString() :
                modLocalization.Text(id);
        }

        private string LocalizedModifierName(ModifierId id)
        {
            return modLocalization == null ? id.ToString() :
                modLocalization.ModifierName(id);
        }

        private string LocalizedChallengeLabel(ModifierId id)
        {
            return modLocalization == null ? string.Empty :
                modLocalization.ChallengeLabel(id);
        }

        private string LocalizedEquipmentName(EquipmentEntry<Weapon> entry)
        {
            if (entry.Value == Weapon.None)
                return L(ModText.CommonNone);
            try
            {
                var value = WeaponProperties.GetDisplayName(entry.Value);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            catch
            {
            }
            return entry.Name;
        }

        private string LocalizedEquipmentName(EquipmentEntry<Super> entry)
        {
            if (entry.Value == Super.None)
                return L(ModText.CommonNone);
            try
            {
                var value = WeaponProperties.GetDisplayName(entry.Value);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            catch
            {
            }
            return entry.Name;
        }

        private string LocalizedEquipmentName(EquipmentEntry<Charm> entry)
        {
            if (entry.Value == Charm.None)
                return L(ModText.CommonNone);
            if (entry.Value == Charm.charm_curse)
                return entry.CurseLevelOverride >= 4
                    ? L(ModText.CharmDivineRelic)
                    : L(ModText.CharmCursedRelic);
            try
            {
                var value = WeaponProperties.GetDisplayName(entry.Value);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            catch
            {
            }
            return entry.Name;
        }

        private void OnModLanguageChanged()
        {
            // Every visible string is resolved while drawing. Reset the one
            // cached native layout so its measured width is rebuilt too.
            nativeRoulettePromptLayoutToken = null;
        }

        private void UpdateLanguageTestShortcut()
        {
            if (!EnableLanguageTestShortcut ||
                (!LanguageTestLeftShortcut.IsDown() &&
                 !LanguageTestRightShortcut.IsDown()))
                return;

            Localization.Languages current;
            try
            {
                current = Localization.language;
            }
            catch
            {
                return;
            }

            if (!languageTestOriginalCaptured)
            {
                languageTestOriginalLanguage = current;
                languageTestOriginalCaptured = true;
            }

            var languages = (Localization.Languages[])Enum.GetValues(
                typeof(Localization.Languages));
            languageTestCycleIndex =
                (languageTestCycleIndex + 1) % languages.Length;
            var nextIndex = languageTestCycleIndex;
            var next = languages[nextIndex];
            Localization.language = next;
            languageTestNoticeUntil = Time.realtimeSinceStartup + 3f;
            Logger.LogWarning("TEMP language test: " + next +
                " (Ctrl+F8 cycles; original=" +
                languageTestOriginalLanguage + ").");
        }

        private void RestoreOriginalTestLanguage()
        {
            if (!languageTestOriginalCaptured)
                return;

            try
            {
                if (Localization.language != languageTestOriginalLanguage)
                    Localization.language = languageTestOriginalLanguage;
            }
            catch
            {
            }
            languageTestOriginalCaptured = false;
            languageTestCycleIndex = -1;
        }

        private void DrawLanguageTestNotice()
        {
            if (!EnableLanguageTestShortcut ||
                Time.realtimeSinceStartup >= languageTestNoticeUntil)
                return;

            var language = modLocalization == null
                ? "UNKNOWN"
                : modLocalization.CurrentLanguage.ToString().ToUpperInvariant();
            var rect = new Rect(365f, 12f, 550f, 44f);
            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, "IDIOMA DE PRUEBA: " + language +
                "  ·  CTRL+F8", subtitleStyle);
            GUI.color = previousColor;
        }

        private void Awake()
        {
            modLocalization = new ModLocalization();
            modLocalization.LanguageChanged += OnModLanguageChanged;
            toggleShortcut = Config.Bind("Controles", "AbrirCerrar", new KeyboardShortcut(KeyCode.F6), "Abre o cierra la ruleta.");
            spinShortcut = Config.Bind("Controles", "Girar", new KeyboardShortcut(KeyCode.F7), "Inicia un giro.");
            autoLoad = Config.Bind("Juego", "CargarAutomaticamente", true, "Carga el jefe al finalizar el giro.");
            difficultySetting = Config.Bind("Juego", "Dificultad", Level.Mode.Normal,
                "Dificultad usada por la ruleta: Easy, Normal o Hard.");
            challengeSetting = Config.Bind("Juego", "Reto", false,
                "Activa los retos adicionales de la ruleta.");
            loadDelay = Config.Bind("Juego", "DemoraAntesDeCargar", 1.25f, "Segundos entre el resultado final y la carga.");
            difficulty = difficultySetting.Value == Level.Mode.Easy ||
                         difficultySetting.Value == Level.Mode.Hard
                ? difficultySetting.Value
                : Level.Mode.Normal;
            uglyMode = HasForcedTestChallenge() ||
                       ForcePlaneRelicChallengeTestSequence ||
                       challengeSetting.Value;
            theme = new GameTheme();
            LoadBlackAndWhiteTransitionShader();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = SpinAudioVolume;
            audioSource.spatialBlend = 0f;
            audioSource.priority = 0;
            audioSource.ignoreListenerPause = true;
            effectsAudioSource = gameObject.AddComponent<AudioSource>();
            effectsAudioSource.playOnAwake = false;
            effectsAudioSource.volume = 1f;
            effectsAudioSource.spatialBlend = 0f;
            // Keep the continuous roulette loop above transient UI voices if
            // Unity ever needs to virtualize one of the sources.
            effectsAudioSource.priority = 64;
            effectsAudioSource.ignoreListenerPause = true;
            RouteModAudioToGameSfxMixer();
            activeInstance = this;
            harmony = new Harmony(PluginGuid);
            var mapPauseCanPause = AccessTools.Method(typeof(MapPauseUI), "get_CanPause");
            var mapPausePostfix = AccessTools.Method(typeof(Plugin), "BlockMapPausePostfix");
            if (mapPauseCanPause != null && mapPausePostfix != null)
                harmony.Patch(mapPauseCanPause, postfix: new HarmonyMethod(mapPausePostfix));
            else
                Logger.LogWarning("Could not install the map pause guard.");

            var mapPlayerCanMove = AccessTools.Method(
                typeof(MapPlayerController), "CanMove");
            var blockMapMovementPostfix = AccessTools.Method(
                typeof(Plugin), "BlockMapMovementPostfix");
            if (mapPlayerCanMove != null && blockMapMovementPostfix != null)
                harmony.Patch(mapPlayerCanMove,
                    postfix: new HarmonyMethod(blockMapMovementPostfix));
            else
                Logger.LogWarning(
                    "Could not install the roulette map movement guard.");

            var mapInteractiveUpdate = AccessTools.Method(
                typeof(AbstractMapInteractiveEntity), "Update");
            var blockMapInteractionPrefix = AccessTools.Method(
                typeof(Plugin), "BlockMapInteractionPrefix");
            if (mapInteractiveUpdate != null &&
                blockMapInteractionPrefix != null)
                harmony.Patch(mapInteractiveUpdate,
                    prefix: new HarmonyMethod(blockMapInteractionPrefix));
            else
                Logger.LogWarning(
                    "Could not install the roulette map interaction guard.");

            var mapCreatePlayers = AccessTools.Method(
                typeof(Map), "CreatePlayers");
            var applyRouletteReturnDestinationPrefix = AccessTools.Method(
                typeof(Plugin), "ApplyRouletteReturnDestinationPrefix");
            if (mapCreatePlayers != null &&
                applyRouletteReturnDestinationPrefix != null)
                harmony.Patch(mapCreatePlayers,
                    prefix: new HarmonyMethod(
                        applyRouletteReturnDestinationPrefix));
            else
                Logger.LogWarning(
                    "Could not install the roulette boss-door return guard.");

            if (ForceFiveSuperCardsForHudTest)
            {
                var superChanged = AccessTools.Method(
                    typeof(LevelHUDPlayerSuper), "OnSuperChanged",
                    new[] { typeof(float) });
                var forceFiveSuperCardsPrefix = AccessTools.Method(
                    typeof(Plugin), "ForceFiveSuperCardsForHudTestPrefix");
                if (superChanged != null &&
                    forceFiveSuperCardsPrefix != null)
                {
                    harmony.Patch(superChanged,
                        prefix: new HarmonyMethod(
                            forceFiveSuperCardsPrefix));
                    Logger.LogWarning(
                        "TEMP HUD test active: both players show five super cards; real meters are unchanged.");
                }
                else
                    Logger.LogWarning(
                        "Could not install the temporary five-card HUD test.");
            }

            var filterGetter = AccessTools.PropertyGetter(
                typeof(SettingsData), "filter");
            var overrideBlackAndWhiteFilterPostfix = AccessTools.Method(
                typeof(Plugin), "OverrideBlackAndWhiteFilterPostfix");
            if (filterGetter != null &&
                overrideBlackAndWhiteFilterPostfix != null)
                harmony.Patch(filterGetter, postfix: new HarmonyMethod(
                    overrideBlackAndWhiteFilterPostfix));
            else
                Logger.LogWarning(
                    "Could not install the black-and-white filter bridge.");

            var levelPreWin = AccessTools.Method(typeof(Level), "_OnPreWin");
            var levelPreWinPrefix = AccessTools.Method(typeof(Plugin), "ClearChallengeOnWinPrefix");
            if (levelPreWin != null && levelPreWinPrefix != null)
                harmony.Patch(levelPreWin, prefix: new HarmonyMethod(levelPreWinPrefix));
            else
                Logger.LogWarning("Could not install the challenge win guard.");

            var baseGameEndingLoad = AccessTools.Method(
                typeof(Cutscene), "Load", new[]
                {
                    typeof(Scenes), typeof(Scenes),
                    typeof(SceneLoader.Transition),
                    typeof(SceneLoader.Transition),
                    typeof(SceneLoader.Icon)
                });
            var returnRouletteFinalBossWinToMapPrefix = AccessTools.Method(
                typeof(Plugin), "ReturnRouletteFinalBossWinToMapPrefix");
            if (baseGameEndingLoad != null &&
                returnRouletteFinalBossWinToMapPrefix != null)
                harmony.Patch(baseGameEndingLoad,
                    prefix: new HarmonyMethod(
                        returnRouletteFinalBossWinToMapPrefix));
            else
                Logger.LogWarning(
                    "Could not install the roulette final-boss ending bypass.");


            var loadLastMap = AccessTools.Method(typeof(SceneLoader), "LoadLastMap");
            var restoreLoadoutBeforeReturnToMapPrefix = AccessTools.Method(
                typeof(Plugin), "RestoreLoadoutBeforeReturnToMapPrefix");
            if (loadLastMap != null && restoreLoadoutBeforeReturnToMapPrefix != null)
                harmony.Patch(loadLastMap,
                    prefix: new HarmonyMethod(restoreLoadoutBeforeReturnToMapPrefix));
            else
                Logger.LogWarning(
                    "Could not install the roulette loadout restoration guard.");

            var changeEquipmentAfterDefeat = AccessTools.Method(
                typeof(LevelGameOverGUI), "ChangeEquipment");
            var blockEquipmentAfterRouletteDefeatPrefix = AccessTools.Method(
                typeof(Plugin), "BlockEquipmentAfterRouletteDefeatPrefix");
            if (changeEquipmentAfterDefeat != null &&
                blockEquipmentAfterRouletteDefeatPrefix != null)
                harmony.Patch(changeEquipmentAfterDefeat,
                    prefix: new HarmonyMethod(
                        blockEquipmentAfterRouletteDefeatPrefix));
            else
                Logger.LogWarning(
                    "Could not install the roulette defeat equipment guard.");

            var handleDash = AccessTools.Method(typeof(LevelPlayerMotor), "HandleDash");
            var handleDashPrefix = AccessTools.Method(typeof(Plugin), "BlockDashPrefix");
            if (handleDash != null && handleDashPrefix != null)
                harmony.Patch(handleDash, prefix: new HarmonyMethod(handleDashPrefix));
            else
                Logger.LogWarning("Could not install the No Dash guard.");

            var canUseEx = AccessTools.PropertyGetter(
                typeof(PlayerStatsManager), "CanUseEx");
            var canUseExPostfix = AccessTools.Method(
                typeof(Plugin), "CanUseExPostfix");
            var planeStartEx = AccessTools.Method(
                typeof(PlanePlayerWeaponManager), "StartEx");
            var blockPlaneExPrefix = AccessTools.Method(
                typeof(Plugin), "BlockPlaneExPrefix");
            if (canUseEx != null && canUseExPostfix != null &&
                planeStartEx != null && blockPlaneExPrefix != null)
            {
                harmony.Patch(canUseEx,
                    postfix: new HarmonyMethod(canUseExPostfix));
                harmony.Patch(planeStartEx,
                    prefix: new HarmonyMethod(blockPlaneExPrefix));
            }
            else
                Logger.LogWarning("Could not install the No EX guard.");

            var handleShrunk = AccessTools.Method(
                typeof(PlanePlayerAnimationController), "HandleShrunk");
            var blockMiniPlanePrefix = AccessTools.Method(
                typeof(Plugin), "BlockMiniPlanePrefix");
            if (handleShrunk != null && blockMiniPlanePrefix != null)
                harmony.Patch(handleShrunk,
                    prefix: new HarmonyMethod(blockMiniPlanePrefix));
            else
                Logger.LogWarning("Could not install the No mini airplane guard.");

            var dealDamage = AccessTools.Method(
                typeof(DamageDealer), "DealDamage",
                new[] { typeof(GameObject) });
            var restartSoloMiniOnInvalidDamagePostfix = AccessTools.Method(
                typeof(Plugin), "RestartSoloMiniOnInvalidDamagePostfix");
            if (dealDamage != null &&
                restartSoloMiniOnInvalidDamagePostfix != null)
                harmony.Patch(dealDamage,
                    postfix: new HarmonyMethod(
                        restartSoloMiniOnInvalidDamagePostfix));
            else
                Logger.LogWarning("Could not install the Solo mini airplane damage guard.");

            var planeWeaponStart = AccessTools.Method(
                typeof(PlanePlayerWeaponManager), "OnLevelStart");
            var planeWeaponStartPostfix = AccessTools.Method(
                typeof(Plugin), "EnforcePlaneStartingWeaponPostfix");
            var handlePlaneWeaponSwitch = AccessTools.Method(
                typeof(PlanePlayerWeaponManager), "HandleWeaponSwitch");
            var blockPlaneWeaponSwitchPrefix = AccessTools.Method(
                typeof(Plugin), "BlockPlaneWeaponSwitchPrefix");
            if (planeWeaponStart != null && planeWeaponStartPostfix != null &&
                handlePlaneWeaponSwitch != null &&
                blockPlaneWeaponSwitchPrefix != null)
            {
                harmony.Patch(planeWeaponStart,
                    postfix: new HarmonyMethod(planeWeaponStartPostfix));
                harmony.Patch(handlePlaneWeaponSwitch,
                    prefix: new HarmonyMethod(blockPlaneWeaponSwitchPrefix));
            }
            else
                Logger.LogWarning("Could not install the No airplane bombs guard.");

            var switchPlaneWeapon = AccessTools.Method(
                typeof(PlanePlayerWeaponManager), "SwitchWeapon",
                new[] { typeof(Weapon) });
            var enforcePlaneWeaponRestrictionPrefix = AccessTools.Method(
                typeof(Plugin), "EnforcePlaneWeaponRestrictionPrefix");
            if (switchPlaneWeapon != null &&
                enforcePlaneWeaponRestrictionPrefix != null)
                harmony.Patch(switchPlaneWeapon,
                    prefix: new HarmonyMethod(
                        enforcePlaneWeaponRestrictionPrefix));
            else
                Logger.LogWarning(
                    "Could not install the cursed relic airplane weapon guard.");

            InstallCurseRelicLevelOverridePatches();

            StartCoroutine(LoadAudio());
            Logger.LogInfo(PluginName + " " + PluginVersion +
                           " listo. F6 o gatillo izquierdo + Equip abre/cierra; F7 gira.");
        }

        private static void ForceFiveSuperCardsForHudTestPrefix(
            LevelHUDPlayerSuper __instance, ref float __0)
        {
            if (!ForceFiveSuperCardsForHudTest || __instance == null)
                return;

            var hud = Traverse.Create(__instance)
                .Property("_hud").GetValue<LevelHUDPlayer>();
            var player = hud != null ? hud.player : null;
            var stats = player != null ? player.stats : null;
            if (stats != null)
            {
                // Feed only the native HUD renderer its own maximum. The real
                // PlayerStatsManager.SuperMeter value is never modified.
                __0 = stats.SuperMeterMax;
            }
        }

        private void InstallCurseRelicLevelOverridePatches()
        {
            var calculateLevel = AccessTools.Method(
                typeof(CharmCurse), "CalculateLevel",
                new[] { typeof(PlayerId) });
            var overrideLevelPostfix = AccessTools.Method(
                typeof(Plugin), "OverrideSelectedCurseRelicLevelPostfix");
            var beginSetupPrefix = AccessTools.Method(
                typeof(Plugin), "BeginCurseRelicRuntimeSetupPrefix");
            var endSetupFinalizer = AccessTools.Method(
                typeof(Plugin), "EndCurseRelicRuntimeSetupFinalizer");

            if (calculateLevel == null || overrideLevelPostfix == null ||
                beginSetupPrefix == null || endSetupFinalizer == null)
            {
                Logger.LogWarning(
                    "Could not install the cursed/divine relic level override.");
                return;
            }

            harmony.Patch(calculateLevel,
                postfix: new HarmonyMethod(overrideLevelPostfix));

            var runtimeSetupMethods = new[]
            {
                AccessTools.Method(typeof(PlayerStatsManager), "LevelInit"),
                AccessTools.Method(typeof(LevelPlayerAnimationController),
                    "Start"),
                AccessTools.Method(typeof(PlanePlayerAnimationController),
                    "Start")
            };
            for (var i = 0; i < runtimeSetupMethods.Length; i++)
            {
                var method = runtimeSetupMethods[i];
                if (method == null)
                {
                    Logger.LogWarning(
                        "Could not find a relic runtime setup method at index " +
                        i + ".");
                    continue;
                }

                harmony.Patch(method,
                    prefix: new HarmonyMethod(beginSetupPrefix),
                    finalizer: new HarmonyMethod(endSetupFinalizer));
            }
        }

        private static void BeginCurseRelicRuntimeSetupPrefix()
        {
            curseRelicRuntimeSetupDepth++;
        }

        private static void EndCurseRelicRuntimeSetupFinalizer()
        {
            if (curseRelicRuntimeSetupDepth > 0)
                curseRelicRuntimeSetupDepth--;
        }

        private static void OverrideSelectedCurseRelicLevelPostfix(
            ref int __result)
        {
            if (curseRelicRuntimeSetupDepth <= 0)
                return;

            var plugin = activeInstance;
            int forcedLevel;
            if (plugin != null &&
                plugin.TryGetSelectedCurseRelicLevel(out forcedLevel))
                __result = forcedLevel;
        }

        private bool TryGetSelectedCurseRelicLevel(out int level)
        {
            level = EquipmentEntry<Charm>.NoCurseLevelOverride;
            if (!loanedLoadoutsActive || result == null || result.Charm < 0 ||
                result.Charm >= RouletteData.Charms.Length)
                return false;

            var selectedCharm = RouletteData.Charms[result.Charm];
            if (selectedCharm.Value != Charm.charm_curse ||
                selectedCharm.CurseLevelOverride < 0)
                return false;

            level = selectedCharm.CurseLevelOverride;
            return true;
        }

        private void LoadBlackAndWhiteTransitionShader()
        {
            var bundlePath = Path.Combine(
                AssetsDirectory,
                Path.Combine("shaders", "gilomx-boss-roulette-shaders"));
            if (!File.Exists(bundlePath))
            {
                Logger.LogWarning(
                    "No se encontró el bundle del shader: " + bundlePath);
                return;
            }

            blackAndWhiteShaderBundle = AssetBundle.LoadFromFile(bundlePath);
            if (blackAndWhiteShaderBundle == null)
            {
                Logger.LogWarning(
                    "Unity no pudo cargar el bundle del shader blanco y negro.");
                return;
            }

            blackAndWhiteTransitionShader =
                blackAndWhiteShaderBundle.LoadAsset<Shader>(
                    "Assets/BossRouletteSaturation.shader");
            if (blackAndWhiteTransitionShader == null)
                Logger.LogWarning(
                    "El bundle no contiene el shader de saturación de cámara esperado.");
            else
                Logger.LogInfo(
                    "Shader suave blanco y negro cargado desde AssetBundle.");

            battleHudSaturationShader =
                blackAndWhiteShaderBundle.LoadAsset<Shader>(
                    "Assets/BossRouletteUiSaturation.shader");
            if (battleHudSaturationShader == null)
                Logger.LogWarning(
                    "El bundle no contiene el shader de saturación para el HUD.");
        }

        private static void BlockMapPausePostfix(ref bool __result)
        {
            var plugin = activeInstance;
            if (plugin != null &&
                (plugin.visible ||
                 Time.frameCount <= plugin.suppressMapPauseUntilFrame ||
                 plugin.IsControllerToggleModifierHeld()))
                __result = false;
        }

        private static void BlockMapMovementPostfix(ref bool __result)
        {
            var plugin = activeInstance;
            if (plugin != null && plugin.visible)
                __result = false;
        }

        private static bool BlockMapInteractionPrefix()
        {
            var plugin = activeInstance;
            return plugin == null ||
                   (!plugin.visible && plugin.cardVisibility <= 0.001f);
        }

        private static void ApplyRouletteReturnDestinationPrefix()
        {
            var plugin = activeInstance;
            if (plugin != null)
                plugin.ApplyRouletteReturnDestination();
        }

        private static void OverrideBlackAndWhiteFilterPostfix(
            ref BlurGamma.Filter __result)
        {
            var plugin = activeInstance;
            if (plugin != null && plugin.blackAndWhiteNativeBaseActive)
                __result = BlurGamma.Filter.BW;
        }

        private bool IsControllerTogglePressed()
        {
            try
            {
                return IsControllerTogglePressed(PlayerId.PlayerOne) ||
                       IsControllerTogglePressed(PlayerId.PlayerTwo);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsControllerTogglePressed(PlayerId playerId)
        {
            var player = PlayerManager.GetPlayerInput(playerId);
            return player != null &&
                   player.GetButtonDown((int)CupheadButton.EquipMenu) &&
                   IsLeftTriggerHeld(player);
        }

        private bool IsControllerToggleModifierHeld()
        {
            try
            {
                return IsLeftTriggerHeld(PlayerManager.GetPlayerInput(PlayerId.PlayerOne)) ||
                       IsLeftTriggerHeld(PlayerManager.GetPlayerInput(PlayerId.PlayerTwo));
            }
            catch
            {
                return false;
            }
        }

        private bool IsControllerMenuButtonDown(CupheadButton button)
        {
            try
            {
                return IsControllerMenuButtonDown(PlayerId.PlayerOne, button) ||
                       IsControllerMenuButtonDown(PlayerId.PlayerTwo, button);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsControllerMenuButtonDown(PlayerId playerId, CupheadButton button)
        {
            var player = PlayerManager.GetPlayerInput(playerId);
            return player != null && player.GetButtonDown((int)button);
        }

        private bool PollControllerRerollPressed()
        {
            var held = false;
            try
            {
                held = IsRightTriggerHeld(PlayerManager.GetPlayerInput(PlayerId.PlayerOne)) ||
                       IsRightTriggerHeld(PlayerManager.GetPlayerInput(PlayerId.PlayerTwo));
            }
            catch
            {
                held = false;
            }

            var pressed = held && !rightTriggerWasHeld;
            rightTriggerWasHeld = held;
            return pressed;
        }

        private static bool IsLeftTriggerHeld(Rewired.Player player)
        {
            if (player == null || player.controllers == null)
                return false;

            foreach (var joystick in player.controllers.Joysticks)
            {
                if (joystick == null)
                    continue;

                foreach (var element in joystick.ElementIdentifiers)
                {
                    if (element == null)
                        continue;

                    var direct = IsLeftTriggerLabel(element.name);
                    var positive = IsLeftTriggerLabel(element.positiveName);
                    var negative = IsLeftTriggerLabel(element.negativeName);
                    if (!direct && !positive && !negative)
                        continue;

                    if (element.elementType == ControllerElementType.Button)
                    {
                        if (joystick.GetButtonById(element.id))
                            return true;
                        continue;
                    }

                    if (element.elementType != ControllerElementType.Axis)
                        continue;

                    var value = joystick.GetAxisById(element.id);
                    if ((direct || positive) && value > 0.5f)
                        return true;
                    if (negative && value < -0.5f)
                        return true;
                }
            }
            return false;
        }

        private static bool IsRightTriggerHeld(Rewired.Player player)
        {
            if (player == null || player.controllers == null)
                return false;

            foreach (var joystick in player.controllers.Joysticks)
            {
                if (joystick == null)
                    continue;

                foreach (var element in joystick.ElementIdentifiers)
                {
                    if (element == null)
                        continue;

                    var direct = IsRightTriggerLabel(element.name);
                    var positive = IsRightTriggerLabel(element.positiveName);
                    var negative = IsRightTriggerLabel(element.negativeName);
                    if (!direct && !positive && !negative)
                        continue;

                    if (element.elementType == ControllerElementType.Button)
                    {
                        if (joystick.GetButtonById(element.id))
                            return true;
                        continue;
                    }

                    if (element.elementType != ControllerElementType.Axis)
                        continue;

                    var value = joystick.GetAxisById(element.id);
                    if ((direct || positive) && value > 0.5f)
                        return true;
                    if (negative && value < -0.5f)
                        return true;
                }
            }
            return false;
        }

        private static bool IsLeftTriggerLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return false;

            var normalized = "";
            foreach (var character in label.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                    normalized += character;
            }

            return normalized.Contains("lefttrigger") ||
                   normalized.Contains("triggerleft") ||
                   normalized == "l2" || normalized.EndsWith("l2") ||
                   normalized == "zl" || normalized.EndsWith("zl");
        }

        private static bool IsRightTriggerLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return false;

            var normalized = "";
            foreach (var character in label.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                    normalized += character;
            }

            return normalized.Contains("righttrigger") ||
                   normalized.Contains("triggerright") ||
                   normalized == "r2" || normalized.EndsWith("r2") ||
                   normalized == "zr" || normalized.EndsWith("zr");
        }

        private bool CanUseRouletteOnMap()
        {
            try
            {
                if (!PlayerData.Initialized || PlayerData.Data == null ||
                    SceneLoader.CurrentlyLoading || Map.Current == null ||
                    Convert.ToInt32(Map.Current.CurrentState) != 1 ||
                    Convert.ToInt32(PauseManager.state) != 0)
                    return false;

                var equipUi = AbstractEquipUI.Current;
                if (equipUi != null &&
                    (Convert.ToInt32(equipUi.CurrentState) != 0 ||
                     Convert.ToInt32(equipUi.state) != 0))
                    return false;

                var difficultyUi = MapDifficultySelectStartUI.Current;
                var confirmUi = MapConfirmStartUI.Current;
                var basicUi = MapBasicStartUI.Current;
                return (difficultyUi == null || Convert.ToInt32(difficultyUi.CurrentState) == 0) &&
                       (confirmUi == null || Convert.ToInt32(confirmUi.CurrentState) == 0) &&
                       (basicUi == null || Convert.ToInt32(basicUi.CurrentState) == 0);
            }
            catch
            {
                return false;
            }
        }

        private void Update()
        {
            UpdateLanguageTestShortcut();
            UpdateLoanedLoadoutLifecycle();
            UpdateActiveChallengeLifecycle();
            UpdateBlackAndWhiteTransition();
            UpdateBlackAndWhiteRenderEffects();
            var controllerRerollPressed = PollControllerRerollPressed();
            var onMap = CanUseRouletteOnMap();
            if (!onMap)
            {
                if (visible)
                    SetVisible(false);
                if (running)
                {
                    running = false;
                    StopSpinAudio();
                }
                pendingLoad = false;
                resultReady = false;
            }

            var visibilityTarget = visible ? 1f : 0f;
            cardVisibility = Mathf.Lerp(cardVisibility, visibilityTarget, Time.unscaledDeltaTime * 10f);
            if (Mathf.Abs(cardVisibility - visibilityTarget) < 0.001f)
                cardVisibility = visibilityTarget;

            if (onMap &&
                (toggleShortcut.Value.IsDown() || IsControllerTogglePressed()))
                SetVisible(!visible);
            if (onMap && visible && !autoLoad.Value && resultReady &&
                !running && !pendingLoad &&
                (spinShortcut.Value.IsDown() || controllerRerollPressed))
                StartRoulette();
            if (visible)
                SetNativeMapEquipEnabled(false);
            if (visible && cardVisibility > 0.72f && !running && !pendingLoad)
                HandleCardNavigation();
            if (running)
                UpdateSpin();
            if (pendingLoad && Time.realtimeSinceStartup >= loadAt)
            {
                if (visible)
                {
                    SetVisible(false);
                    loadAt = Time.realtimeSinceStartup + 0.43f;
                }
                else if (cardVisibility <= 0.01f)
                {
                    pendingLoad = false;
                    LoadResult();
                }
            }
        }

        private void HandleCardNavigation()
        {
            if (Input.GetKeyDown(KeyCode.Escape) ||
                IsControllerMenuButtonDown(CupheadButton.Cancel))
            {
                suppressMapPauseUntilFrame = Time.frameCount;
                SetVisible(false);
                return;
            }

            var moved = false;
            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                IsControllerMenuButtonDown(CupheadButton.MenuUp))
            {
                navigationIndex = Wrap(navigationIndex - 1, 4);
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) ||
                     IsControllerMenuButtonDown(CupheadButton.MenuDown))
            {
                navigationIndex = Wrap(navigationIndex + 1, 4);
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) ||
                     IsControllerMenuButtonDown(CupheadButton.MenuLeft))
            {
                ChangeCurrentSetting(-1);
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) ||
                     IsControllerMenuButtonDown(CupheadButton.MenuRight))
            {
                ChangeCurrentSetting(1);
                moved = true;
            }

            if (moved)
                PlayNativeMenuSound("menu_equipment_move", selectionClip, 0.45f);

            if (!Input.GetKeyDown(KeyCode.Return) &&
                !Input.GetKeyDown(KeyCode.KeypadEnter) &&
                !IsControllerMenuButtonDown(CupheadButton.Accept))
                return;

            if (navigationIndex == 3)
            {
                if (resultReady)
                    BeginResultLoad();
                else
                    StartRoulette();
            }
            else
                ChangeCurrentSetting(1);
            PlayNativeMenuSound("menu_equipment_move", selectionClip, 0.65f);
        }

        private void BeginResultLoad()
        {
            if (running || pendingLoad || !resultReady)
                return;
            pendingLoad = true;
            loadAt = Time.realtimeSinceStartup;
        }
        private void ChangeCurrentSetting(int direction)
        {
            var changed = false;
            if (navigationIndex == 0)
            {
                var modes = new[] { Level.Mode.Easy, Level.Mode.Normal, Level.Mode.Hard };
                var current = difficulty == Level.Mode.Easy ? 0 : difficulty == Level.Mode.Hard ? 2 : 1;
                var nextDifficulty = modes[Wrap(current + direction, modes.Length)];
                if (difficulty != nextDifficulty)
                {
                    difficulty = nextDifficulty;
                    difficultySetting.Value = difficulty;
                    changed = true;
                }
            }
            else if (navigationIndex == 1)
            {
                uglyMode = !uglyMode;
                challengeSetting.Value = uglyMode;
                changed = true;
            }
            else if (navigationIndex == 2)
            {
                autoLoad.Value = !autoLoad.Value;
                changed = true;
            }

            if (!changed)
                return;

            Config.Save();
            if (resultReady)
            {
                resultReady = false;
                status = RouletteStatus.Ready;
            }
        }

        private void FindNativeMapEquipUi()
        {
            if (nativeMapEquipUi == null)
                nativeMapEquipUi = UnityEngine.Object.FindObjectOfType<MapEquipUI>();
        }

        private void SetNativeMapEquipEnabled(bool enabled)
        {
            FindNativeMapEquipUi();
            if (nativeMapEquipUi != null && nativeMapEquipUi.enabled != enabled)
                nativeMapEquipUi.enabled = enabled;
        }

        private IEnumerator RestoreNativeMapEquipNextFrame()
        {
            yield return null;
            if (!visible)
                SetNativeMapEquipEnabled(true);
        }
        private void SetVisible(bool value)
        {
            if (visible == value)
                return;
            if (value)
                RefreshAvailableContent();
            else if (running)
                CancelRouletteSpin();
            visible = value;
            if (visible)
            {
                navigationIndex = 3;
                cardRoll = random.Next(-4, 5);
                SetNativeMapEquipEnabled(false);
            }
            else
            {
                StartCoroutine(RestoreNativeMapEquipNextFrame());
            }
            PlayNativeMenuSound(visible ? "menu_cardup" : "menu_carddown",
                visible ? openClip : closeClip, 0.65f);
        }

        private void CancelRouletteSpin()
        {
            running = false;
            pendingLoad = false;
            resultReady = false;
            spinStartedAt = 0f;
            loadAt = 0f;
            ticker = 0;
            revealed = 0;
            result = new RouletteResult();
            status = RouletteStatus.Ready;

            for (var i = 0; i < pulseUntil.Length; i++)
                pulseUntil[i] = 0f;

            StopSpinAudio();
            if (effectsAudioSource != null)
                effectsAudioSource.Stop();
            Logger.LogInfo("Giro cancelado al cerrar la ruleta.");
        }

        private void StartRoulette()
        {
            if (!CanUseRouletteOnMap() || running || pendingLoad)
                return;
            RefreshAvailableContent();
            if (HasForcedTestChallenge())
                uglyMode = true;
            if (ForcePlaneRelicChallengeTestSequence)
                uglyMode = true;
            if (!visible)
                SetVisible(true);

            resultReady = false;
            result = CreateRandomResult();
            revealed = 0;
            ticker = 0;
            spinStartedAt = Time.realtimeSinceStartup;
            running = true;
            status = RouletteStatus.Spinning;
            EndBattleResultHudSession();
            ClearActiveChallenge();
            if (spinClip != null)
            {
                audioSource.clip = spinClip;
                audioSource.loop = true;
                audioSource.volume = SpinAudioVolume;
                audioSource.time = 0f;
                audioSource.Play();
            }
            else
                Logger.LogWarning("El audio de giro no esta disponible.");
        }

        private RouletteResult CreateRandomResult()
        {
            EnsureAvailableContent();
            var forcedModifier =
                ForcedPlaneRelicChallengeModifierIndex();
            if (forcedModifier < 0)
                forcedModifier = ForcedTestModifierIndex();
            var forcedBoss = ForcedTestBossIndex();
            var boss = forcedBoss >= 0
                ? forcedBoss
                : forcedModifier >= 0
                    ? RandomBossForModifier(forcedModifier)
                    : RandomPoolIndex(availableBossIndices);
            var weapon1 = RandomNonEmptyPoolIndex(
                availableWeaponIndices, RouletteData.Weapons.Length - 1);
            var emptyWeaponIndex = RouletteData.Weapons.Length - 1;
            int weapon2;
            if (random.NextDouble() < 0.2)
                weapon2 = emptyWeaponIndex;
            else
            {
                do weapon2 = RandomNonEmptyPoolIndex(
                    availableWeaponIndices, emptyWeaponIndex);
                while (weapon2 == weapon1);
            }

            var super = random.NextDouble() < 0.2
                ? RouletteData.Supers.Length - 1
                : RandomNonEmptyPoolIndex(
                    availableSuperIndices, RouletteData.Supers.Length - 1);
            var forcedCharm = ForcedRelicTestCharmIndex();
            var charm = forcedCharm >= 0
                ? forcedCharm
                : random.NextDouble() < 0.2
                    ? RouletteData.Charms.Length - 1
                    : RandomNonEmptyPoolIndex(
                        availableCharmIndices, RouletteData.Charms.Length - 1);

            var modifier = forcedModifier >= 0
                ? forcedModifier
                : RouletteData.Modifiers.Length - 1;

            if (uglyMode && forcedModifier < 0)
            {
                var valid = RouletteData.ValidModifierIndices(RouletteData.Bosses[boss]);
                if (valid.Count > 0)
                    modifier = valid[random.Next(valid.Count)];
            }

            return new RouletteResult
            {
                Boss = boss,
                Weapon1 = weapon1,
                Weapon2 = weapon2,
                Super = super,
                Charm = charm,
                Modifier = modifier
            };
        }

        private int ForcedPlaneRelicChallengeModifierIndex()
        {
            if (!ForcePlaneRelicChallengeTestSequence)
                return -1;

            var testSpin = forcedPlaneRelicChallengeTestSpin++ % 4;
            var expectedId = testSpin < 2
                ? ModifierId.NoBombs
                : ModifierId.NoPeashooter;
            for (var i = 0; i < RouletteData.Modifiers.Length; i++)
            {
                if (RouletteData.Modifiers[i].Id == expectedId)
                {
                    Logger.LogInfo(
                        "Forced plane relic challenge: " + expectedId +
                        ".");
                    return i;
                }
            }

            return -1;
        }

        private int ForcedTestBossIndex()
        {
            if (!ForceTestBoss || ForcedTestBossSequence.Length == 0)
                return -1;

            var forcedLevel = ForcedTestBossSequence[forcedBossTestSpin];
            forcedBossTestSpin =
                (forcedBossTestSpin + 1) % ForcedTestBossSequence.Length;
            for (var i = 0; i < availableBossIndices.Count; i++)
            {
                var bossIndex = availableBossIndices[i];
                var boss = RouletteData.Bosses[bossIndex];
                if (boss.Level == forcedLevel)
                {
                    Logger.LogInfo(
                        "Forced test boss: " + boss.Character + ".");
                    return bossIndex;
                }
            }

            Logger.LogWarning(
                "Could not force test boss " + forcedLevel +
                " because it is unavailable.");
            return -1;
        }

        private int ForcedRelicTestCharmIndex()
        {
            if (!ForceRelicTestSequence)
                return -1;

            var expectedCurseLevel = forcedRelicTestSpin++ % 2 == 0 ? 0 : 4;
            for (var i = 0; i < availableCharmIndices.Count; i++)
            {
                var charmIndex = availableCharmIndices[i];
                if (RouletteData.Charms[charmIndex].CurseLevelOverride ==
                    expectedCurseLevel)
                {
                    Logger.LogInfo(
                        "Forced relic test curse level: " +
                        expectedCurseLevel + ".");
                    return charmIndex;
                }
            }

            return -1;
        }

        private static bool HasForcedTestChallenge()
        {
            return ForcedTestChallenge != ModifierId.None;
        }

        private static int ForcedTestModifierIndex()
        {
            if (!HasForcedTestChallenge())
                return -1;

            for (var i = 0; i < RouletteData.Modifiers.Length; i++)
            {
                if (RouletteData.Modifiers[i].Id == ForcedTestChallenge)
                    return i;
            }
            return -1;
        }

        private int RandomBossForModifier(int modifierIndex)
        {
            var compatibleBosses = new List<int>();
            for (var i = 0; i < availableBossIndices.Count; i++)
            {
                var bossIndex = availableBossIndices[i];
                if (RouletteData.ValidModifierIndices(
                    RouletteData.Bosses[bossIndex]).Contains(modifierIndex))
                    compatibleBosses.Add(bossIndex);
            }

            return compatibleBosses.Count > 0
                ? compatibleBosses[random.Next(compatibleBosses.Count)]
                : RandomPoolIndex(availableBossIndices);
        }

        private void EnsureAvailableContent()
        {
            if (availableBossIndices.Count == 0 ||
                availableWeaponIndices.Count == 0 ||
                availableSuperIndices.Count == 0 ||
                availableCharmIndices.Count == 0)
                RefreshAvailableContent();
        }

        private void RefreshAvailableContent()
        {
            var dlcEnabled = false;
            try
            {
                DLCManager.RefreshDLC();
                dlcEnabled = DLCManager.DLCEnabled();
            }
            catch (Exception exception)
            {
                if (!dlcAvailabilityKnown)
                    Logger.LogWarning(
                        "No se pudo consultar el DLC; se usara solo contenido base: " +
                        exception.Message);
            }

            availableBossIndices.Clear();
            for (var i = 0; i < RouletteData.Bosses.Length; i++)
            {
                if (dlcEnabled || !RouletteData.Bosses[i].RequiresDlc)
                    availableBossIndices.Add(i);
            }

            availableWeaponIndices.Clear();
            for (var i = 0; i < RouletteData.Weapons.Length; i++)
            {
                if (dlcEnabled || !RouletteData.Weapons[i].RequiresDlc)
                    availableWeaponIndices.Add(i);
            }

            availableSuperIndices.Clear();
            for (var i = 0; i < RouletteData.Supers.Length; i++)
            {
                if (dlcEnabled || !RouletteData.Supers[i].RequiresDlc)
                    availableSuperIndices.Add(i);
            }

            availableCharmIndices.Clear();
            for (var i = 0; i < RouletteData.Charms.Length; i++)
            {
                if (dlcEnabled || !RouletteData.Charms[i].RequiresDlc)
                    availableCharmIndices.Add(i);
            }

            if (!dlcAvailabilityKnown || dlcEnabledForRoulette != dlcEnabled)
            {
                Logger.LogInfo(dlcEnabled
                    ? "DLC disponible: la ruleta usara contenido base y DLC."
                    : "DLC no disponible: la ruleta usara solo contenido base.");
            }
            dlcEnabledForRoulette = dlcEnabled;
            dlcAvailabilityKnown = true;
        }

        private int RandomPoolIndex(List<int> pool)
        {
            return pool.Count > 0 ? pool[random.Next(pool.Count)] : 0;
        }

        private int RandomNonEmptyPoolIndex(List<int> pool, int emptyIndex)
        {
            var candidates = new List<int>();
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] != emptyIndex)
                    candidates.Add(pool[i]);
            }
            return RandomPoolIndex(candidates);
        }

        private void UpdateSpin()
        {
            var elapsed = Time.realtimeSinceStartup - spinStartedAt;
            ticker = (int)(elapsed / 0.07f);
            var fields = uglyMode ? 6 : 5;
            var oldRevealed = revealed;
            if (elapsed >= 5f)
                revealed = Math.Min(fields, (int)(elapsed - 5f) + 1);

            if (revealed > oldRevealed)
            {
                for (var i = oldRevealed; i < revealed; i++)
                    pulseUntil[i] = Time.realtimeSinceStartup + 0.38f;
                PlayOneShot(selectionClip, SelectionStopAudioVolume);
            }

            if (revealed < fields)
                return;

            running = false;
            resultReady = true;
            StopSpinAudio();
            status = autoLoad.Value
                ? RouletteStatus.ResultLoading
                : RouletteStatus.ResultReady;
            if (autoLoad.Value)
            {
                pendingLoad = true;
                loadAt = Time.realtimeSinceStartup + Math.Max(0f, loadDelay.Value);
            }
        }

        private void LoadResult()
        {
            var previousMap = default(Scenes);
            var returnDestinationPrepared = false;
            try
            {
                if (!PlayerData.Initialized || PlayerData.Data == null)
                {
                    status = RouletteStatus.SaveRequired;
                    Logger.LogWarning("Selecciona primero una partida guardada.");
                    return;
                }
                if (SceneLoader.CurrentlyLoading)
                {
                    status = RouletteStatus.SceneLoading;
                    return;
                }

                resultReady = false;
                previousMap = PlayerData.Data.CurrentMap;
                CaptureOriginalLoadouts();
                ApplyLoadout(PlayerId.PlayerOne);
                ApplyLoadout(PlayerId.PlayerTwo);
                Level.SetCurrentMode(difficulty);
                var boss = RouletteData.Bosses[result.Boss];
                SetActiveChallenge(
                    uglyMode
                        ? RouletteData.Modifiers[result.Modifier].Id
                        : ModifierId.None,
                    result.Boss);
                BeginBattleResultHudSession();
                if (!PrepareBattleResultHud())
                    Logger.LogWarning(
                        "Could not prepare the roulette battle HUD before loading.");
                PrepareRouletteReturnDestination(boss.Level);
                returnDestinationPrepared = true;
                Logger.LogInfo("Cargando " + boss.Character + " (" + boss.Level + ")");
                SceneLoader.LoadLevel(boss.Level, SceneLoader.Transition.Iris, SceneLoader.Icon.None);
            }
            catch (Exception exception)
            {
                if (returnDestinationPrepared &&
                    PlayerData.Initialized && PlayerData.Data != null)
                {
                    PlayerData.Data.CurrentMap = previousMap;
                    rouletteReturnDestinationPending = false;
                }
                status = RouletteStatus.LoadFailed;
                RestoreOriginalLoadouts(false);
                ClearActiveChallenge();
                EndBattleResultHudSession();
                Logger.LogError(exception);
                SetVisible(true);
            }
        }

        private void PrepareRouletteReturnDestination(Levels bossLevel)
        {
            Scenes targetMap;
            if (!TryGetBossMap(bossLevel, out targetMap))
                throw new InvalidOperationException(
                    "Could not determine the return map for " + bossLevel + ".");

            rouletteReturnLevel = bossLevel;
            rouletteReturnMap = targetMap;
            rouletteReturnDestinationPending = true;
            PlayerData.Data.CurrentMap = targetMap;
            Logger.LogInfo(
                "Roulette return destination prepared: " + bossLevel +
                " on " + targetMap + ".");
        }

        private static bool TryGetBossMap(Levels bossLevel, out Scenes map)
        {
            if (ContainsLevel(Level.world1BossLevels, bossLevel))
            {
                map = Scenes.scene_map_world_1;
                return true;
            }
            if (ContainsLevel(Level.world2BossLevels, bossLevel))
            {
                map = Scenes.scene_map_world_2;
                return true;
            }
            if (ContainsLevel(Level.world3BossLevels, bossLevel))
            {
                map = Scenes.scene_map_world_3;
                return true;
            }
            if (ContainsLevel(Level.world4BossLevels, bossLevel))
            {
                map = Scenes.scene_map_world_4;
                return true;
            }
            if (ContainsLevel(Level.worldDLCBossLevelsWithSaltbaker, bossLevel) ||
                bossLevel == Levels.Graveyard)
            {
                map = Scenes.scene_map_world_DLC;
                return true;
            }

            map = default(Scenes);
            return false;
        }

        private static bool ContainsLevel(Levels[] levels, Levels target)
        {
            if (levels == null)
                return false;
            for (var i = 0; i < levels.Length; i++)
            {
                if (levels[i] == target)
                    return true;
            }
            return false;
        }

        private void ApplyRouletteReturnDestination()
        {
            if (!rouletteReturnDestinationPending ||
                !PlayerData.Initialized || PlayerData.Data == null ||
                PlayerData.Data.CurrentMap != rouletteReturnMap)
                return;

            try
            {
                var entrance = FindRouletteBossEntrance(rouletteReturnLevel);
                if (entrance == null)
                {
                    Logger.LogWarning(
                        "Could not find the map entrance for " +
                        rouletteReturnLevel + " on " + rouletteReturnMap +
                        "; keeping the map's saved position.");
                    return;
                }

                var mapData = PlayerData.Data.CurrentMapData;
                mapData.sessionStarted = true;
                mapData.enteringFrom =
                    PlayerData.MapData.EntryMethod.None;
                entrance.SetPlayerReturnPos();
                rouletteReturnDestinationPending = false;
                Logger.LogInfo(
                    "Returning roulette players to the native entrance for " +
                    rouletteReturnLevel + " on " + rouletteReturnMap + ".");
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "Could not apply the roulette boss-door return position: " +
                    exception);
            }
        }

        private static AbstractMapInteractiveEntity FindRouletteBossEntrance(
            Levels bossLevel)
        {
            var levelField = AccessTools.Field(
                typeof(MapLevelLoader), "level");
            if (levelField != null)
            {
                var loaders =
                    Resources.FindObjectsOfTypeAll<MapLevelLoader>();
                for (var i = 0; i < loaders.Length; i++)
                {
                    var loader = loaders[i];
                    if (loader == null ||
                        !loader.gameObject.scene.IsValid())
                        continue;
                    var entranceLevel = (Levels)levelField.GetValue(loader);
                    if (entranceLevel == bossLevel ||
                        (bossLevel == Levels.Saltbaker &&
                         entranceLevel == Levels.Kitchen))
                        return loader;
                }
            }

            if (bossLevel == Levels.DicePalaceMain ||
                bossLevel == Levels.Devil)
            {
                var diceEntrances =
                    Resources.FindObjectsOfTypeAll<MapDicePalaceSceneLoader>();
                for (var i = 0; i < diceEntrances.Length; i++)
                {
                    var entrance = diceEntrances[i];
                    if (entrance != null &&
                        entrance.gameObject.scene.IsValid())
                        return entrance;
                }
            }

            if (bossLevel == Levels.Saltbaker)
            {
                // The DLC bakery door is a dedicated interactive entity, not
                // a MapLevelLoader. It already owns the native return offsets
                // for both players, so use it before the scene-loader fallback.
                var bakeryEntrances =
                    Resources.FindObjectsOfTypeAll<MapBakeryLoader>();
                for (var i = 0; i < bakeryEntrances.Length; i++)
                {
                    var entrance = bakeryEntrances[i];
                    if (entrance != null &&
                        entrance.gameObject.scene.IsValid())
                        return entrance;
                }

                var sceneField = AccessTools.Field(
                    typeof(MapSceneLoader), "scene");
                if (sceneField != null)
                {
                    var sceneEntrances =
                        Resources.FindObjectsOfTypeAll<MapSceneLoader>();
                    for (var i = 0; i < sceneEntrances.Length; i++)
                    {
                        var entrance = sceneEntrances[i];
                        if (entrance == null ||
                            !entrance.gameObject.scene.IsValid())
                            continue;
                        if ((Scenes)sceneField.GetValue(entrance) ==
                            Scenes.scene_level_kitchen)
                            return entrance;
                    }
                }
            }
            return null;
        }

        private void ApplyLoadout(PlayerId playerId)
        {
            var loadout = PlayerData.Data.Loadouts.GetPlayerLoadout(playerId);
            if (loadout == null)
                return;

            loadout.primaryWeapon = RouletteData.Weapons[result.Weapon1].Value;
            loadout.secondaryWeapon = RouletteData.Weapons[result.Weapon2].Value;
            loadout.super = RouletteData.Supers[result.Super].Value;
            loadout.charm = RouletteData.Charms[result.Charm].Value;
            loadout.HasEquippedSecondaryRegularWeapon =
                loadout.secondaryWeapon != Weapon.None;
            loadout.MustNotifySwitchRegularWeapon = true;
        }

        private void CaptureOriginalLoadouts()
        {
            if (loanedLoadoutsActive && !RestoreOriginalLoadouts(false))
                throw new InvalidOperationException(
                    "Could not restore the previous roulette loadout before a new fight.");

            originalPlayerOneLoadout =
                LoadoutSnapshot.Capture(PlayerId.PlayerOne);
            originalPlayerTwoLoadout =
                LoadoutSnapshot.Capture(PlayerId.PlayerTwo);
            loanedLoadoutsActive = originalPlayerOneLoadout != null ||
                                   originalPlayerTwoLoadout != null;
            loanedBattleSeen = false;

            if (!loanedLoadoutsActive)
                throw new InvalidOperationException(
                    "Could not capture either player loadout.");

            Logger.LogInfo("Saved the original loadout before the roulette fight.");
        }

        private bool RestoreOriginalLoadouts(bool saveAfterRestore)
        {
            if (!loanedLoadoutsActive)
                return true;
            if (!PlayerData.Initialized || PlayerData.Data == null)
                return false;

            try
            {
                if (originalPlayerOneLoadout != null)
                    originalPlayerOneLoadout.Restore(PlayerId.PlayerOne);
                if (originalPlayerTwoLoadout != null)
                    originalPlayerTwoLoadout.Restore(PlayerId.PlayerTwo);
                if (saveAfterRestore)
                    PlayerData.SaveCurrentFile();

                originalPlayerOneLoadout = null;
                originalPlayerTwoLoadout = null;
                loanedLoadoutsActive = false;
                loanedBattleSeen = false;
                Logger.LogInfo("Restored the loadout used before the roulette fight.");
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogError(
                    "Could not restore the loadout used before the roulette fight: " +
                    exception);
                return false;
            }
        }

        private void UpdateLoanedLoadoutLifecycle()
        {
            if (!loanedLoadoutsActive || SceneLoader.CurrentlyLoading)
                return;

            try
            {
                var level = Level.Current;
                if (level != null)
                {
                    if (level.LevelType == Level.Type.Battle)
                        loanedBattleSeen = true;
                    return;
                }

                if (loanedBattleSeen && Map.Current != null)
                    RestoreOriginalLoadouts(true);
            }
            catch
            {
                // Scene transitions can briefly invalidate Cuphead's static references.
            }
        }

        private static void RestoreLoadoutBeforeReturnToMapPrefix()
        {
            var plugin = activeInstance;
            if (plugin != null)
            {
                plugin.RestoreOriginalLoadouts(false);
                plugin.EndBattleResultHudSession();
            }
        }

        private static bool BlockEquipmentAfterRouletteDefeatPrefix()
        {
            var plugin = activeInstance;
            return plugin == null || !plugin.loanedLoadoutsActive;
        }

        private bool ShouldRestoreLoanedLoadoutOnWin(Level level)
        {
            if (!loanedLoadoutsActive)
                return false;
            if (level == null || !LoanedLoadoutUsesDicePalace())
                return true;

            return level.CurrentLevel == Levels.DicePalaceMain;
        }

        private bool LoanedLoadoutUsesDicePalace()
        {
            return result.Boss >= 0 &&
                   result.Boss < RouletteData.Bosses.Length &&
                   RouletteData.Bosses[result.Boss].Level ==
                   Levels.DicePalaceMain;
        }

        private void OnGUI()
        {
            theme.Refresh();
            EnsureStyles();

            var scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight);
            var offsetX = (Screen.width - DesignWidth * scale) * 0.5f;
            var offsetY = (Screen.height - DesignHeight * scale) * 0.5f;
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(offsetX, offsetY, 0f),
                Quaternion.identity,
                new Vector3(scale, scale, 1f));

            DrawLanguageTestNotice();
            if (CanUseRouletteOnMap() && cardVisibility > 0.001f)
                DrawRoulette();

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private void DrawRouletteLegacy()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, DesignWidth, DesignHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var panel = new Rect(55f, 28f, 1170f, 664f);
            theme.DrawPaper(panel);
            GUI.BeginGroup(panel);

            GUI.color = Ink;
            GUI.DrawTexture(new Rect(22f, 18f, 1126f, 61f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(35f, 17f, 1100f, 58f),
                L(ModText.Brand), titleStyle);
            GUI.Label(new Rect(35f, 76f, 1100f, 30f),
                L(ModText.Tagline), subtitleStyle);

            var bossIndex = DisplayPoolIndex(
                0, result.Boss, availableBossIndices, 0);
            var boss = RouletteData.Bosses[bossIndex];
            DrawBossPanel(boss);

            var weapon1 = DisplayPoolIndex(
                1, result.Weapon1, availableWeaponIndices, 0);
            var weapon2 = DisplayPoolIndex(
                2, result.Weapon2, availableWeaponIndices,
                availableWeaponIndices.Count / 2);
            var super = DisplayPoolIndex(
                3, result.Super, availableSuperIndices,
                availableSuperIndices.Count / 3);
            var charm = DisplayPoolIndex(
                4, result.Charm, availableCharmIndices,
                availableCharmIndices.Count / 4);

            DrawEquipmentCard(new Rect(360f, 127f, 225f, 151f),
                L(ModText.SlotWeaponA),
                LocalizedEquipmentName(RouletteData.Weapons[weapon1]),
                RouletteData.Weapons[weapon1].Image,
                RouletteData.Weapons[weapon1].NativeSprite, 1);
            DrawEquipmentCard(new Rect(607f, 127f, 225f, 151f),
                L(ModText.SlotWeaponB),
                LocalizedEquipmentName(RouletteData.Weapons[weapon2]),
                RouletteData.Weapons[weapon2].Image,
                RouletteData.Weapons[weapon2].NativeSprite, 2);
            DrawEquipmentCard(new Rect(854f, 127f, 225f, 151f),
                L(ModText.SlotSuper),
                LocalizedEquipmentName(RouletteData.Supers[super]),
                RouletteData.Supers[super].Image,
                RouletteData.Supers[super].NativeSprite, 3);

            var charmRect = uglyMode
                ? new Rect(484f, 302f, 225f, 151f)
                : new Rect(607f, 302f, 225f, 151f);
            DrawEquipmentCard(charmRect, L(ModText.SlotCharm),
                LocalizedEquipmentName(RouletteData.Charms[charm]),
                RouletteData.Charms[charm].Image,
                RouletteData.Charms[charm].NativeSprite, 4);

            if (uglyMode)
            {
                var rollingModifier = CurrentRollingModifier(bossIndex);
                var modifier = DisplayIndex(5, result.Modifier, RouletteData.Modifiers.Length, rollingModifier - ticker);
                DrawEquipmentCard(new Rect(731f, 302f, 225f, 151f),
                    L(ModText.SlotChallenge),
                    LocalizedModifierName(RouletteData.Modifiers[modifier].Id),
                    RouletteData.Modifiers[modifier].Image,
                    null, 5);
            }

            DrawBottomControls();
            GUI.EndGroup();
        }

        private void DrawBossPanel(BossEntry boss)
        {
            var rect = PulseRect(new Rect(43f, 127f, 274f, 326f), 0);
            GUI.color = new Color(0.12f, 0.10f, 0.08f, 0.20f);
            GUI.DrawTexture(new Rect(rect.x + 7f, rect.y + 8f, rect.width, rect.height), Texture2D.whiteTexture);
            GUI.color = Cream;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GameTheme.DrawBorder(rect, Ink, 4f);
            DrawTexture(new Rect(rect.x + 22f, rect.y + 19f, rect.width - 44f, 218f), boss.Image);
            GUI.color = Red;
            GUI.DrawTexture(new Rect(rect.x + 12f, rect.y + 246f, rect.width - 24f, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 251f, rect.width - 20f, 39f), boss.Character.ToUpperInvariant(), bossStyle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 287f, rect.width - 20f, 28f), boss.Fight.ToUpperInvariant(), smallStyle);
        }

        private void DrawEquipmentCard(Rect baseRect, string heading, string value, string fallbackImage, string nativeSprite, int field)
        {
            var rect = PulseRect(baseRect, field);
            GUI.color = new Color(0.12f, 0.10f, 0.08f, 0.22f);
            GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + 7f, rect.width, rect.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.91f, 0.82f, 0.63f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GameTheme.DrawBorder(rect, Ink, 4f);

            GUI.color = Red;
            GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 31f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 3f, rect.width - 16f, 32f), heading, cardTitleStyle);

            var iconRect = new Rect(rect.x + 15f, rect.y + 45f, 75f, 75f);
            GUI.color = new Color(0.98f, 0.94f, 0.82f);
            GUI.DrawTexture(iconRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (!theme.DrawSprite(nativeSprite, iconRect, Color.white))
                DrawTexture(iconRect, fallbackImage);

            GUI.Label(new Rect(rect.x + 96f, rect.y + 46f, rect.width - 106f, 73f), value.ToUpperInvariant(), bodyStyle);
            GUI.Label(new Rect(rect.x + 13f, rect.y + 124f, rect.width - 26f, 20f),
                revealed > field
                    ? L(ModText.ValueSelected)
                    : L(ModText.ValueRolling), smallStyle);
        }

        private void DrawBottomControls()
        {
            GUI.Label(new Rect(40f, 473f, 170f, 31f),
                L(ModText.SettingDifficulty), cardTitleStyle);
            DrawModeButton(new Rect(210f, 469f, 132f, 38f),
                L(ModText.DifficultyEasy), Level.Mode.Easy);
            DrawModeButton(new Rect(351f, 469f, 132f, 38f),
                L(ModText.DifficultyNormal), Level.Mode.Normal);
            DrawModeButton(new Rect(492f, 469f, 132f, 38f),
                L(ModText.DifficultyHard), Level.Mode.Hard);

            if (GUI.Button(new Rect(657f, 469f, 208f, 38f),
                (uglyMode ? "?  " : "") + L(ModText.SettingChallenge),
                uglyMode ? buttonActiveStyle : buttonStyle))
                uglyMode = !uglyMode;
            if (GUI.Button(new Rect(878f, 469f, 214f, 38f),
                (autoLoad.Value ? "?  " : "") +
                L(ModText.SettingAutoLoad),
                autoLoad.Value ? buttonActiveStyle : buttonStyle))
                autoLoad.Value = !autoLoad.Value;

            GUI.Label(new Rect(42f, 518f, 1050f, 31f),
                modLocalization.StatusText(status), subtitleStyle);
            GUI.enabled = !running && !pendingLoad;
            var spinRect = new Rect(385f, 558f, 287f, 58f);
            if (GUI.Button(spinRect,
                L(ModText.ActionSpin) + "   F7", buttonActiveStyle))
                StartRoulette();
            GUI.enabled = true;
            if (GUI.Button(new Rect(688f, 558f, 148f, 58f),
                L(ModText.ActionClose), buttonStyle))
                SetVisible(false);

            if (spinRect.Contains(Event.current.mousePosition))
                theme.DrawSprite("hand_cursor_boil_0001", new Rect(spinRect.x - 59f, spinRect.y + 4f, 54f, 54f), Color.white);

            GUI.Label(new Rect(35f, 625f, 1100f, 24f),
                L(ModText.ControlsLegacy), smallStyle);
        }

        private void DrawModeButton(Rect rect, string label, Level.Mode mode)
        {
            if (GUI.Button(rect, (difficulty == mode ? "?  " : "") + label,
                difficulty == mode ? buttonActiveStyle : buttonStyle))
                difficulty = mode;
        }

        private void UpdateBlackAndWhiteTransition()
        {
            var challengeSelected =
                activeChallenge == ModifierId.BlackAndWhite;
            var activeFight = false;
            var levelInstanceId = -1;

            if (challengeSelected && !SceneLoader.CurrentlyLoading)
            {
                try
                {
                    var level = Level.Current;
                    activeFight = level != null &&
                                  level.LevelType == Level.Type.Battle &&
                                  ActiveChallengeMatches(level);
                    if (activeFight)
                        levelInstanceId = level.GetInstanceID();
                }
                catch
                {
                    activeFight = false;
                }
            }

            if (activeFight)
            {
                if (blackAndWhiteLevelInstanceId != levelInstanceId)
                {
                    ResetBlackAndWhiteRenderEffects();
                    // A fresh attempt always starts in the player's normal
                    // colors, then waits before fading into monochrome.
                    blackAndWhiteLevelInstanceId = levelInstanceId;
                    blackAndWhiteFadeOutStarted = false;
                    blackAndWhiteBlend = 0f;
                    BeginBlackAndWhiteTransition(
                        1f, BlackAndWhiteEntryDelay,
                        BlackAndWhiteFadeInDuration);
                    Logger.LogInfo(
                        "Black-and-white challenge transition started for " +
                        Level.Current.CurrentLevel + ".");
                }
            }
            else if (!(challengeSelected && SceneLoader.CurrentlyLoading))
            {
                blackAndWhiteLevelInstanceId = -1;
                var fadingIn = blackAndWhiteTransitionStartedAt >= 0f &&
                               blackAndWhiteTransitionTo > 0.001f;
                if (!blackAndWhiteFadeOutStarted &&
                    (blackAndWhiteBlend > 0.001f || fadingIn))
                {
                    blackAndWhiteFadeOutStarted = true;
                    BeginBlackAndWhiteTransition(
                        0f, 0f, BlackAndWhiteFadeOutDuration);
                }
            }

            AdvanceBlackAndWhiteTransition();
        }

        private void UpdateBlackAndWhiteRenderEffects()
        {
            var shouldRun = blackAndWhiteLevelInstanceId >= 0 ||
                            blackAndWhiteNativeBaseActive ||
                            blackAndWhiteBlend > 0.001f ||
                            blackAndWhiteTransitionStartedAt >= 0f;

            for (var i = blackAndWhiteEffects.Count - 1; i >= 0; i--)
            {
                var effect = blackAndWhiteEffects[i];
                if (!effect.IsValid)
                {
                    effect.Dispose();
                    blackAndWhiteEffects.RemoveAt(i);
                    continue;
                }

                effect.SetBlend(blackAndWhiteBlend);
            }

            // Switch to Cuphead's exact native filter only after saturation
            // has reached zero. On fade-out, release it immediately while the
            // correction component still renders a fully gray frame.
            blackAndWhiteNativeBaseActive =
                ShouldUseNativeBlackAndWhiteFilter();

            if (!shouldRun)
            {
                ResetBlackAndWhiteRenderEffects();
                return;
            }

            if (Time.realtimeSinceStartup < nextBlackAndWhiteEffectScanAt)
                return;

            nextBlackAndWhiteEffectScanAt =
                Time.realtimeSinceStartup + 0.2f;
            var blurEffects = FindObjectsOfType<BlurGamma>();
            for (var i = 0; i < blurEffects.Length; i++)
            {
                var blurEffect = blurEffects[i];
                if (blurEffect == null || HasBlackAndWhiteEffect(blurEffect))
                    continue;

                BlackAndWhiteSaturationEffect effect;
                string error;
                if (!BlackAndWhiteSaturationEffect.TryCreate(
                    blurEffect, blackAndWhiteTransitionShader,
                    out effect, out error))
                {
                    if (!string.IsNullOrEmpty(error) &&
                        !blackAndWhiteRenderFailureLogged)
                    {
                        blackAndWhiteRenderFailureLogged = true;
                        Logger.LogWarning(
                            "Black-and-white render bridge is waiting: " +
                            error);
                    }
                    continue;
                }

                effect.SetBlend(blackAndWhiteBlend);
                blackAndWhiteEffects.Add(effect);
                blackAndWhiteRenderFailureLogged = false;
                Logger.LogInfo(
                    "Attached the bundled saturation transition to camera " +
                    blurEffect.gameObject.name + ".");
            }

            blackAndWhiteNativeBaseActive =
                ShouldUseNativeBlackAndWhiteFilter();
            for (var i = 0; i < blackAndWhiteEffects.Count; i++)
                blackAndWhiteEffects[i].SetBlend(blackAndWhiteBlend);
        }

        private bool ShouldUseNativeBlackAndWhiteFilter()
        {
            var fadingOut = blackAndWhiteTransitionStartedAt >= 0f &&
                            blackAndWhiteTransitionTo < 0.001f;
            return blackAndWhiteEffects.Count > 0 && !fadingOut &&
                   blackAndWhiteBlend >= 0.999f;
        }

        private bool HasBlackAndWhiteEffect(BlurGamma blurEffect)
        {
            for (var i = 0; i < blackAndWhiteEffects.Count; i++)
            {
                if (blackAndWhiteEffects[i].Matches(blurEffect))
                    return true;
            }
            return false;
        }

        private void ResetBlackAndWhiteRenderEffects()
        {
            // Restore the player's real filter before removing the temporary
            // saturation correction. No persistent setting is changed.
            blackAndWhiteNativeBaseActive = false;
            for (var i = blackAndWhiteEffects.Count - 1; i >= 0; i--)
                blackAndWhiteEffects[i].Dispose();
            blackAndWhiteEffects.Clear();
            nextBlackAndWhiteEffectScanAt = 0f;
        }

        private void BeginBlackAndWhiteTransition(
            float target, float delay, float duration)
        {
            blackAndWhiteTransitionStartedAt = Time.realtimeSinceStartup;
            blackAndWhiteTransitionFrom = blackAndWhiteBlend;
            blackAndWhiteTransitionTo = Mathf.Clamp01(target);
            blackAndWhiteTransitionDelay = Mathf.Max(0f, delay);
            blackAndWhiteTransitionDuration = Mathf.Max(0.001f, duration);
        }

        private void AdvanceBlackAndWhiteTransition()
        {
            if (blackAndWhiteTransitionStartedAt < 0f)
                return;

            // If a camera takes longer than usual to initialize, hold the
            // color frame instead of starting a transition with no renderer.
            if (blackAndWhiteTransitionTo > 0.999f &&
                blackAndWhiteEffects.Count == 0 &&
                Time.realtimeSinceStartup - blackAndWhiteTransitionStartedAt >
                    blackAndWhiteTransitionDelay)
            {
                blackAndWhiteTransitionStartedAt =
                    Time.realtimeSinceStartup - blackAndWhiteTransitionDelay;
                blackAndWhiteBlend = blackAndWhiteTransitionFrom;
                return;
            }

            var elapsed = Time.realtimeSinceStartup -
                          blackAndWhiteTransitionStartedAt -
                          blackAndWhiteTransitionDelay;
            if (elapsed <= 0f)
            {
                blackAndWhiteBlend = blackAndWhiteTransitionFrom;
                return;
            }

            var progress = Mathf.Clamp01(
                elapsed / blackAndWhiteTransitionDuration);
            var smoothProgress = progress * progress * (3f - 2f * progress);
            blackAndWhiteBlend = Mathf.Lerp(
                blackAndWhiteTransitionFrom,
                blackAndWhiteTransitionTo,
                smoothProgress);
            if (progress >= 1f)
            {
                blackAndWhiteBlend = blackAndWhiteTransitionTo;
                blackAndWhiteTransitionStartedAt = -1f;
            }
        }

        private void SetActiveChallenge(ModifierId challenge, int bossIndex)
        {
            soloMiniRestartPending = false;
            activeChallenge = challenge;
            activeChallengeBoss = activeChallenge == ModifierId.None
                ? -1 : bossIndex;
        }

        private void ClearActiveChallenge()
        {
            soloMiniRestartPending = false;
            activeChallenge = ModifierId.None;
            activeChallengeBoss = -1;
            SetNativeChallengePromptVisible(false);
        }

        private static void ClearChallengeOnWinPrefix(Level __instance)
        {
            var plugin = activeInstance;
            if (plugin == null)
                return;

            var currentLevel = __instance == null
                ? default(Levels)
                : __instance.CurrentLevel;
            var isRouletteFinalBoss = plugin.loanedLoadoutsActive &&
                (currentLevel == Levels.Devil ||
                 currentLevel == Levels.Saltbaker);
            if (isRouletteFinalBoss)
                plugin.returnToMapAfterRouletteFinalBossWin = true;

            if (plugin.ShouldRestoreLoanedLoadoutOnWin(__instance))
            {
                plugin.KeepBattleResultHudThroughVictory(
                    currentLevel == Levels.Saltbaker);
                plugin.RestoreOriginalLoadouts(false);
            }
            if (plugin.ShouldClearChallengeOnWin(__instance))
                plugin.ClearActiveChallenge();
        }

        private static bool ReturnRouletteFinalBossWinToMapPrefix(
            Scenes __0, Scenes __1)
        {
            var plugin = activeInstance;
            if (plugin == null ||
                !plugin.returnToMapAfterRouletteFinalBossWin)
                return true;

            var previousLevel = Level.PreviousLevel;
            var isDevilEnding = previousLevel == Levels.Devil &&
                __0 == Scenes.scene_title &&
                __1 == Scenes.scene_cutscene_outro;
            var isSaltbakerEnding = previousLevel == Levels.Saltbaker &&
                __0 == Scenes.scene_map_world_DLC &&
                __1 == Scenes.scene_cutscene_dlc_ending;
            if (!isDevilEnding && !isSaltbakerEnding)
                return true;

            // WinScreen reaches this call only after grading, progression,
            // achievements and PlayerData.SaveCurrentFile(). Reuse the normal
            // map-return path so its existing loadout/HUD cleanup also runs.
            plugin.returnToMapAfterRouletteFinalBossWin = false;
            plugin.Logger.LogInfo(
                "Roulette final-boss victory: skipping the ending and returning to the map (" +
                previousLevel + ").");
            SceneLoader.LoadLastMap();
            return false;
        }

        private bool ShouldClearChallengeOnWin(Level level)
        {
            if (soloMiniRestartPending)
                return false;

            if (level == null || !IsActiveDicePalaceChallenge())
                return true;

            return level.CurrentLevel == Levels.DicePalaceMain;
        }

        private static bool BlockDashPrefix(ref bool __result)
        {
            var plugin = activeInstance;
            if (plugin == null || !plugin.ShouldBlockDash())
                return true;

            __result = false;
            return false;
        }

        private bool ShouldBlockDash()
        {
            return activeChallenge == ModifierId.NoDash &&
                   ShouldShowActiveChallenge();
        }

        private static bool BlockMiniPlanePrefix()
        {
            var plugin = activeInstance;
            return plugin == null || !plugin.ShouldBlockMiniPlane();
        }

        private bool ShouldBlockMiniPlane()
        {
            return activeChallenge == ModifierId.NoMiniPlane &&
                   ShouldShowActiveChallenge();
        }

        private static void RestartSoloMiniOnInvalidDamagePostfix(
            GameObject hit,
            float __result,
            DamageDealer.DamageSource ___damageSource)
        {
            var plugin = activeInstance;
            if (plugin == null || __result <= 0f ||
                !plugin.ShouldRestartOnNonMiniPlaneDamage() ||
                (___damageSource == DamageDealer.DamageSource.SmallPlane ||
                 ___damageSource == DamageDealer.DamageSource.Super) ||
                !IsEnemyDamageTarget(hit))
                return;

            plugin.QueueSoloMiniRestart();
        }

        private bool ShouldRestartOnNonMiniPlaneDamage()
        {
            return activeChallenge == ModifierId.MiniPlaneOnly &&
                   ShouldShowActiveChallenge();
        }

        private void QueueSoloMiniRestart()
        {
            if (soloMiniRestartPending)
                return;

            soloMiniRestartPending = true;
            StartCoroutine(RestartAfterSoloMiniViolation());
        }

        private IEnumerator RestartAfterSoloMiniViolation()
        {
            // Let the valid damage call and its collision callbacks finish
            // before asking SceneLoader to replace the battle scene.
            yield return null;

            if (soloMiniRestartPending &&
                activeChallenge == ModifierId.MiniPlaneOnly &&
                !SceneLoader.CurrentlyLoading)
                SceneLoader.ReloadLevel();

            soloMiniRestartPending = false;
        }

        private static bool IsEnemyDamageTarget(GameObject hit)
        {
            if (hit == null)
                return false;

            var receiver = hit.GetComponent<DamageReceiver>();
            if (receiver == null)
            {
                var child = hit.GetComponent<DamageReceiverChild>();
                if (child != null && child.enabled)
                    receiver = child.Receiver;
            }

            return receiver != null && receiver.enabled &&
                   receiver.type == DamageReceiver.Type.Enemy;
        }

        private static void EnforcePlaneStartingWeaponPostfix(
            PlanePlayerWeaponManager __instance)
        {
            var plugin = activeInstance;
            if (plugin == null || __instance == null)
                return;

            Weapon startingWeapon;
            if (!plugin.TryGetRequiredPlaneWeapon(out startingWeapon))
                return;

            __instance.SwitchToWeapon(startingWeapon);
        }

        private static void EnforcePlaneWeaponRestrictionPrefix(
            ref Weapon __0)
        {
            var plugin = activeInstance;
            Weapon requiredWeapon;
            if (plugin != null && plugin.ShouldLockPlaneWeapon() &&
                plugin.TryGetRequiredPlaneWeapon(out requiredWeapon))
                __0 = requiredWeapon;
        }

        private bool TryGetRequiredPlaneWeapon(out Weapon weapon)
        {
            weapon = Weapon.None;
            if (result == null || result.Charm < 0 ||
                result.Charm >= RouletteData.Charms.Length)
                return false;

            var isChalice =
                RouletteData.Charms[result.Charm].Value == Charm.charm_chalice;
            if (activeChallenge == ModifierId.NoBombs)
            {
                weapon = isChalice
                    ? Weapon.plane_chalice_weapon_3way
                    : Weapon.plane_weapon_peashot;
                return true;
            }
            if (activeChallenge == ModifierId.NoPeashooter)
            {
                weapon = isChalice
                    ? Weapon.plane_chalice_weapon_bomb
                    : Weapon.plane_weapon_bomb;
                return true;
            }

            return false;
        }

        private static bool BlockPlaneWeaponSwitchPrefix()
        {
            var plugin = activeInstance;
            return plugin == null || !plugin.ShouldLockPlaneWeapon();
        }

        private bool ShouldLockPlaneWeapon()
        {
            return (activeChallenge == ModifierId.NoBombs ||
                    activeChallenge == ModifierId.NoPeashooter) &&
                   ShouldShowActiveChallenge();
        }

        private static void CanUseExPostfix(ref bool __result)
        {
            var plugin = activeInstance;
            if (plugin != null && plugin.ShouldBlockGroundEx())
                __result = false;
        }

        private static bool BlockPlaneExPrefix()
        {
            var plugin = activeInstance;
            return plugin == null || !plugin.ShouldBlockEx();
        }

        private bool ShouldBlockGroundEx()
        {
            return ShouldBlockEx() && !ActiveChallengeUsesPlaneControls();
        }

        private bool ShouldBlockEx()
        {
            return activeChallenge == ModifierId.NoEx &&
                   ShouldShowActiveChallenge();
        }

        private bool ActiveChallengeUsesPlaneControls()
        {
            if (activeChallengeBoss < 0 ||
                activeChallengeBoss >= RouletteData.Bosses.Length)
                return false;

            try
            {
                var level = Level.Current;
                if (level != null &&
                    (level.CurrentLevel == Levels.DicePalaceFlyingHorse ||
                     level.CurrentLevel == Levels.DicePalaceFlyingMemory))
                    return true;
            }
            catch
            {
                // Fall back to the roulette boss type during scene transitions.
            }

            return RouletteData.Bosses[activeChallengeBoss].IsPlane;
        }

        private void UpdateActiveChallengeLifecycle()
        {
            if (activeChallenge == ModifierId.None ||
                SceneLoader.CurrentlyLoading)
                return;

            try
            {
                var level = Level.Current;
                if (level != null)
                {
                    if (level.LevelType != Level.Type.Battle || !ActiveChallengeMatches(level))
                        ClearActiveChallenge();
                    return;
                }

                if (Map.Current != null)
                    ClearActiveChallenge();
            }
            catch
            {
                // Scene transitions can briefly invalidate Cuphead's static references.
            }
        }

        private bool ShouldShowActiveChallenge()
        {
            if (activeChallenge == ModifierId.None ||
                SceneLoader.CurrentlyLoading)
                return false;

            try
            {
                var level = Level.Current;
                return level != null &&
                       level.LevelType == Level.Type.Battle &&
                       ActiveChallengeMatches(level);
            }
            catch
            {
                return false;
            }
        }

        private bool ActiveChallengeMatches(Level level)
        {
            if (activeChallengeBoss < 0 ||
                activeChallengeBoss >= RouletteData.Bosses.Length)
                return false;

            var targetLevel = RouletteData.Bosses[activeChallengeBoss].Level;
            return level.CurrentLevel == targetLevel ||
                   (targetLevel == Levels.DicePalaceMain &&
                    IsDicePalaceLevel(level.CurrentLevel));
        }

        private bool IsActiveDicePalaceChallenge()
        {
            return activeChallengeBoss >= 0 &&
                   activeChallengeBoss < RouletteData.Bosses.Length &&
                   RouletteData.Bosses[activeChallengeBoss].Level ==
                   Levels.DicePalaceMain;
        }

        private static bool IsDicePalaceLevel(Levels level)
        {
            return level.ToString().StartsWith(
                "DicePalace", StringComparison.Ordinal);
        }

        private Rect PulseRect(Rect rect, int field)
        {
            if (field < 0 || field >= pulseUntil.Length || Time.realtimeSinceStartup >= pulseUntil[field])
                return rect;
            var remaining = pulseUntil[field] - Time.realtimeSinceStartup;
            var amount = Mathf.Sin((0.38f - remaining) / 0.38f * Mathf.PI) * 0.075f;
            var width = rect.width * (1f + amount);
            var height = rect.height * (1f + amount);
            return new Rect(rect.center.x - width * 0.5f, rect.center.y - height * 0.5f, width, height);
        }

        private int DisplayIndex(int field, int finalIndex, int length, int offset)
        {
            return revealed > field ? finalIndex : Wrap(ticker + offset, length);
        }

        private int DisplayPoolIndex(int field, int finalIndex,
            List<int> pool, int offset)
        {
            if (revealed > field || pool == null || pool.Count == 0)
                return finalIndex;

            return pool[Wrap(ticker + offset, pool.Count)];
        }

        private int CurrentRollingModifier(int bossIndex)
        {
            var valid = RouletteData.ValidModifierIndices(RouletteData.Bosses[bossIndex]);
            return valid.Count == 0 ? RouletteData.Modifiers.Length - 1 : valid[Wrap(ticker, valid.Count)];
        }

        private void DrawTexture(Rect rect, string relativePath)
        {
            var texture = GetTexture(relativePath);
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        }

        private Texture2D GetTexture(string relativePath)
        {
            Texture2D texture;
            if (textures.TryGetValue(relativePath, out texture))
                return texture;

            var path = Path.Combine(AssetsDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                textures[relativePath] = null;
                return null;
            }

            try
            {
                texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    Destroy(texture);
                    texture = null;
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning("No se pudo cargar " + path + ": " + exception.Message);
                texture = null;
            }
            textures[relativePath] = texture;
            return texture;
        }

        private IEnumerator LoadAudio()
        {
            yield return StartCoroutine(LoadClip("sounds/spin.wav", AudioType.WAV, clip => spinClip = clip));
            yield return StartCoroutine(LoadClip("sounds/selection.wav", AudioType.WAV, clip => selectionClip = clip));
            yield return StartCoroutine(LoadClip("sounds/abrir.wav", AudioType.WAV, clip => openClip = clip));
            yield return StartCoroutine(LoadClip("sounds/cerrar.wav", AudioType.WAV, clip => closeClip = clip));
            yield return StartCoroutine(LoadClip("sounds/impact_01.wav", AudioType.WAV,
                clip => battleHudImpactClip = clip));
        }

        private IEnumerator LoadClip(string relativePath, AudioType type, Action<AudioClip> assign)
        {
            var path = Path.Combine(AssetsDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                yield break;

            var www = new WWW("file:///" + path.Replace('\\', '/'));
            yield return www;
            if (string.IsNullOrEmpty(www.error))
            {
                var clip = www.GetAudioClip(false, false, type);
                if (clip != null)
                    assign(clip);
            }
            else
            {
                Logger.LogWarning("No se pudo cargar audio " + relativePath + ": " + www.error);
            }
            www.Dispose();
        }

        private void PlayNativeMenuSound(string soundName, AudioClip fallback, float volume)
        {
            try
            {
                AudioManager.Play(soundName);
            }
            catch
            {
                PlayOneShot(fallback, volume);
            }
        }

        private void RouteModAudioToGameSfxMixer()
        {
            try
            {
                var groups = AudioManagerMixer.GetGroups();
                var sfxGroup = groups != null ? groups.sfx : null;
                if (sfxGroup == null)
                {
                    Logger.LogWarning(
                        "No se encontro el grupo SFX de Cuphead; se usara la salida de audio predeterminada.");
                    return;
                }

                audioSource.outputAudioMixerGroup = sfxGroup;
                effectsAudioSource.outputAudioMixerGroup = sfxGroup;
                Logger.LogInfo(
                    "Audio del mod conectado a los volumenes Principal y Efectos de Cuphead.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "No se pudo conectar el audio del mod al mezclador SFX de Cuphead: " +
                    exception.Message);
            }
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip != null && effectsAudioSource != null)
                effectsAudioSource.PlayOneShot(clip, volume);
        }

        private void StopSpinAudio()
        {
            if (audioSource == null)
                return;
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null && stylesFont == theme.TitleFont)
                return;

            stylesFont = theme.TitleFont;
            titleStyle = NewStyle(theme.TitleFont, 34, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            subtitleStyle = NewStyle(theme.BodyFont, 18, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            bossStyle = NewStyle(theme.TitleFont, 25, TextAnchor.MiddleCenter, Ink, FontStyle.Normal);
            bodyStyle = NewStyle(theme.BodyFont, 17, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            smallStyle = NewStyle(theme.BodyFont, 13, TextAnchor.MiddleCenter, Ink, FontStyle.Normal);
            cardTitleStyle = NewStyle(theme.TitleFont, 19, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);

            buttonStyle = NewStyle(theme.TitleFont, 17, TextAnchor.MiddleCenter, Ink, FontStyle.Normal);
            buttonStyle.normal.background = MakeButtonTexture(new Color(0.90f, 0.81f, 0.62f));
            buttonStyle.hover.background = MakeButtonTexture(new Color(0.98f, 0.90f, 0.70f));
            buttonStyle.active.background = MakeButtonTexture(new Color(0.78f, 0.68f, 0.50f));
            buttonStyle.border = new RectOffset(4, 4, 4, 4);

            buttonActiveStyle = NewStyle(theme.TitleFont, 17, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            buttonActiveStyle.normal.background = MakeButtonTexture(Red);
            buttonActiveStyle.hover.background = MakeButtonTexture(new Color(0.78f, 0.17f, 0.13f));
            buttonActiveStyle.active.background = MakeButtonTexture(new Color(0.52f, 0.08f, 0.07f));
            buttonActiveStyle.border = new RectOffset(4, 4, 4, 4);
        }

        private static GUIStyle NewStyle(Font font, int size, TextAnchor alignment, Color color, FontStyle style)
        {
            var result = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = size,
                alignment = alignment,
                fontStyle = style,
                wordWrap = true
            };
            result.normal.textColor = color;
            result.hover.textColor = color;
            result.active.textColor = color;
            return result;
        }

        private Texture2D MakeButtonTexture(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.ARGB32, false);
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var border = x == 0 || x == 7 || y == 0 || y == 7;
                texture.SetPixel(x, y, border ? Ink : color);
            }
            texture.Apply(false, true);
            texture.filterMode = FilterMode.Point;
            textures["__button_" + textures.Count] = texture;
            return texture;
        }

        private void OnApplicationQuit()
        {
            RestoreOriginalTestLanguage();
        }

        private void OnDestroy()
        {
            RestoreOriginalTestLanguage();
            if (harmony != null)
                harmony.UnpatchSelf();
            if (activeInstance == this)
                activeInstance = null;
            SetNativeMapEquipEnabled(true);
            DestroyNativeRoulettePrompt();
            DestroyNativeChallengePrompt();
            DestroyBattleResultHud();
            ResetBlackAndWhiteRenderEffects();
            blackAndWhiteTransitionShader = null;
            battleHudSaturationShader = null;
            if (blackAndWhiteShaderBundle != null)
            {
                blackAndWhiteShaderBundle.Unload(true);
                blackAndWhiteShaderBundle = null;
            }

            StopSpinAudio();
            if (modLocalization != null)
            {
                modLocalization.LanguageChanged -= OnModLanguageChanged;
                modLocalization.Dispose();
                modLocalization = null;
            }
            if (theme != null)
                theme.Dispose();
            foreach (var texture in textures.Values)
            {
                if (texture != null)
                    Destroy(texture);
            }
            textures.Clear();
        }

        private static int Wrap(int value, int length)
        {
            value %= length;
            return value < 0 ? value + length : value;
        }

    }
}
