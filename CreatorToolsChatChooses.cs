using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    public sealed partial class Plugin
    {
        private CreatorToolsChatChoosesController creatorToolsChatChooses;
        private ConfigEntry<bool> chatChoosesChallengeSetting;
        private float chatChoosesCatalogRefreshAt;
        private bool chatChoosesBattleSeen;

        private void InitializeChatChooses()
        {
            if (creatorToolsChatChooses == null)
            {
                chatChoosesChallengeSetting = Config.Bind("El chat elige", "Con reto", true,
                    "Incluye una ronda para votar por un reto compatible después del amuleto.");
                creatorToolsChatChooses = new CreatorToolsChatChoosesController(
                    creatorToolsInteractions.LiveEvents, chatChoosesChallengeSetting.Value);
            }
            creatorToolsServer.SetChatChoosesController(creatorToolsChatChooses,
                delegate(string query)
                {
                    var accepted = creatorToolsChatChooses.Command(query);
                    creatorToolsInteractions.PublishPeskyBattleState(creatorToolsServer);
                    return accepted;
                });
        }

        private void UpdateChatChooses()
        {
            if (creatorToolsChatChooses == null || creatorToolsServer == null) return;
            creatorToolsServer.ApplyChatChoosesMainThreadActions(delegate
            {
                var available = CanUseRouletteOnMap() && !running && !pendingLoad;
                ChatChoosesCatalog catalog = null;
                if (available && creatorToolsChatChooses.NeedsCatalog &&
                    Time.realtimeSinceStartup >= chatChoosesCatalogRefreshAt)
                {
                    EnsureCreatorToolsForceDefaults();
                    catalog = BuildChatChoosesCatalog();
                    chatChoosesCatalogRefreshAt = Time.realtimeSinceStartup + 1f;
                }
                // SetCatalog(null) only updates availability; never removes a prepared catalog.
                creatorToolsChatChooses.SetCatalog(catalog, available);
                creatorToolsChatChooses.Tick();
                if (visible && creatorToolsChatChooses.Reserved) SetVisible(false);
                if (chatChoosesChallengeSetting.Value != creatorToolsChatChooses.WithChallenge)
                {
                    chatChoosesChallengeSetting.Value = creatorToolsChatChooses.WithChallenge;
                    Config.Save();
                }
                if (chatChoosesBattleSeen && Map.Current != null && !SceneLoader.CurrentlyLoading)
                {
                    creatorToolsChatChooses.ReturnedToMap();
                    chatChoosesBattleSeen = false;
                }
                int[] chosen;
                if (!creatorToolsChatChooses.TryBeginLoad(out chosen)) return;
                // Revalidate against current DLC/catalog settings before crossing into Unity.
                if (!availableBossIndices.Contains(chosen[0]) ||
                    (chosen[1] >= 0 && !availableWeaponIndices.Contains(chosen[1])) ||
                    (chosen[2] >= 0 && !availableWeaponIndices.Contains(chosen[2])) ||
                    (chosen[3] >= 0 && !availableSuperIndices.Contains(chosen[3])) ||
                    !availableCharmIndices.Contains(chosen[4]) ||
                    (!RouletteData.Bosses[chosen[0]].IsPlane && RouletteData.Charms[chosen[4]].Value == Charm.charm_curse) ||
                    (chosen[5] >= 0 && chosen[5] != RouletteData.Modifiers.Length - 1 &&
                     !CreatorToolsValidModifierIndices(RouletteData.Bosses[chosen[0]]).Contains(chosen[5])))
                { creatorToolsChatChooses.Loaded(false); return; }
                result = new RouletteResult
                {
                    Boss = chosen[0],
                    Weapon1 = chosen[1] >= 0 ? chosen[1] : RandomNonEmptyPoolIndex(availableWeaponIndices, RouletteData.Weapons.Length - 1),
                    Weapon2 = chosen[2] >= 0 ? chosen[2] : RouletteData.Weapons.Length - 1,
                    Super = chosen[3] >= 0 ? chosen[3] : RouletteData.Supers.Length - 1,
                    Charm = chosen[4],
                    Modifier = chosen[5] >= 0 ? chosen[5] : RouletteData.Modifiers.Length - 1
                };
                uglyMode = RouletteData.Modifiers[result.Modifier].Id != ModifierId.None;
                RememberBossResult(result.Boss);
                running = false; pendingLoad = false; resultReady = false;
                if (visible) SetVisible(false);
                ClearActiveChallenge(); EndBattleResultHudSession();
                chatChoosesBattleSeen = false;
                BeginCreatorToolsInteractionGameplayLevelLoad("chat chooses result");
                creatorToolsChatChooses.Loaded(LoadResult());
                creatorToolsInteractions.PublishLiveEventsState(creatorToolsServer);
            });
        }

        private ChatChoosesCatalog BuildChatChoosesCatalog()
        {
            var catalog = new ChatChoosesCatalog();
            foreach (var key in new[] { "boss", "weapon1", "weapon2", "super", "charm", "modifier" })
                catalog.Pools[key] = new List<ChatChoice>();
            foreach (var id in availableBossIndices)
            {
                var item = RouletteData.Bosses[id];
                catalog.Pools["boss"].Add(new ChatChoice { Id = id, Name = LocalizedBossName(item), Image = item.Image, Plane = item.IsPlane });
            }
            foreach (var id in availableWeaponIndices)
            {
                var item = RouletteData.Weapons[id];
                var none = id == RouletteData.Weapons.Length - 1;
                var option = new ChatChoice { Id = id, Name = LocalizedEquipmentName(item), Image = none ? "creator-tools/empty.png" : item.Image, None = none };
                catalog.Pools["weapon1"].Add(option); catalog.Pools["weapon2"].Add(option);
            }
            foreach (var id in availableSuperIndices)
            {
                var item = RouletteData.Supers[id];
                var none = id == RouletteData.Supers.Length - 1;
                catalog.Pools["super"].Add(new ChatChoice { Id = id, Name = LocalizedEquipmentName(item), Image = none ? "creator-tools/empty.png" : item.Image, None = none });
            }
            foreach (var id in availableCharmIndices)
            {
                var item = RouletteData.Charms[id];
                var none = id == RouletteData.Charms.Length - 1;
                catalog.Pools["charm"].Add(new ChatChoice { Id = id, Name = LocalizedEquipmentName(item), Image = none ? "creator-tools/empty.png" : item.Image, None = none,
                    RandomWeapons = item.Value == Charm.charm_curse });
            }
            for (var id = 0; id < RouletteData.Modifiers.Length; id++)
            {
                var item = RouletteData.Modifiers[id];
                if (!item.Selectable || (item.Id != ModifierId.None &&
                    (!ExperimentalFeatures.IsChallengeEnabled(item.Id) || !IsCreatorToolsChallengeEnabled(item.Id)))) continue;
                catalog.Pools["modifier"].Add(new ChatChoice { Id = id, Name = item.Id == ModifierId.None ? L(ModText.CommonNone) : LocalizedModifierName(item.Id),
                    Image = item.Id == ModifierId.None ? "creator-tools/empty.png" : item.Image, None = item.Id == ModifierId.None,
                    Kind = item.Kind == ModifierKind.Plane ? "plane" : item.Kind == ModifierKind.Ground ? "ground" : "both" });
            }
            return catalog;
        }

        private void DrawChatChoosesResult()
        {
            if (creatorToolsChatChooses == null || !creatorToolsChatChooses.ShowingResult) return;
            EnsureStyles();
            // Common paper/icons, with the dark-ink styles intended for paper.
            var card = new Rect(302f, 65f, 676f, 590f);
            theme.DrawPaper(card);
            var english = Localization.language == Localization.Languages.English;
            GUI.Label(new Rect(340f, 89f, 600f, 42f), english ? "THE CHAT CHOSE" : "EL CHAT ELIGIÓ", bossStyle);
            // The selected values are obtained without parsing web JSON.
            var selection = creatorToolsChatChooses.SelectedOptions;
            if (selection[0] != null)
            {
                DrawTexture(new Rect(550f, 148f, 180f, 180f), selection[0].Image);
                GUI.Label(new Rect(342f, 319f, 596f, 45f), selection[0].Name.ToUpperInvariant(), bossStyle);
            }
            var fieldCount = selection[1] == null ? 2 : 5;
            var n = 0;
            for (var i = 1; i < 6; i++)
            {
                if (selection[1] == null && i < 4) continue;
                var x = 640f + (n++ - (fieldCount - 1) / 2f) * 113f;
                var image = selection[i] == null ? "creator-tools/empty.png" : selection[i].Image;
                DrawTexture(new Rect(x - 43f, 397f, 86f, 86f), image);
                var label = i == 1 ? L(ModText.SlotWeaponA) : i == 2 ? L(ModText.SlotWeaponB) : i == 3 ? L(ModText.SlotSuper) : i == 4 ? L(ModText.SlotCharm) : L(ModText.SlotChallenge);
                GUI.Label(new Rect(x - 53f, 483f, 106f, 35f), label, smallStyle);
                GUI.Label(new Rect(x - 53f, 514f, 106f, 35f), selection[i] == null ?
                    (english ? "No challenge" : "Sin reto") : selection[i].Name, bodyStyle);
            }
            var seconds = creatorToolsChatChooses.CountdownSeconds;
            GUI.Label(new Rect(344f, 563f, 592f, 44f), seconds > 0 ?
                (english ? "STARTING IN " : "EMPEZAMOS EN ") + seconds : english ? "GET READY!" : "¡PREPÁRATE!", bossStyle);
        }
    }
}
