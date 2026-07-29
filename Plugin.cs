using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mx.gilomx.cuphead.bossroulette";
        public const string PluginName = "Gilomx Boss Roulette";
        public const string PluginVersion = "0.1.0";

        private readonly System.Random random = new System.Random();
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private Rect window = new Rect(80f, 55f, 760f, 570f);
        private GUIStyle titleStyle;
        private GUIStyle centeredStyle;
        private GUIStyle resultStyle;
        private GUIStyle hintStyle;
        private ConfigEntry<KeyboardShortcut> toggleShortcut;
        private ConfigEntry<KeyboardShortcut> spinShortcut;
        private ConfigEntry<bool> autoLoad;
        private ConfigEntry<float> loadDelay;
        private bool visible = true;
        private bool uglyMode;
        private bool running;
        private bool secretVisible;
        private bool forceSelection;
        private bool pendingLoad;
        private float spinStartedAt;
        private float loadAt;
        private int ticker;
        private int revealed;
        private Level.Mode difficulty = Level.Mode.Normal;
        private RouletteResult result = new RouletteResult();
        private RouletteResult forced = new RouletteResult { Boss = 0, Weapon1 = 0, Weapon2 = 1, Super = 0, Charm = 0, Modifier = 6 };
        private string status = "F7 para girar";
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
            Logger.LogInfo(PluginName + " " + PluginVersion + " listo. F6 abre/cierra; F7 gira.");
        }

        private void Update()
        {
            if (toggleShortcut.Value.IsDown())
                visible = !visible;

            if (spinShortcut.Value.IsDown())
                StartRoulette();

            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.I))
                secretVisible = !secretVisible;

            if (running)
                UpdateSpin();

            if (pendingLoad && Time.realtimeSinceStartup >= loadAt)
            {
                pendingLoad = false;
                LoadResult();
            }
        }

        private void StartRoulette()
        {
            if (running || pendingLoad)
                return;

            if (!visible)
                visible = true;

            result = forceSelection ? Copy(forced) : CreateRandomResult();
            revealed = 0;
            ticker = 0;
            spinStartedAt = Time.realtimeSinceStartup;
            running = true;
            status = "Girando...";
            activeChallenge = "";
        }

        private RouletteResult CreateRandomResult()
        {
            var boss = random.Next(RouletteData.Bosses.Length);
            var weapon1 = random.Next(RouletteData.Weapons.Length - 1);
            int weapon2;
            do weapon2 = random.Next(RouletteData.Weapons.Length - 1);
            while (weapon2 == weapon1);

            // Reproduce literalmente las probabilidades de la web: el 20 % fuerza
            // "Nada"; la rama restante todavÃ­a puede caer en "Nada".
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

            if (elapsed >= 5f)
                revealed = Math.Min(fields, (int)(elapsed - 5f) + 1);

            if (revealed < fields)
                return;

            running = false;
            status = autoLoad.Value ? "Resultado listo. Cargando combate..." : "Resultado listo";
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
                    status = "Selecciona primero una partida guardada.";
                    Logger.LogWarning(status);
                    return;
                }

                if (SceneLoader.CurrentlyLoading)
                {
                    status = "Cuphead ya estÃ¡ cargando otra escena.";
                    return;
                }

                ApplyLoadout(PlayerId.PlayerOne);
                ApplyLoadout(PlayerId.PlayerTwo);
                Level.SetCurrentMode(difficulty);
                activeChallenge = uglyMode ? RouletteData.Modifiers[result.Modifier].Name : "";
                visible = false;

                var boss = RouletteData.Bosses[result.Boss];
                Logger.LogInfo("Cargando " + boss.Character + " (" + boss.Level + ")");
                SceneLoader.LoadLevel(boss.Level, SceneLoader.Transition.Iris, SceneLoader.Icon.None);
            }
            catch (Exception exception)
            {
                status = "No se pudo cargar el combate. Revisa LogOutput.log.";
                Logger.LogError(exception);
                visible = true;
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
            EnsureStyles();

            if (!string.IsNullOrEmpty(activeChallenge) && activeChallenge != "Nada")
            {
                GUI.Box(new Rect(Screen.width / 2f - 170f, 12f, 340f, 38f), "RETO: " + activeChallenge, resultStyle);
            }

            if (!visible)
            {
                GUI.Label(new Rect(12f, Screen.height - 30f, 300f, 22f), "F6 Â· Abrir ruleta", hintStyle);
                return;
            }

            window = GUI.Window(GetInstanceID(), window, DrawWindow, "RULETA DE JEFES Â· CUPHEAD");
        }

        private void DrawWindow(int id)
        {
            var bossIndex = DisplayIndex(0, result.Boss, RouletteData.Bosses.Length, 0);
            var boss = RouletteData.Bosses[bossIndex];

            GUI.Label(new Rect(20f, 28f, 720f, 30f), boss.Character, titleStyle);
            GUI.Label(new Rect(20f, 58f, 720f, 24f), boss.Fight, centeredStyle);
            DrawTexture(new Rect(320f, 88f, 120f, 120f), boss.Image);

            var planeHint = boss.IsPlane && revealed > 0 ? "JEFE DE AVIÃ“N Â· el armamento terrestre no se usa en este combate" : "";
            GUI.Label(new Rect(20f, 210f, 720f, 22f), planeHint, hintStyle);

            var weapon1 = DisplayIndex(1, result.Weapon1, RouletteData.Weapons.Length, 0);
            var weapon2 = DisplayIndex(2, result.Weapon2, RouletteData.Weapons.Length, RouletteData.Weapons.Length / 2);
            var super = DisplayIndex(3, result.Super, RouletteData.Supers.Length, RouletteData.Supers.Length / 3);
            var charm = DisplayIndex(4, result.Charm, RouletteData.Charms.Length, RouletteData.Charms.Length / 4);

            DrawCard(20f, 240f, "ARMA A", RouletteData.Weapons[weapon1].Name, RouletteData.Weapons[weapon1].Image);
            DrawCard(165f, 240f, "ARMA B", RouletteData.Weapons[weapon2].Name, RouletteData.Weapons[weapon2].Image);
            DrawCard(310f, 240f, "SÃšPER", RouletteData.Supers[super].Name, RouletteData.Supers[super].Image);
            DrawCard(455f, 240f, "AMULETO", RouletteData.Charms[charm].Name, RouletteData.Charms[charm].Image);

            if (uglyMode)
            {
                var rollingModifier = CurrentRollingModifier(bossIndex);
                var modifier = DisplayIndex(5, result.Modifier, RouletteData.Modifiers.Length, rollingModifier - ticker);
                DrawCard(600f, 240f, "RETO", RouletteData.Modifiers[modifier].Name, RouletteData.Modifiers[modifier].Image);
            }

            GUI.Label(new Rect(20f, 390f, 150f, 22f), "Dificultad:", centeredStyle);
            if (GUI.Toggle(new Rect(175f, 390f, 90f, 24f), difficulty == Level.Mode.Easy, "Simple"))
                difficulty = Level.Mode.Easy;
            if (GUI.Toggle(new Rect(270f, 390f, 90f, 24f), difficulty == Level.Mode.Normal, "Normal"))
                difficulty = Level.Mode.Normal;
            if (GUI.Toggle(new Rect(365f, 390f, 90f, 24f), difficulty == Level.Mode.Hard, "Experto"))
                difficulty = Level.Mode.Hard;
            uglyMode = GUI.Toggle(new Rect(500f, 390f, 220f, 24f), uglyMode, " Modo feo (retos)");

            GUI.Label(new Rect(20f, 420f, 720f, 25f), status, centeredStyle);
            GUI.enabled = !running && !pendingLoad;
            if (GUI.Button(new Rect(220f, 452f, 200f, 42f), "GIRAR Â· F7"))
                StartRoulette();
            if (GUI.Button(new Rect(430f, 452f, 110f, 42f), "CERRAR"))
                visible = false;
            GUI.enabled = true;

            if (secretVisible)
                DrawSecretPanel();

            GUI.Label(new Rect(20f, 535f, 720f, 20f), "F6 abrir/cerrar Â· F7 girar Â· Ctrl+I selecciÃ³n forzada", hintStyle);
            GUI.DragWindow(new Rect(0f, 0f, 760f, 28f));
        }

        private void DrawCard(float x, float y, string heading, string value, string image)
        {
            GUI.Box(new Rect(x, y, 130f, 140f), "");
            GUI.Label(new Rect(x + 5f, y + 5f, 120f, 20f), heading, hintStyle);
            DrawTexture(new Rect(x + 37f, y + 30f, 56f, 56f), image);
            GUI.Label(new Rect(x + 5f, y + 92f, 120f, 40f), value, centeredStyle);
        }

        private void DrawSecretPanel()
        {
            GUI.Box(new Rect(15f, 500f, 730f, 32f), "");
            forceSelection = GUI.Toggle(new Rect(25f, 505f, 145f, 22f), forceSelection, " Forzar resultado");
            if (!forceSelection)
                return;

            if (GUI.Button(new Rect(175f, 504f, 28f, 24f), "â€¹")) forced.Boss = Wrap(forced.Boss - 1, RouletteData.Bosses.Length);
            GUI.Label(new Rect(205f, 505f, 250f, 22f), RouletteData.Bosses[forced.Boss].Character, centeredStyle);
            if (GUI.Button(new Rect(458f, 504f, 28f, 24f), "â€º")) forced.Boss = Wrap(forced.Boss + 1, RouletteData.Bosses.Length);
            if (GUI.Button(new Rect(500f, 504f, 225f, 24f), "Copiar equipo visible"))
            {
                forced.Weapon1 = result.Weapon1;
                forced.Weapon2 = result.Weapon2;
                forced.Super = result.Super;
                forced.Charm = result.Charm;
                forced.Modifier = result.Modifier;
            }
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

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.86f, 0.28f) }
            };
            centeredStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            resultStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.86f, 0.28f) }
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = new Color(0.82f, 0.82f, 0.82f) }
            };
        }

        private static int Wrap(int value, int length)
        {
            value %= length;
            return value < 0 ? value + length : value;
        }

        private static RouletteResult Copy(RouletteResult source)
        {
            return new RouletteResult
            {
                Boss = source.Boss,
                Weapon1 = source.Weapon1,
                Weapon2 = source.Weapon2,
                Super = source.Super,
                Charm = source.Charm,
                Modifier = source.Modifier
            };
        }
    }
}

