/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * DROP TABLES
 * ========================================================================
 * 
 * PURPOSE:
 * Grants a rolled DropTableResult to a player (gold, XP, training points,
 * items, potion slot) and shows the loot popup. Shared by the quest reward
 * path (ResDropTableHandler) and the mob-defeat path (CombatService).
 * 
 * USAGE EXAMPLE:
 * Both reward paths roll their drop tables and hand the result to
 * LootGranter.GrantAndDisplay.
 * 
 * NOTE:
 * All sends go to the player's SessionActor, which routes them to the right
 * service or socket. One implementation means both paths share the same
 * proven grant behavior.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 08/22/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.DropTables;

/// <summary>
/// Grants a rolled drop table result to a player and shows the loot popup.
/// </summary>
public static class LootGranter {

    private const uint LOOT_LIST_SERIALIZATION_FLAGS = 4;
    private const uint INVENTORY_ADD_SERIALIZATION_FLAGS =
        (uint) (PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit);
    private static readonly ConcurrentDictionary<ulong, byte> s_warnedTemplateIds = new();
    private static readonly CoreObjectSerializer s_itemSerializer = new(behaviors: SerializerFlags.None);

    /// <summary>
    /// Grants every reward in <paramref name="results"/> to the player and shows the loot popup.
    /// </summary>
    /// <param name="playerActor">The player's SessionActor, which routes the messages.</param>
    /// <param name="wizard">The player wizard data receiving the rewards.</param>
    /// <param name="results">The rolled drop table results to grant.</param>
    public static void GrantAndDisplay(IActorRef playerActor, Wizard wizard, DropTableResult results) {
        UpdateWizardGold(playerActor, wizard, results.GoldAmount);
        UpdateWizardXP(playerActor, results.ExperienceAmount);
        UpdateWizardTP(playerActor, wizard, results.TrainingPoints);

        // Only what was actually granted is shown in the loot popup.
        UpdateCharacterItems(playerActor, wizard, results);
        SendLootInfoToClient(playerActor, results, wizard);

        if (results.GrantsPotionSlot) {
            UpdateWizardPotionMax(playerActor, wizard);
        }
    }

    private static void UpdateWizardGold(IActorRef playerActor, Wizard wizard, int goldDelta) {
        if (goldDelta == 0) {
            return;
        }

        // Add gold to the wizard. This will save their data, but not inform their game client.
        wizard.AddGold(goldDelta);

        // Now, inform the game client their gold has been updated.
        // This only changes the character page. It does not show a popup or anything.
        // The popup comes from the network LootInfoList.
        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD() {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch
        };
        playerActor.Tell(networkMessage);
    }

    private static void UpdateWizardXP(IActorRef playerActor, int xpDelta) {
        if (xpDelta == 0) {
            return;
        }

        // XP is simple and we only need to inform the SessionActor.
        var internalMsg = new CHARACTER_103_PROTOCOL.MSG_GAINXP {
            XP = xpDelta
        };
        playerActor.Tell(internalMsg);

        // That's it. The SessionActor will inform the game client, and level them up if needed.
    }

    private static void UpdateWizardTP(IActorRef playerActor, Wizard wizard, int tpDelta) {
        if (tpDelta == 0) {
            return;
        }

        // Set the new amount of training points for the wizard.
        var oldTP = wizard.MagicSchoolBehavior.TrainingPoints;
        var newTP = Math.Max(0, oldTP + tpDelta);
        if (newTP == oldTP) {
            return;
        }

        wizard.UpdateTrainingPoints(newTP);

        // Inform the game client of the new TP amount.
        var msg = new WIZARD_12_PROTOCOL.MSG_UPDATETRAINING() {
            TrainingPoints = (ushort) newTP
        };
        playerActor.Tell(msg);
    }

    private static void UpdateCharacterItems(IActorRef playerActor, Wizard wizard, DropTableResult results) {
        if (results.Items.Count == 0) {
            return;
        }

        // Replaced below by what was actually granted, so the loot popup lists only that.
        var rolledItems = results.Items;
        var grantedItems = new List<DropItemResult>();
        results.Items = grantedItems;

        foreach (var item in rolledItems) {
            if (!ulong.TryParse(item.ItemId, out var itemGuid)) {
                continue;
            }

            // A drop table entry may name an item, a spell or something else; resolve it first.
            switch (CoreObjectFactory.GetCoreTemplate(itemGuid)) {
                case ItemTemplate:
                    if (GrantItem(playerActor, wizard, item, itemGuid)) {
                        grantedItems.Add(item);
                    }

                    break;
                case SpellTemplate spellTemplate:
                    GrantSpell(playerActor, wizard, results, spellTemplate, (uint) itemGuid, item.Quantity);

                    break;
                default:
                    WarnSkippedOnce(itemGuid, "is neither an item nor a spell");

                    break;
            }
        }
    }

    private static bool GrantItem(IActorRef playerActor, Wizard wizard, DropItemResult item, ulong templateId) {
        if (!wizard.AddItemToInventory(templateId, out var addedItem)) {
            Logger.Error("Failed to add item {0} to wizard {1}'s inventory.",
                Logger.Args(item.ItemId, wizard.CharId));

            return false;
        }

        // The attach payload (which carries the inventory) was already sent, so push each
        // item to the client explicitly or the reward stays invisible this session.
        SendInventoryAdd(playerActor, wizard, addedItem);

        return true;
    }

    private static void GrantSpell(IActorRef playerActor, Wizard wizard, DropTableResult results,
        SpellTemplate spellTemplate, uint templateId, int quantity) {
        if (spellTemplate.m_Treasure) {
            // Treasure cards (booster pack contents) go to the treasure card book, never the spellbook.
            GrantTreasureCards(playerActor, wizard, spellTemplate, templateId, quantity);

            return;
        }

        if (SpellTeacher.TryTeach(playerActor, wizard, templateId) == SpellTeachResult.Learned) {
            results.SpellIds.Add(templateId);
        }
    }

    private static void GrantTreasureCards(IActorRef playerActor, Wizard wizard,
        SpellTemplate spellTemplate, uint templateId, int quantity) {
        var spellHash = StringHash.Compute(spellTemplate.m_name);

        // Same path as buying a treasure card: the wizard's treasure book, persisted, then the client.
        for (var i = 0; i < Math.Max(1, quantity); i++) {
            wizard.SpellbookBehavior.AddTreasureCard(templateId);
            WizardCollection.AddTreasureCard(wizard, templateId);

            playerActor.Tell(new WIZARD_12_PROTOCOL.MSG_ADDTREASURESPELLTOBOOK {
                SpellID = (int) spellHash,
                EnchantmentID = 0,
            });
        }
    }

    private static void WarnSkippedOnce(ulong templateId, string reason) {
        if (!s_warnedTemplateIds.TryAdd(templateId, 0)) {
            return;
        }

        Logger.Warning("Skipping drop table entry {0}: template {1}.", Logger.Args(templateId, reason));
    }

    private static void SendInventoryAdd(IActorRef playerActor, Wizard wizard, WizClientObjectItem item) {
        if (!s_itemSerializer.Serialize(item, INVENTORY_ADD_SERIALIZATION_FLAGS, out var serializedItem)) {
            Logger.Error("Failed to serialize reward item {0} for inventory-add.",
                Logger.Args(item.m_globalID.Full));

            return;
        }

        playerActor.Tell(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = wizard.GameObjectID,
            SerializedItem = serializedItem,
        });
    }

    private static void UpdateWizardPotionMax(IActorRef playerActor, Wizard wizard) {
        var currentWizardMaxPots = wizard.GameStats.m_potionMax;
        var newWizardMaxPots = currentWizardMaxPots + 1;

        wizard.UpdatePotions(newWizardMaxPots, newWizardMaxPots);

        // Inform the player's game client that their potion charge has been updated.
        var potionChargeUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEPOTIONS {
            PotionMax = newWizardMaxPots,
            PotionCharge = newWizardMaxPots
        };
        playerActor.Tell(potionChargeUpdateMsg);
    }

    private static void SendLootInfoToClient(IActorRef playerActor, DropTableResult results, Wizard wizard) {
        // Inform the game client of the loot results.
        if (!DropTableConverter.HasRewards(results)) {
            return;
        }

        // Convert the loot results into something we can send over the network.
        var networkLootList = DropTableConverter.ToLootInfoList(results);
        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(networkLootList, LOOT_LIST_SERIALIZATION_FLAGS, out var serializedLootList)) {
            Logger.Error("Failed to serialize loot list for network transmission.");

            return;
        }

        var lootMsg = new WIZARD_12_PROTOCOL.MSG_LOOT() {
            GlobalID = wizard.GameObjectID,
            LootList = serializedLootList
        };

        playerActor.Tell(lootMsg);
    }

}
