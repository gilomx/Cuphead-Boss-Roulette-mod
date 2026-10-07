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
        private bool chatChoosesReviewCard;
        private int chatChoosesCardSession = -1;
        private bool chatChoosesOriginalUglyMode;
        private readonly ChatChoosesCardEntrance chatChoosesCardEntrance = new ChatChoosesCardEntrance();

        private void InitializeChatChooses()
        {
            if (creatorToolsChatChooses == null)
            {
                chatChoosesChallengeSetting = Config.Bind("El chat elige", "Con reto", true,
                    "Incluye una ronda para votar por un reto compatible después del amuleto.");
#if PICHI_LAUNCHER_DEV
                const bool developmentTools = true;
#else
                const bool developmentTools = false;
#endif
                creatorToolsChatChooses = new CreatorToolsChatChoosesController(
                    creatorToolsInteractions.LiveEvents, chatChoosesChallengeSetting.Value,
                    developmentTools: developmentTools);
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
                if (chatChoosesCardSession >= 0 && (!creatorToolsChatChooses.Reserved ||
                    chatChoosesCardSession != creatorToolsChatChooses.SessionId)) ClearChatChoosesCard();
                if (visible && creatorToolsChatChooses.Reserved && !chatChoosesReviewCard) SetVisible(false);
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
                if (!available || !creatorToolsChatChooses.TryGetResult(out chosen))
                {
                    chatChoosesCardEntrance.Suspend();
                    return;
                }
                if (chatChoosesCardSession != creatorToolsChatChooses.SessionId)
                {
                    if (!IsChatChoosesSelectionValid(chosen))
                    {
                        creatorToolsChatChooses.InvalidateResult();
                        return;
                    }
                    chatChoosesOriginalUglyMode = uglyMode;
                    chatChoosesCardSession = creatorToolsChatChooses.SessionId;
                    chatChoosesReviewCard = true;
                    result = CreateChatChoosesResult(chosen);
                    uglyMode = RouletteData.Modifiers[result.Modifier].Id != ModifierId.None;
                    RememberBossResult(result.Boss);
                    running = false; pendingLoad = false; revealed = 6;
                    Array.Clear(pulseUntil, 0, pulseUntil.Length);
                    resultReady = false;
                }
                if (!resultReady)
                {
                    resultReady = true; status = RouletteStatus.ResultReady;
                    cardVisibility = 0f;
                    chatChoosesCardEntrance.Begin();
                }
                if (chatChoosesCardEntrance.ShouldOpen(creatorToolsApplicationFocused, available, Time.realtimeSinceStartup))
                {
                    cardVisibility = 0f;
                    SetVisible(true);
                }
            });
        }

        private RouletteResult CreateChatChoosesResult(int[] chosen)
        {
            var selected = new RouletteResult {
                Boss = chosen[0], Weapon1 = chosen[1], Weapon2 = chosen[2], Super = chosen[3],
                Charm = chosen[4], Modifier = chosen[5] >= 0 ? chosen[5] : RouletteData.Modifiers.Length - 1
            };
            if (!RouletteData.Bosses[selected.Boss].IsPlane) return selected;
            // Plane rounds never choose these slots. Keep the current equipment
            // internally; the card projects them as empty and ApplyLoadout leaves
            // each player's own weapons/super untouched.
            var loadout = PlayerData.Data.Loadouts.GetPlayerLoadout(PlayerId.PlayerOne);
            selected.Weapon1 = loadout == null ? -1 : Array.FindIndex(RouletteData.Weapons, item => item.Value.Equals(loadout.primaryWeapon));
            selected.Weapon2 = loadout == null ? -1 : Array.FindIndex(RouletteData.Weapons, item => item.Value.Equals(loadout.secondaryWeapon));
            selected.Super = loadout == null ? -1 : Array.FindIndex(RouletteData.Supers, item => item.Value.Equals(loadout.super));
            if (selected.Weapon1 < 0) selected.Weapon1 = RouletteData.Weapons.Length - 1;
            if (selected.Weapon2 < 0) selected.Weapon2 = RouletteData.Weapons.Length - 1;
            if (selected.Super < 0) selected.Super = RouletteData.Supers.Length - 1;
            return selected;
        }

        private bool IsChatChoosesSelectionValid(int[] chosen)
        {
            RefreshAvailableContent();
            return chosen != null && chosen.Length == 6 && availableBossIndices.Contains(chosen[0]) &&
                (chosen[1] < 0 || availableWeaponIndices.Contains(chosen[1])) &&
                (chosen[2] < 0 || availableWeaponIndices.Contains(chosen[2])) &&
                (chosen[3] < 0 || availableSuperIndices.Contains(chosen[3])) &&
                availableCharmIndices.Contains(chosen[4]) &&
                (RouletteData.Bosses[chosen[0]].IsPlane || RouletteData.Charms[chosen[4]].Value != Charm.charm_curse) &&
                (chosen[5] < 0 || chosen[5] == RouletteData.Modifiers.Length - 1 ||
                    CreatorToolsValidModifierIndices(RouletteData.Bosses[chosen[0]]).Contains(chosen[5]));
        }

        private void ClearChatChoosesCard()
        {
            chatChoosesCardEntrance.Cancel();
            if (chatChoosesReviewCard)
            {
                if (pendingLoad) CancelCreatorToolsInteractionGameplayLevelLoad();
                pendingLoad = false; resultReady = false;
                if (visible) SetVisible(false);
            }
            uglyMode = chatChoosesOriginalUglyMode;
            chatChoosesReviewCard = false; chatChoosesCardSession = -1;
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
                    Image = ChatChoosesModifierImage(item), None = item.Id == ModifierId.None,
                    Kind = item.Kind == ModifierKind.Plane ? "plane" : item.Kind == ModifierKind.Ground ? "ground" : "both" });
            }
            return catalog;
        }

        private static string ChatChoosesModifierImage(ModifierEntry item)
        {
            string name;
            switch (item.Id)
            {
                case ModifierId.None: return "creator-tools/empty.png";
                case ModifierId.BlackAndWhite: name = "blanco-y-negro"; break;
                case ModifierId.UpsideDown: name = "pantalla-invertida"; break;
                case ModifierId.RgbShift: name = "pantalla-rgb"; break;
                case ModifierId.NoPeashooter: name = "sin-peashooter"; break;
                case ModifierId.NoMiniPlane: name = "sin-miniavion"; break;
                case ModifierId.NoEx: name = "sin-ex"; break;
                case ModifierId.NoDash: name = "sin-dash"; break;
                case ModifierId.NoBombs: name = "sin-bombas"; break;
                case ModifierId.MiniPlaneOnly: name = "solo-miniavion"; break;
                case ModifierId.StiffMode: name = "modo-tieso"; break;
                case ModifierId.InkRain: name = "lluvia-de-tinta"; break;
                case ModifierId.HpOne: name = "una-vida"; break;
                case ModifierId.HalfDamage: name = "mitad-de-dano"; break;
                default: return item.Image;
            }
            return "creator-tools/chat-chooses-modifiers/" + name + ".png";
        }

    }
}
