using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mx.gilomx.cuphead.bossroulette";
        public const string PluginName = "Gilomx Boss Roulette";
        public const string PluginVersion = "0.5.42";

        private const float DesignWidth = 1280f;
        private const float DesignHeight = 720f;
        // TEMPORARY TEST SELECTOR. Keep non-empty while developing a challenge.
        // Compatible bosses are still chosen randomly.
        private static readonly string ForcedTestChallenge =
            "Solo mini avión";
        private static readonly Color Ink = new Color(0.075f, 0.065f, 0.055f);
        private static readonly Color Red = new Color(0.67f, 0.12f, 0.10f);
        private static readonly Color Cream = new Color(0.94f, 0.87f, 0.70f);
        private static readonly Color Gold = new Color(0.94f, 0.72f, 0.19f);

        private readonly System.Random random = new System.Random();
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly float[] pulseUntil = new float[6];
        private ConfigEntry<KeyboardShortcut> toggleShortcut;
        private ConfigEntry<KeyboardShortcut> spinShortcut;
        private ConfigEntry<bool> autoLoad;
        private ConfigEntry<Level.Mode> difficultySetting;
        private ConfigEntry<bool> challengeSetting;
        private ConfigEntry<float> loadDelay;
        private GameTheme theme;
        private AudioSource audioSource;
        private AudioSource effectsAudioSource;
        private AudioClip spinClip;
        private AudioClip selectionClip;
        private AudioClip openClip;
        private AudioClip closeClip;
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
        private float spinStartedAt;
        private float loadAt;
        private int ticker;
        private int revealed;
        private Level.Mode difficulty = Level.Mode.Normal;
        private RouletteResult result = new RouletteResult();
        private string status = "PULSA ENTER PARA GIRAR";
        private string activeChallenge = "";
        private int activeChallengeBoss = -1;
        private bool soloMiniRestartPending;

        private string AssetsDirectory
        {
            get { return Path.Combine(Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath, "assets"); }
        }

        private void Awake()
        {
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
            uglyMode = HasForcedTestChallenge() || challengeSetting.Value;
            theme = new GameTheme();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.45f;
            audioSource.spatialBlend = 0f;
            audioSource.priority = 0;
            audioSource.ignoreListenerPause = true;
            effectsAudioSource = gameObject.AddComponent<AudioSource>();
            effectsAudioSource.playOnAwake = false;
            effectsAudioSource.volume = 1f;
            effectsAudioSource.spatialBlend = 0f;
            effectsAudioSource.priority = 0;
            effectsAudioSource.ignoreListenerPause = true;
            activeInstance = this;
            harmony = new Harmony(PluginGuid);
            var mapPauseCanPause = AccessTools.Method(typeof(MapPauseUI), "get_CanPause");
            var mapPausePostfix = AccessTools.Method(typeof(Plugin), "BlockMapPausePostfix");
            if (mapPauseCanPause != null && mapPausePostfix != null)
                harmony.Patch(mapPauseCanPause, postfix: new HarmonyMethod(mapPausePostfix));
            else
                Logger.LogWarning("Could not install the map pause guard.");

            var levelPreWin = AccessTools.Method(typeof(Level), "_OnPreWin");
            var levelPreWinPrefix = AccessTools.Method(typeof(Plugin), "ClearChallengeOnWinPrefix");
            if (levelPreWin != null && levelPreWinPrefix != null)
                harmony.Patch(levelPreWin, prefix: new HarmonyMethod(levelPreWinPrefix));
            else
                Logger.LogWarning("Could not install the challenge win guard.");

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

            StartCoroutine(LoadAudio());
            Logger.LogInfo(PluginName + " " + PluginVersion + " listo. F6 abre/cierra; F7 gira.");
        }

        private static void BlockMapPausePostfix(ref bool __result)
        {
            var plugin = activeInstance;
            if (plugin != null &&
                (plugin.visible || Time.frameCount <= plugin.suppressMapPauseUntilFrame))
                __result = false;
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
            UpdateActiveChallengeLifecycle();
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

            if (onMap && toggleShortcut.Value.IsDown())
                SetVisible(!visible);
            if (onMap && visible && !autoLoad.Value && resultReady &&
                !running && !pendingLoad && spinShortcut.Value.IsDown())
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
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                suppressMapPauseUntilFrame = Time.frameCount;
                SetVisible(false);
                return;
            }

            var moved = false;
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                navigationIndex = Wrap(navigationIndex - 1, 4);
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                navigationIndex = Wrap(navigationIndex + 1, 4);
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ChangeCurrentSetting(-1);
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                ChangeCurrentSetting(1);
                moved = true;
            }

            if (moved)
                PlayNativeMenuSound("menu_equipment_move", selectionClip, 0.45f);

            if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
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
                status = "PULSA ENTER PARA GIRAR";
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
            visible = value;
            if (visible)
            {
                navigationIndex = 3;
                cardRoll = random.Next(-4, 5);
                SetNativeMapEquipEnabled(false);
            }
            else
            {
                cardRoll = 0f;
                StartCoroutine(RestoreNativeMapEquipNextFrame());
            }
            PlayNativeMenuSound(visible ? "menu_cardup" : "menu_carddown",
                visible ? openClip : closeClip, 0.65f);
        }

        private void StartRoulette()
        {
            if (!CanUseRouletteOnMap() || running || pendingLoad)
                return;
            if (HasForcedTestChallenge())
                uglyMode = true;
            if (!visible)
                SetVisible(true);

            resultReady = false;
            result = CreateRandomResult();
            revealed = 0;
            ticker = 0;
            spinStartedAt = Time.realtimeSinceStartup;
            running = true;
            status = "¡LA RULETA ESTÁ GIRANDO!";
            ClearActiveChallenge();
            if (spinClip != null)
            {
                audioSource.clip = spinClip;
                audioSource.loop = true;
                audioSource.volume = 0.45f;
                audioSource.time = 0f;
                audioSource.Play();
            }
            else
                Logger.LogWarning("El audio de giro no esta disponible.");
        }

        private RouletteResult CreateRandomResult()
        {
            var forcedModifier = ForcedTestModifierIndex();
            var boss = forcedModifier >= 0
                ? RandomBossForModifier(forcedModifier)
                : random.Next(RouletteData.Bosses.Length);
            var weapon1 = random.Next(RouletteData.Weapons.Length - 1);
            int weapon2;
            do weapon2 = random.Next(RouletteData.Weapons.Length - 1);
            while (weapon2 == weapon1);

            var super = random.NextDouble() < 0.2
                ? RouletteData.Supers.Length - 1
                : random.Next(RouletteData.Supers.Length);
            var charm = random.NextDouble() < 0.2
                ? RouletteData.Charms.Length - 1
                : random.Next(RouletteData.Charms.Length);

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

        private static bool HasForcedTestChallenge()
        {
            return !string.IsNullOrEmpty(ForcedTestChallenge);
        }

        private static int ForcedTestModifierIndex()
        {
            if (!HasForcedTestChallenge())
                return -1;

            for (var i = 0; i < RouletteData.Modifiers.Length; i++)
            {
                if (RouletteData.Modifiers[i].Name == ForcedTestChallenge)
                    return i;
            }
            return -1;
        }

        private int RandomBossForModifier(int modifierIndex)
        {
            var compatibleBosses = new List<int>();
            for (var i = 0; i < RouletteData.Bosses.Length; i++)
            {
                if (RouletteData.ValidModifierIndices(
                    RouletteData.Bosses[i]).Contains(modifierIndex))
                    compatibleBosses.Add(i);
            }

            return compatibleBosses.Count > 0
                ? compatibleBosses[random.Next(compatibleBosses.Count)]
                : random.Next(RouletteData.Bosses.Length);
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
                PlayOneShot(selectionClip, 0.9f);
            }

            if (revealed < fields)
                return;

            running = false;
            resultReady = true;
            StopSpinAudio();
            status = autoLoad.Value ? "¡RESULTADO LISTO! PREPARANDO COMBATE..." : "¡RESULTADO LISTO!";
            if (autoLoad.Value)
            {
                pendingLoad = true;
                loadAt = Time.realtimeSinceStartup + Math.Max(0f, loadDelay.Value);
            }
        }

        private void LoadResult()
        {
            try
            {
                if (!PlayerData.Initialized || PlayerData.Data == null)
                {
                    status = "SELECCIONA PRIMERO UNA PARTIDA GUARDADA";
                    Logger.LogWarning("Selecciona primero una partida guardada.");
                    return;
                }
                if (SceneLoader.CurrentlyLoading)
                {
                    status = "CUPHEAD YA ESTÁ CARGANDO OTRA ESCENA";
                    return;
                }

                resultReady = false;
                ApplyLoadout(PlayerId.PlayerOne);
                ApplyLoadout(PlayerId.PlayerTwo);
                Level.SetCurrentMode(difficulty);
                var boss = RouletteData.Bosses[result.Boss];
                SetActiveChallenge(
                    uglyMode ? RouletteData.Modifiers[result.Modifier].Name : "",
                    result.Boss);
                Logger.LogInfo("Cargando " + boss.Character + " (" + boss.Level + ")");
                SceneLoader.LoadLevel(boss.Level, SceneLoader.Transition.Iris, SceneLoader.Icon.None);
            }
            catch (Exception exception)
            {
                status = "NO SE PUDO CARGAR. REVISA LOGOUTPUT.LOG";
                Logger.LogError(exception);
                SetVisible(true);
            }
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
            GUI.Label(new Rect(35f, 17f, 1100f, 58f), "CUPHEAD · BOSS ROULETTE", titleStyle);
            GUI.Label(new Rect(35f, 76f, 1100f, 30f), "¡EL DESTINO DECIDE TU PRÓXIMO COMBATE!", subtitleStyle);

            var bossIndex = DisplayIndex(0, result.Boss, RouletteData.Bosses.Length, 0);
            var boss = RouletteData.Bosses[bossIndex];
            DrawBossPanel(boss);

            var weapon1 = DisplayIndex(1, result.Weapon1, RouletteData.Weapons.Length, 0);
            var weapon2 = DisplayIndex(2, result.Weapon2, RouletteData.Weapons.Length, RouletteData.Weapons.Length / 2);
            var super = DisplayIndex(3, result.Super, RouletteData.Supers.Length, RouletteData.Supers.Length / 3);
            var charm = DisplayIndex(4, result.Charm, RouletteData.Charms.Length, RouletteData.Charms.Length / 4);

            DrawEquipmentCard(new Rect(360f, 127f, 225f, 151f), "ARMA A",
                RouletteData.Weapons[weapon1].Name, RouletteData.Weapons[weapon1].Image,
                RouletteData.Weapons[weapon1].NativeSprite, 1);
            DrawEquipmentCard(new Rect(607f, 127f, 225f, 151f), "ARMA B",
                RouletteData.Weapons[weapon2].Name, RouletteData.Weapons[weapon2].Image,
                RouletteData.Weapons[weapon2].NativeSprite, 2);
            DrawEquipmentCard(new Rect(854f, 127f, 225f, 151f), "SÚPER",
                RouletteData.Supers[super].Name, RouletteData.Supers[super].Image,
                RouletteData.Supers[super].NativeSprite, 3);

            var charmRect = uglyMode
                ? new Rect(484f, 302f, 225f, 151f)
                : new Rect(607f, 302f, 225f, 151f);
            DrawEquipmentCard(charmRect, "AMULETO",
                RouletteData.Charms[charm].Name, RouletteData.Charms[charm].Image,
                RouletteData.Charms[charm].NativeSprite, 4);

            if (uglyMode)
            {
                var rollingModifier = CurrentRollingModifier(bossIndex);
                var modifier = DisplayIndex(5, result.Modifier, RouletteData.Modifiers.Length, rollingModifier - ticker);
                DrawEquipmentCard(new Rect(731f, 302f, 225f, 151f), "RETO",
                    RouletteData.Modifiers[modifier].Name, RouletteData.Modifiers[modifier].Image,
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
                revealed > field ? "SELECCIONADO" : "GIRANDO...", smallStyle);
        }

        private void DrawBottomControls()
        {
            GUI.Label(new Rect(40f, 473f, 170f, 31f), "DIFICULTAD", cardTitleStyle);
            DrawModeButton(new Rect(210f, 469f, 132f, 38f), "SIMPLE", Level.Mode.Easy);
            DrawModeButton(new Rect(351f, 469f, 132f, 38f), "NORMAL", Level.Mode.Normal);
            DrawModeButton(new Rect(492f, 469f, 132f, 38f), "EXPERTO", Level.Mode.Hard);

            if (GUI.Button(new Rect(657f, 469f, 208f, 38f),
                uglyMode ? "?  MODO FEO" : "MODO FEO", uglyMode ? buttonActiveStyle : buttonStyle))
                uglyMode = !uglyMode;
            if (GUI.Button(new Rect(878f, 469f, 214f, 38f),
                autoLoad.Value ? "?  CARGA AUTO" : "CARGA AUTO", autoLoad.Value ? buttonActiveStyle : buttonStyle))
                autoLoad.Value = !autoLoad.Value;

            GUI.Label(new Rect(42f, 518f, 1050f, 31f), status, subtitleStyle);
            GUI.enabled = !running && !pendingLoad;
            var spinRect = new Rect(385f, 558f, 287f, 58f);
            if (GUI.Button(spinRect, "¡GIRAR!   F7", buttonActiveStyle))
                StartRoulette();
            GUI.enabled = true;
            if (GUI.Button(new Rect(688f, 558f, 148f, 58f), "CERRAR", buttonStyle))
                SetVisible(false);

            if (spinRect.Contains(Event.current.mousePosition))
                theme.DrawSprite("hand_cursor_boil_0001", new Rect(spinRect.x - 59f, spinRect.y + 4f, 54f, 54f), Color.white);

            GUI.Label(new Rect(35f, 625f, 1100f, 24f),
                "F6  ABRIR/CERRAR     ·     F7  GIRAR     ·     CTRL+I  SELECCIÓN FORZADA", smallStyle);
        }

        private void DrawModeButton(Rect rect, string label, Level.Mode mode)
        {
            if (GUI.Button(rect, (difficulty == mode ? "?  " : "") + label,
                difficulty == mode ? buttonActiveStyle : buttonStyle))
                difficulty = mode;
        }

        private void SetActiveChallenge(string challenge, int bossIndex)
        {
            soloMiniRestartPending = false;
            activeChallenge = challenge == "Nada" ? "" : challenge;
            activeChallengeBoss = string.IsNullOrEmpty(activeChallenge) ? -1 : bossIndex;
            if (!string.IsNullOrEmpty(activeChallenge) &&
                !PrepareNativeChallengePrompt())
                Logger.LogWarning("Could not prepare the persistent challenge prompt.");
        }

        private void ClearActiveChallenge()
        {
            soloMiniRestartPending = false;
            activeChallenge = "";
            activeChallengeBoss = -1;
            SetNativeChallengePromptVisible(false);
        }

        private static void ClearChallengeOnWinPrefix(Level __instance)
        {
            var plugin = activeInstance;
            if (plugin != null && plugin.ShouldClearChallengeOnWin(__instance))
                plugin.ClearActiveChallenge();
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
            return activeChallenge == "No Dash" && ShouldShowActiveChallenge();
        }

        private static bool BlockMiniPlanePrefix()
        {
            var plugin = activeInstance;
            return plugin == null || !plugin.ShouldBlockMiniPlane();
        }

        private bool ShouldBlockMiniPlane()
        {
            return activeChallenge == "No mini avión" &&
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
                ___damageSource == DamageDealer.DamageSource.SmallPlane ||
                !IsEnemyDamageTarget(hit))
                return;

            plugin.QueueSoloMiniRestart();
        }

        private bool ShouldRestartOnNonMiniPlaneDamage()
        {
            return activeChallenge == "Solo mini avión" &&
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
                activeChallenge == "Solo mini avión" &&
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

            var charm = RouletteData.Charms[plugin.result.Charm].Value;
            Weapon startingWeapon;
            if (plugin.activeChallenge == "No disparo bombas")
            {
                startingWeapon = charm == Charm.charm_chalice
                    ? Weapon.plane_chalice_weapon_3way
                    : Weapon.plane_weapon_peashot;
            }
            else if (plugin.activeChallenge == "No disparo Peashooter")
            {
                startingWeapon = charm == Charm.charm_chalice
                    ? Weapon.plane_chalice_weapon_bomb
                    : Weapon.plane_weapon_bomb;
            }
            else
                return;

            __instance.SwitchToWeapon(startingWeapon);
        }

        private static bool BlockPlaneWeaponSwitchPrefix()
        {
            var plugin = activeInstance;
            return plugin == null || !plugin.ShouldLockPlaneWeapon();
        }

        private bool ShouldLockPlaneWeapon()
        {
            return (activeChallenge == "No disparo bombas" ||
                    activeChallenge == "No disparo Peashooter") &&
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
            return activeChallenge == "No EX" &&
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
            if (string.IsNullOrEmpty(activeChallenge) || SceneLoader.CurrentlyLoading)
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
            if (string.IsNullOrEmpty(activeChallenge) || SceneLoader.CurrentlyLoading)
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

        private void OnDestroy()
        {
            if (harmony != null)
                harmony.UnpatchSelf();
            if (activeInstance == this)
                activeInstance = null;
            SetNativeMapEquipEnabled(true);
            DestroyNativeRoulettePrompt();
            DestroyNativeChallengePrompt();

            StopSpinAudio();
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
