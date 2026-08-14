using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace Gilomx.CupheadBossRoulette
{
    internal enum CreatorToolsAlignment
    {
        Left,
        Center,
        Right
    }

    internal enum CreatorToolsOrder
    {
        IconsAbove,
        TextAbove
    }

    public sealed partial class Plugin
    {
        private const int CreatorToolsDefaultPort = 18081;
        private const int CreatorToolsPortCandidates = 100;

        private ConfigEntry<bool> creatorToolsEnabledSetting;
        private ConfigEntry<int> creatorToolsResolvedPortSetting;
        private ConfigEntry<int> creatorToolsScaleSetting;
        private ConfigEntry<CreatorToolsOrder> creatorToolsOrderSetting;
        private ConfigEntry<CreatorToolsAlignment>
            creatorToolsAlignmentSetting;
        private ConfigEntry<int> creatorToolsOpacitySetting;
        private ConfigEntry<bool> creatorToolsPreviewSetting;

        private CreatorToolsServer creatorToolsServer;
        private bool creatorToolsBattleSessionActive;
        private bool creatorToolsBattleVisible;
        private int creatorToolsBattleSessionId;
        private int creatorToolsRevealedIcons;
        private bool creatorToolsTextVisible;
        private string creatorToolsLastPublishedState;
        private string creatorToolsLabelKey;
        private int creatorToolsLabelRevision;
        private bool creatorToolsLabelRenderFailureLogged;
        private string creatorToolsServerError;
        private bool creatorToolsPortChanged;

        private void InitializeCreatorTools()
        {
            creatorToolsEnabledSetting = Config.Bind(
                "Creator Tools", "Activado", false,
                "Activa el overlay local para OBS.");
            creatorToolsResolvedPortSetting = Config.Bind(
                "Creator Tools", "PuertoResuelto",
                CreatorToolsDefaultPort,
                "Puerto local resuelto automaticamente. No necesita editarse.");
            creatorToolsScaleSetting = Config.Bind(
                "Creator Tools", "Tamano", 1,
                "Escala del overlay: 1 o 2.");
            creatorToolsOrderSetting = Config.Bind(
                "Creator Tools", "Orden", CreatorToolsOrder.IconsAbove,
                "Distribucion vertical del overlay.");
            creatorToolsAlignmentSetting = Config.Bind(
                "Creator Tools", "Alineacion",
                CreatorToolsAlignment.Center,
                "Alineacion horizontal del overlay.");
            creatorToolsOpacitySetting = Config.Bind(
                "Creator Tools", "Opacidad", 100,
                "Opacidad del overlay: 25, 50, 75 o 100.");
            creatorToolsPreviewSetting = Config.Bind(
                "Creator Tools", "VistaPrevia", false,
                "Muestra un resultado simulado mientras no hay combate.");

            NormalizeCreatorToolsSettings();
            if (creatorToolsEnabledSetting.Value)
                StartCreatorToolsServer();
        }

        private void NormalizeCreatorToolsSettings()
        {
            creatorToolsScaleSetting.Value =
                creatorToolsScaleSetting.Value == 2 ? 2 : 1;
            var opacity = creatorToolsOpacitySetting.Value;
            opacity = Mathf.Clamp(opacity, 25, 100);
            opacity = Mathf.RoundToInt(opacity / 25f) * 25;
            creatorToolsOpacitySetting.Value = opacity;
            var port = creatorToolsResolvedPortSetting.Value;
            if (port < 1024 || port > 65535)
                creatorToolsResolvedPortSetting.Value =
                    CreatorToolsDefaultPort;
        }

        private bool StartCreatorToolsServer()
        {
            creatorToolsServerError = null;
            creatorToolsPortChanged = false;
            if (creatorToolsServer == null)
            {
                creatorToolsServer = new CreatorToolsServer(
                    AssetsDirectory,
                    delegate(string message) { Logger.LogInfo(message); },
                    delegate(string message) { Logger.LogWarning(message); });
            }
            if (creatorToolsServer.IsRunning)
                return true;

            var preferredPort = creatorToolsResolvedPortSetting.Value;
            if (!creatorToolsServer.Start(
                preferredPort, CreatorToolsPortCandidates))
            {
                creatorToolsServerError =
                    "NO HAY UN PUERTO DISPONIBLE";
                Logger.LogWarning(
                    "Creator Tools could not find an available local port.");
                return false;
            }

            if (creatorToolsServer.Port != preferredPort)
            {
                creatorToolsPortChanged = true;
                creatorToolsResolvedPortSetting.Value =
                    creatorToolsServer.Port;
            }
            PublishCreatorToolsState(true);
            return true;
        }

        private void StopCreatorToolsServer()
        {
            if (creatorToolsServer != null)
                creatorToolsServer.Stop();
            creatorToolsLastPublishedState = null;
        }

        private void SetCreatorToolsEnabled(bool enabled)
        {
            if (creatorToolsEnabledSetting.Value != enabled)
                creatorToolsEnabledSetting.Value = enabled;
            if (enabled)
            {
                if (!StartCreatorToolsServer())
                    creatorToolsEnabledSetting.Value = false;
            }
            else
                StopCreatorToolsServer();
        }

        private void UpdateCreatorTools()
        {
            if (creatorToolsEnabledSetting == null)
                return;

            if (creatorToolsEnabledSetting.Value)
            {
                if (creatorToolsServer == null ||
                    !creatorToolsServer.IsRunning)
                    StartCreatorToolsServer();
            }
            else if (creatorToolsServer != null &&
                     creatorToolsServer.IsRunning)
                StopCreatorToolsServer();

            UpdateCreatorToolsChallengeLabel();
        }

        private void DisposeCreatorTools()
        {
            if (creatorToolsServer == null)
                return;
            creatorToolsServer.Dispose();
            creatorToolsServer = null;
        }

        private string CreatorToolsUrl
        {
            get
            {
                var port = creatorToolsServer != null &&
                           creatorToolsServer.IsRunning
                    ? creatorToolsServer.Port
                    : creatorToolsResolvedPortSetting == null
                        ? CreatorToolsDefaultPort
                        : creatorToolsResolvedPortSetting.Value;
                return "http://127.0.0.1:" + port + "/";
            }
        }

        private void BeginCreatorToolsBattleSession()
        {
            creatorToolsBattleSessionActive = true;
            creatorToolsBattleVisible = false;
            creatorToolsBattleSessionId++;
            creatorToolsRevealedIcons = 0;
            creatorToolsTextVisible = false;
            creatorToolsLabelKey = null;
            if (creatorToolsPreviewSetting != null &&
                creatorToolsPreviewSetting.Value)
                creatorToolsPreviewSetting.Value = false;
            PublishCreatorToolsState(true);
        }

        private void SetCreatorToolsBattleVisibility(bool visible)
        {
            if (!creatorToolsBattleSessionActive ||
                creatorToolsBattleVisible == visible)
                return;
            creatorToolsBattleVisible = visible;
            PublishCreatorToolsState(false);
        }

        private void UpdateCreatorToolsBattleReveal(
            int revealedIcons, bool textVisible)
        {
            if (!creatorToolsBattleSessionActive)
                return;
            if (creatorToolsRevealedIcons == revealedIcons &&
                creatorToolsTextVisible == textVisible)
                return;
            creatorToolsRevealedIcons = revealedIcons;
            creatorToolsTextVisible = textVisible;
            PublishCreatorToolsState(false);
        }

        private void EndCreatorToolsBattleSession()
        {
            creatorToolsBattleSessionActive = false;
            creatorToolsBattleVisible = false;
            creatorToolsRevealedIcons = 0;
            creatorToolsTextVisible = false;
            creatorToolsLabelKey = null;
            PublishCreatorToolsState(true);
        }

        private void CreatorToolsLanguageChanged()
        {
            creatorToolsLabelKey = null;
            PublishCreatorToolsState(true);
        }

        private void PublishCreatorToolsState(bool force)
        {
            if (creatorToolsServer == null ||
                !creatorToolsServer.IsRunning)
                return;

            var json = BuildCreatorToolsStateJson();
            if (!force && json == creatorToolsLastPublishedState)
                return;
            creatorToolsLastPublishedState = json;
            creatorToolsServer.Publish(json);
        }

        private string BuildCreatorToolsStateJson()
        {
            var preview = !creatorToolsBattleSessionActive &&
                          creatorToolsPreviewSetting.Value;
            var visible = creatorToolsBattleSessionActive
                ? creatorToolsBattleVisible
                : preview;
            var icons = preview
                ? CreatorToolsPreviewIcons()
                : CreatorToolsBattleIcons();
            var challengeText = preview
                ? LocalizedChallengeLabel(ModifierId.NoDash)
                    .ToUpperInvariant()
                : CreatorToolsBattleChallengeText();
            var revealed = preview
                ? icons.Count
                : creatorToolsRevealedIcons;
            var textVisible = preview || creatorToolsTextVisible;
            var session = preview
                ? -1
                : creatorToolsBattleSessionId;

            var builder = new StringBuilder(512);
            builder.Append("{\"type\":\"state\",\"active\":true");
            builder.Append(",\"visible\":").Append(
                visible ? "true" : "false");
            builder.Append(",\"preview\":").Append(
                preview ? "true" : "false");
            builder.Append(",\"session\":").Append(session);
            builder.Append(",\"revealed\":").Append(revealed);
            builder.Append(",\"textVisible\":").Append(
                textVisible ? "true" : "false");
            builder.Append(",\"challengeText\":\"")
                .Append(EscapeJson(challengeText)).Append("\"");
            builder.Append(",\"labelRevision\":")
                .Append(creatorToolsLabelRevision);
            builder.Append(",\"icons\":[");
            for (var i = 0; i < icons.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append('"').Append(EscapeJson(icons[i])).Append('"');
            }
            builder.Append(']');
            builder.Append(",\"settings\":{");
            builder.Append("\"scale\":")
                .Append(creatorToolsScaleSetting.Value);
            builder.Append(",\"textFirst\":").Append(
                creatorToolsOrderSetting.Value ==
                CreatorToolsOrder.TextAbove ? "true" : "false");
            builder.Append(",\"alignment\":\"")
                .Append(CreatorToolsAlignmentValue()).Append("\"");
            builder.Append(",\"opacity\":")
                .Append((creatorToolsOpacitySetting.Value / 100f)
                    .ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append("}}");
            return builder.ToString();
        }

        private List<string> CreatorToolsPreviewIcons()
        {
            return new List<string>
            {
                RouletteData.Weapons[0].Image,
                RouletteData.Weapons[1].Image,
                RouletteData.Supers[0].Image,
                RouletteData.Charms[0].Image,
                RouletteData.Modifiers[0].Image
            };
        }

        private List<string> CreatorToolsBattleIcons()
        {
            var icons = new List<string>();
            var snapshot = battleHudResultSnapshot;
            if (snapshot == null || snapshot.Boss < 0 ||
                snapshot.Boss >= RouletteData.Bosses.Length)
                return icons;

            var boss = RouletteData.Bosses[snapshot.Boss];
            if (!boss.IsPlane)
            {
                icons.Add(RouletteData.Weapons[ClampIndex(
                    snapshot.Weapon1, RouletteData.Weapons.Length)].Image);
                icons.Add(RouletteData.Weapons[ClampIndex(
                    snapshot.Weapon2, RouletteData.Weapons.Length)].Image);
                icons.Add(RouletteData.Supers[ClampIndex(
                    snapshot.Super, RouletteData.Supers.Length)].Image);
            }
            icons.Add(RouletteData.Charms[ClampIndex(
                snapshot.Charm, RouletteData.Charms.Length)].Image);
            icons.Add(CreatorToolsChallengeIcon(snapshot));
            return icons;
        }

        private string CreatorToolsChallengeIcon(RouletteResult snapshot)
        {
            if (battleHudChallengeSnapshot == ModifierId.None)
                return "weapons/vacio.png";
            if (snapshot.Modifier >= 0 &&
                snapshot.Modifier < RouletteData.Modifiers.Length)
                return RouletteData.Modifiers[snapshot.Modifier].Image;
            for (var i = 0; i < RouletteData.Modifiers.Length; i++)
            {
                if (RouletteData.Modifiers[i].Id ==
                    battleHudChallengeSnapshot)
                    return RouletteData.Modifiers[i].Image;
            }
            return "weapons/vacio.png";
        }

        private string CreatorToolsBattleChallengeText()
        {
            return battleHudChallengeSnapshot == ModifierId.None
                ? string.Empty
                : LocalizedChallengeLabel(battleHudChallengeSnapshot)
                    .ToUpperInvariant();
        }

        private string CreatorToolsAlignmentValue()
        {
            if (creatorToolsAlignmentSetting.Value ==
                CreatorToolsAlignment.Left)
                return "left";
            if (creatorToolsAlignmentSetting.Value ==
                CreatorToolsAlignment.Right)
                return "right";
            return "center";
        }

        private static int ClampIndex(int value, int length)
        {
            return Math.Max(0, Math.Min(length - 1, value));
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            var builder = new StringBuilder(value.Length + 8);
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 32)
                            builder.Append("\\u")
                                .Append(((int)character).ToString("x4"));
                        else
                            builder.Append(character);
                        break;
                }
            }
            return builder.ToString();
        }

        private void UpdateCreatorToolsChallengeLabel()
        {
            if (creatorToolsServer == null ||
                !creatorToolsServer.IsRunning ||
                !creatorToolsBattleSessionActive ||
                battleHudChallengeText == null)
                return;

            var label = CreatorToolsBattleChallengeText();
            if (string.IsNullOrEmpty(label))
            {
                if (creatorToolsLabelKey != string.Empty)
                {
                    creatorToolsLabelKey = string.Empty;
                    creatorToolsLabelRevision = 0;
                    creatorToolsServer.SetChallengeLabel(null, 0);
                    PublishCreatorToolsState(true);
                }
                return;
            }

            var sourceFont = battleHudChallengeText.font;
            var key = label + "|" +
                      (sourceFont == null ? 0 : sourceFont.GetInstanceID()) +
                      "|" + creatorToolsScaleSetting.Value;
            if (key == creatorToolsLabelKey)
                return;

            try
            {
                var png = RenderCreatorToolsLabelPng(
                    battleHudChallengeText, label);
                if (png == null || png.Length == 0)
                    return;
                creatorToolsLabelKey = key;
                creatorToolsLabelRevision++;
                creatorToolsServer.SetChallengeLabel(
                    png, creatorToolsLabelRevision);
                PublishCreatorToolsState(true);
            }
            catch (Exception exception)
            {
                if (!creatorToolsLabelRenderFailureLogged)
                {
                    creatorToolsLabelRenderFailureLogged = true;
                    Logger.LogWarning(
                        "Creator Tools could not render the native challenge " +
                        "label: " + exception.Message);
                }
            }
        }

        private static byte[] RenderCreatorToolsLabelPng(
            Text source, string label)
        {
            if (source == null || source.font == null ||
                string.IsNullOrEmpty(label))
                return null;

            const int renderScale = 4;
            const int renderLayer = 31;
            source.font.RequestCharactersInTexture(
                label, source.fontSize * renderScale, source.fontStyle);

            GameObject cameraObject = null;
            GameObject canvasObject = null;
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            var previousActive = RenderTexture.active;
            try
            {
                canvasObject = new GameObject(
                    "Gilomx Creator Tools Label Canvas",
                    typeof(RectTransform), typeof(Canvas));
                canvasObject.layer = renderLayer;
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;

                var labelObject = UnityEngine.Object.Instantiate(
                    source.gameObject);
                labelObject.name = "Gilomx Creator Tools Label";
                labelObject.layer = renderLayer;
                labelObject.transform.SetParent(canvasObject.transform, false);
                var text = labelObject.GetComponent<Text>();
                text.text = label;
                text.fontSize = source.fontSize * renderScale;
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;

                var width = Mathf.Clamp(
                    Mathf.CeilToInt(text.preferredWidth + 24f), 16, 2048);
                var height = Mathf.Clamp(
                    Mathf.CeilToInt(text.preferredHeight + 24f), 16, 256);
                var canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(width, height);
                var textRect = text.rectTransform;
                textRect.anchorMin = new Vector2(0.5f, 0.5f);
                textRect.anchorMax = new Vector2(0.5f, 0.5f);
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = new Vector2(width, height);
                textRect.localScale = Vector3.one;

                cameraObject = new GameObject(
                    "Gilomx Creator Tools Label Camera", typeof(Camera));
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f;
                camera.aspect = width / (float)height;
                camera.cullingMask = 1 << renderLayer;
                camera.transform.position = new Vector3(0f, 0f, -10f);

                renderTexture = new RenderTexture(
                    width, height, 0, RenderTextureFormat.ARGB32);
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(
                    width, height, TextureFormat.ARGB32, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
                if (canvasObject != null)
                    UnityEngine.Object.DestroyImmediate(canvasObject);
                if (cameraObject != null)
                    UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
