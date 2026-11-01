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
 * PACK MANAGER
 * ========================================================================
 *
 * PURPOSE:
 * Handles rolling booster pack loot tables, granting the resulting items
 * (spells, reagents, pet snacks, equipment/housing items) to a wizard, and
 * dispatching the pack reward confirmation packet to the client.
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Pet;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Packs;

public static class PackManager {

    private static readonly CoreObjectSerializer s_coreObjectSerializer = new(behaviors: SerializerFlags.None);
    private static readonly ObjectSerializer s_objectSerializer = new(Versionable: false);

    /// <summary>
    /// Opens one or more booster packs atomically, grants the drops to the player, and sends the client confirmations.
    /// Returns <c>true</c> if all drops across all packs were successfully granted; otherwise, <c>false</c>.
    /// </summary>
    public static bool OpenPack(IActorRef sessionActorRef, Wizard wizard, ulong packTemplateId, int count = 1) {
        if (count < 1) {
            return false;
        }

        if (CoreObjectFactory.GetCoreTemplate(packTemplateId) is not BoosterPackTemplate boosterTemplate) {
            Logger.Warning("Template {0} is not a BoosterPackTemplate.", Logger.Args(packTemplateId));
            return false;
        }

        var lootTables = boosterTemplate.m_lootTables;
        if (lootTables is null || lootTables.Count == 0) {
            Logger.Warning("Booster pack {0} has no m_lootTables defined in TemplateManifest.", Logger.Args(packTemplateId));
            return false;
        }

        var rolledPacks = new List<(List<(uint ItemTemplateId, CoreTemplate ItemTemplate, string TableName, string ItemName)> Drops, ByteString SerializedLoot)>(count);
        var inventorySlotsNeeded = 0;
        var reagentCounts = new Dictionary<ulong, int>();
        var snackCounts = new Dictionary<ulong, int>();

        // 1. Roll and serialize all packs in memory first without modifying player state.
        for (var i = 0; i < count; i++) {
            var rolledDrops = new List<(uint ItemTemplateId, CoreTemplate ItemTemplate, string TableName, string ItemName)>();
            foreach (var tableName in lootTables) {
                var rollResult = DropTableRoller.Roll([tableName], sessionActorRef, wizard.GameObject, wizard);
                if (rollResult.Items.Count == 0) {
                    continue;
                }

                foreach (var droppedItem in rollResult.Items) {
                    if (!uint.TryParse(droppedItem.ItemId, out var itemTemplateId)) {
                        continue;
                    }

                    var itemTemplate = CoreObjectFactory.GetCoreTemplate(itemTemplateId);
                    if (itemTemplate is null) {
                        Logger.Warning("Booster pack {0} rolled unknown item template {1}.",
                            Logger.Args(packTemplateId, itemTemplateId));
                        continue;
                    }

                    rolledDrops.Add((itemTemplateId, itemTemplate, tableName, droppedItem.ItemName));
                }
            }

            if (rolledDrops.Count == 0) {
                Logger.Warning("Booster pack {0} rolled no valid drops.", Logger.Args(packTemplateId));
                return false;
            }

            var lootItems = new List<LootInfo>(rolledDrops.Count);
            var lootRarities = new List<LootRarity>(rolledDrops.Count);

            foreach (var (itemTemplateId, itemTemplate, tableName, _) in rolledDrops) {
                if (itemTemplate is ReagentItemTemplate) {
                    reagentCounts[itemTemplateId] = reagentCounts.GetValueOrDefault(itemTemplateId) + 1;
                }
                else if (itemTemplate is PetSnackItemTemplate) {
                    snackCounts[itemTemplateId] = snackCounts.GetValueOrDefault(itemTemplateId) + 1;
                }
                else if (itemTemplate is not SpellTemplate) {
                    inventorySlotsNeeded++;
                }

                LootInfo lootInfo = itemTemplate switch {
                    SpellTemplate => new TreasureCardLootInfo {
                        m_lootType = LOOT_TYPE.LOOT_TYPE_TREASURE_CARD,
                        m_spellID = itemTemplateId,
                        m_numItems = 1
                    },
                    PetSnackItemTemplate => new PetSnackLootInfo {
                        m_lootType = LOOT_TYPE.LOOT_TYPE_PETSNACK,
                        m_itemID = (GID) itemTemplateId,
                        m_numItems = 1
                    },
                    ReagentItemTemplate => new ReagentLootInfo {
                        m_lootType = LOOT_TYPE.LOOT_TYPE_REAGENT,
                        m_itemID = (GID) itemTemplateId,
                        m_numItems = 1
                    },
                    _ => new ItemLootInfo {
                        m_lootType = LOOT_TYPE.LOOT_TYPE_ITEM,
                        m_itemID = (GID) itemTemplateId,
                        m_numItems = 1
                    }
                };
                lootItems.Add(lootInfo);

                lootRarities.Add(new LootRarity {
                    m_rarity = ResolveRarity(itemTemplate, tableName),
                    m_lootGid = (GID) itemTemplateId,
                    m_odds = 0
                });
            }

            var lootInfoList = new LootInfoList {
                m_loot = lootItems,
                m_goldInfo = null,
                m_lootRarityList = new LootRarityList {
                    m_loot = lootRarities
                }
            };

            if (!s_objectSerializer.Serialize(lootInfoList, PropertyFlags.Prop_Public, out var serializedLoot)) {
                Logger.Error("Failed to serialize LootInfoList for booster pack {0}.", Logger.Args(packTemplateId));
                return false;
            }

            rolledPacks.Add((rolledDrops, serializedLoot));
        }

        // 2. Pre-validate aggregated capacity across all rolled packs before modifying player state.
        if (inventorySlotsNeeded > 0 && !wizard.InventoryBehavior.HasInventorySpace(inventorySlotsNeeded)) {
            Logger.Warning("Booster pack {0} grant failed: inventory full (needed {1} slots).",
                Logger.Args(packTemplateId, inventorySlotsNeeded));
            return false;
        }

        foreach (var (reagentId, needed) in reagentCounts) {
            if (!(wizard.AlchemyBehavior?.HasReagentSpace(reagentId, needed) ?? false)) {
                Logger.Warning("Booster pack {0} grant failed: reagent stack limit reached for {1}.",
                    Logger.Args(packTemplateId, reagentId));
                return false;
            }
        }

        foreach (var (snackId, needed) in snackCounts) {
            if (!(wizard.PetSnackBehavior?.HasSnackSpace(snackId, needed) ?? false)) {
                Logger.Warning("Booster pack {0} grant failed: pet snack stack limit reached for {1}.",
                    Logger.Args(packTemplateId, snackId));
                return false;
            }
        }

        // 3. Grant the rolled drops and dispatch confirmations now that capacity is guaranteed.
        foreach (var (rolledDrops, serializedLoot) in rolledPacks) {
            foreach (var (itemTemplateId, itemTemplate, tableName, itemName) in rolledDrops) {
                GrantDroppedItem(sessionActorRef, wizard, itemTemplateId, itemTemplate);

                Logger.Information("Granted {0} ({1}) from slot {2}",
                    Logger.Args(itemName, itemTemplateId, tableName));
            }

            sessionActorRef.Tell(new WIZARD_12_PROTOCOL.MSG_CROWNSBUYCONFIRM {
                Failure = 0,
                WebFailure = 0,
                Credits = wizard.Account.Crowns,
                Data = serializedLoot,
                TemplateID = packTemplateId
            });
        }

        return true;
    }

    private static void GrantDroppedItem(
        IActorRef sessionActorRef,
        Wizard wizard,
        uint itemTemplateId,
        CoreTemplate itemTemplate) {

        switch (itemTemplate) {
            case SpellTemplate spellTemplate:
                GrantTreasureCard(sessionActorRef, wizard, itemTemplateId, spellTemplate);
                break;

            case ReagentItemTemplate:
                GrantReagent(sessionActorRef, wizard, itemTemplateId);
                break;

            case PetSnackItemTemplate:
                GrantPetSnack(sessionActorRef, wizard, itemTemplateId);
                break;

            default:
                GrantInventoryItem(sessionActorRef, wizard, itemTemplateId);
                break;
        }
    }

    private static void GrantTreasureCard(
        IActorRef sessionActorRef,
        Wizard wizard,
        uint spellTemplateId,
        SpellTemplate spellTemplate) {

        wizard.SpellbookBehavior.AddTreasureCard(spellTemplateId);
        WizardCollection.AddTreasureCard(wizard, spellTemplateId);

        if (!string.IsNullOrEmpty(spellTemplate.m_name)) {
            var spellHash = StringHash.Compute(spellTemplate.m_name);
            sessionActorRef.Tell(new WIZARD_12_PROTOCOL.MSG_ADDTREASURESPELLTOBOOK {
                SpellID = (int) spellHash,
                EnchantmentID = 0
            });
        }
    }

    private static void GrantReagent(IActorRef sessionActorRef, Wizard wizard, uint reagentTemplateId) {
        if (!wizard.AddReagent(reagentTemplateId, out var reagentItem)) {
            Logger.Warning("Could not add reagent {0} to reagent bag.", Logger.Args(reagentTemplateId));
            return;
        }

        var flags = PropertyFlags.Prop_Save | PropertyFlags.Prop_Copy | PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        if (!s_coreObjectSerializer.Serialize(reagentItem, flags, out var serializedReagent)) {
            Logger.Warning("Failed to serialize reagent {0} for booster pack grant.", Logger.Args(reagentTemplateId));
            return;
        }

        sessionActorRef.Tell(new WIZARD_12_PROTOCOL.MSG_REAGENTADD {
            GlobalID = wizard.GameObjectID,
            Data = serializedReagent
        });

        sessionActorRef.Tell(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = reagentItem.m_globalID,
            ItemTemplateID = (uint) reagentItem.m_templateID,
            ItemLocation = 1
        });

        sessionActorRef.Tell(new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE {
            GlobalID = wizard.GameObjectID,
            ItemID = reagentItem.m_globalID,
            Quantity = reagentItem.m_quantity
        });
    }

    private static void GrantPetSnack(IActorRef sessionActorRef, Wizard wizard, uint snackTemplateId) {
        if (!wizard.AddSnack(snackTemplateId, out var snackItem)) {
            Logger.Warning("Could not add pet snack {0} to snack bag.", Logger.Args(snackTemplateId));
            return;
        }

        if (!s_coreObjectSerializer.Serialize(snackItem, PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit, out var serializedSnack)) {
            Logger.Warning("Failed to serialize pet snack {0} for booster pack grant.", Logger.Args(snackTemplateId));
            return;
        }

        sessionActorRef.Tell(new PET_9_PROTOCOL.MSG_PETSNACKADD {
            GlobalID = wizard.GameObjectID,
            Data = serializedSnack
        });

        sessionActorRef.Tell(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = snackItem.m_globalID,
            ItemTemplateID = (uint) snackItem.m_templateID,
            ItemLocation = 1
        });

        sessionActorRef.Tell(new PET_9_PROTOCOL.MSG_PETSNACKUPDATE {
            GlobalID = wizard.GameObjectID,
            ItemID = snackItem.m_globalID,
            Quantity = snackItem.m_quantity
        });
    }

    private static void GrantInventoryItem(IActorRef sessionActorRef, Wizard wizard, uint itemTemplateId) {
        WizClientObjectItem itemCoreObject;
        if (PetFactory.IsPetTemplate(itemTemplateId)) {
            if (!wizard.AddHatchedPetToInventory(itemTemplateId, out itemCoreObject)) {
                Logger.Warning("Could not add hatched pet {0} to inventory.", Logger.Args(itemTemplateId));
                return;
            }
        }
        else {
            if (!wizard.AddItemToInventory(itemTemplateId, out itemCoreObject)) {
                Logger.Warning("Could not add item {0} to inventory.", Logger.Args(itemTemplateId));
                return;
            }
        }

        if (!s_coreObjectSerializer.Serialize(itemCoreObject, PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit, out var serializedItem)) {
            Logger.Warning("Failed to serialize inventory item {0} for booster pack grant.", Logger.Args(itemTemplateId));
            return;
        }

        sessionActorRef.Tell(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = wizard.GameObjectID,
            SerializedItem = serializedItem
        });

        sessionActorRef.Tell(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = itemCoreObject.m_globalID,
            ItemTemplateID = (uint) itemCoreObject.m_templateID,
            ItemLocation = 1
        });
    }

    /// <summary>
    /// Resolves the card border rarity:
    /// - Items declare their own rarity via m_rarity.
    /// - Spells inherit rarity from the drop table name.
    /// </summary>
    private static RarityType ResolveRarity(CoreTemplate template, string tableName) {
        if (template is WizItemTemplate wizItem) {
            return wizItem.m_rarity;
        }

        if (tableName.Contains("Epic", StringComparison.OrdinalIgnoreCase)) {
            return RarityType.RT_EPIC;
        }

        if (tableName.Contains("UltraRare", StringComparison.OrdinalIgnoreCase)) {
            return RarityType.RT_ULTRARARE;
        }

        if (tableName.Contains("Rare", StringComparison.OrdinalIgnoreCase)) {
            return RarityType.RT_RARE;
        }

        if (tableName.Contains("Uncommon", StringComparison.OrdinalIgnoreCase)) {
            return RarityType.RT_UNCOMMON;
        }

        return RarityType.RT_COMMON;
    }

}
