using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mx.gilomx.cuphead.bossroulette";
        public const string PluginName = "Gilomx Boss Roulette";
        public const string PluginVersion = "0.4.0";

        private const float DesignWidth = 1280f;
        private const float DesignHeight = 720f;
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
        private ConfigEntry<float> loadDelay;
        private GameTheme theme;
        private AudioSource audioSource;
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
        private GUIStyle challengeStyle;
        private Font stylesFont;
        private bool visible = true;
        private float cardVisibility = 1f;
        private int navigationIndex = 4;
        private int lastSettingsIndex = 1;
        private bool uglyMode;
        private bool running;
        private bool pendingLoad;
        private float spinStartedAt;
        private float loadAt;
        private int ticker;
        private int revealed;
        private Level.Mode difficulty = Level.Mode.Normal;
        private RouletteResult result = new RouletteResult();
        private string status = "PULSA ENTER PARA GIRAR";
        private string activeChallenge = "";

        private string AssetsDirectory
        {
            get { return Path.Combine(Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath, "assets"); }
        }

        private void Awake()
        {
            toggleShortcut = Config.Bind("Controles", "AbrirCerrar", new KeyboardShortcut(KeyCode.F6), "Abre o cierra la ruleta.");
            spinShortcut = Config.Bind("Controles", "Girar", new KeyboardShortcut(KeyCode.F7), "Inicia un giro.");
            autoLoad = Config.Bind("Juego", "CargarAutomaticamente", true, "Carga el jefe al finalizar el giro.");
            loadDelay = Config.Bind("Juego", "DemoraAntesDeCargar", 1.25f, "Segundos entre el resultado final y la carga.");
            theme = new GameTheme();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.45f;
            StartCoroutine(LoadAudio());
            Logger.LogInfo(PluginName + " " + PluginVersion + " listo. F6 abre/cierra; F7 gira.");
        }

        private void Update()
        {
            cardVisibility = Mathf.MoveTowards(cardVisibility, visible ? 1f : 0f,
                Time.unscaledDeltaTime / 0.42f);

            if (toggleShortcut.Value.IsDown())
                SetVisible(!visible);
            if (spinShortcut.Value.IsDown())
                StartRoulette();
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
                SetVisible(false);
                return;
            }

            var moved = false;
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (navigationIndex == 4)
                    navigationIndex = lastSettingsIndex;
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (navigationIndex < 4)
                {
                    lastSettingsIndex = navigationIndex;
                    navigationIndex = 4;
                }
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (navigationIndex == 4)
                    navigationIndex = lastSettingsIndex;
                navigationIndex = Wrap(navigationIndex - 1, 4);
                lastSettingsIndex = navigationIndex;
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (navigationIndex == 4)
                    navigationIndex = lastSettingsIndex;
                navigationIndex = Wrap(navigationIndex + 1, 4);
                lastSettingsIndex = navigationIndex;
                moved = true;
            }

            if (moved)
                PlayOneShot(selectionClip, 0.45f);

            if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
                return;

            switch (navigationIndex)
            {
                case 0:
                    difficulty = Level.Mode.Easy;
                    break;
                case 1:
                    difficulty = Level.Mode.Normal;
                    break;
                case 2:
                    difficulty = Level.Mode.Hard;
                    break;
                case 3:
                    uglyMode = !uglyMode;
                    break;
                default:
                    StartRoulette();
                    break;
            }
            PlayOneShot(selectionClip, 0.65f);
        }
        private void SetVisible(bool value)
        {
            if (visible == value)
                return;
            visible = value;
            if (visible)
                navigationIndex = 4;
            PlayOneShot(visible ? openClip : closeClip, 0.65f);
        }

        private void StartRoulette()
        {
            if (running || pendingLoad)
                return;
            if (!visible)
                SetVisible(true);

            result = CreateRandomResult();
            revealed = 0;
            ticker = 0;
            spinStartedAt = Time.realtimeSinceStartup;
            running = true;
            status = "¡LA RULETA ESTÁ GIRANDO!";
            activeChallenge = "";
            if (spinClip != null)
            {
                audioSource.clip = spinClip;
                audioSource.loop = true;
                audioSource.volume = 0.45f;
                audioSource.Play();
            }
        }

        private RouletteResult CreateRandomResult()
        {
            var boss = random.Next(RouletteData.Bosses.Length);
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
            var modifier = RouletteData.Modifiers.Length - 1;

            if (uglyMode && random.NextDouble() >= 0.3)
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

                ApplyLoadout(PlayerId.PlayerOne);
                ApplyLoadout(PlayerId.PlayerTwo);
                Level.SetCurrentMode(difficulty);
                activeChallenge = uglyMode ? RouletteData.Modifiers[result.Modifier].Name : "";
                var boss = RouletteData.Bosses[result.Boss];
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
            loadout.HasEquippedSecondaryRegularWeapon = loadout.secondaryWeapon != Weapon.None;
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

            if (!string.IsNullOrEmpty(activeChallenge) && activeChallenge != "Nada")
                DrawChallengeBanner();

            if (cardVisibility > 0.001f)
                DrawRoulette();
            else
                GUI.Label(new Rect(24f, 672f, 250f, 28f), "F6  ABRIR RULETA", smallStyle);

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
                uglyMode ? "✓  MODO FEO" : "MODO FEO", uglyMode ? buttonActiveStyle : buttonStyle))
                uglyMode = !uglyMode;
            if (GUI.Button(new Rect(878f, 469f, 214f, 38f),
                autoLoad.Value ? "✓  CARGA AUTO" : "CARGA AUTO", autoLoad.Value ? buttonActiveStyle : buttonStyle))
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
            if (GUI.Button(rect, (difficulty == mode ? "✓  " : "") + label,
                difficulty == mode ? buttonActiveStyle : buttonStyle))
                difficulty = mode;
        }

        private void DrawChallengeBanner()
        {
            var rect = new Rect(408f, 18f, 464f, 53f);
            GUI.color = Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GameTheme.DrawBorder(rect, Gold, 3f);
            GUI.Label(rect, "RETO: " + activeChallenge.ToUpperInvariant(), challengeStyle);
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
            yield return StartCoroutine(LoadClip("sounds/spin.mp3", AudioType.MPEG, clip => spinClip = clip));
            yield return StartCoroutine(LoadClip("sounds/selection.mp3", AudioType.MPEG, clip => selectionClip = clip));
            yield return StartCoroutine(LoadClip("sounds/abrir.mp3", AudioType.MPEG, clip => openClip = clip));
            yield return StartCoroutine(LoadClip("sounds/cerrar.mp3", AudioType.MPEG, clip => closeClip = clip));
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

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip, volume);
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
            challengeStyle = NewStyle(theme.TitleFont, 22, TextAnchor.MiddleCenter, Gold, FontStyle.Normal);

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
