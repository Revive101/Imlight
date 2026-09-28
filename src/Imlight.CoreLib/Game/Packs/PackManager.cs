using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Imlight.CoreLib.Game.Packs;

public static class PackManager {
    private static readonly CoreObjectSerializer s_coSerializer = new(behaviors: SerializerFlags.None);
    private static readonly ObjectSerializer s_objSerializer = new(Versionable: false);

    public static bool IsBoosterPack(ulong templateId) {
        var template = CoreObjectFactory.GetCoreTemplate(templateId);
        return template is BoosterPackTemplate;
    }

    /// <summary>
    /// Opens a booster pack, grants the drops to the player, and sends the client confirmation.
    /// </summary>
    public static void OpenPack(IActorRef sessionActorRef, Wizard wizard, ulong packTemplateId) {
        if (CoreObjectFactory.GetCoreTemplate(packTemplateId) is not BoosterPackTemplate boosterTemplate) {
            Logger.Warning("Template {0} is not a BoosterPackTemplate.", Logger.Args(packTemplateId));
            return;
        }

        var lootTables = boosterTemplate.m_lootTables;
        if (lootTables == null || lootTables.Count == 0) {
            Logger.Warning("Booster pack {0} has no m_lootTables defined in TemplateManifest.", Logger.Args(packTemplateId));
            return;
        }

        var lootItems = new List<LootInfo>();
        var lootRarities = new List<LootRarity>();

        foreach (var tableName in lootTables) {
            var dropTable = DropTableCollection.GetDropTable(tableName);
            if (dropTable == null) {
                Logger.Warning("DropTable '{0}' not found in SpiralDB/DropTables!", Logger.Args(tableName));
                continue;
            }

            var rollResult = DropTableRoller.Roll([tableName], sessionActorRef, wizard.GameObject, wizard);
            if (rollResult.Items.Count == 0) {
                continue;
            }

            foreach (var droppedItem in rollResult.Items) {
                if (!ulong.TryParse(droppedItem.ItemId, out var itemTemplateId)) {
                    continue;
                }

                var itemTemplate = CoreObjectFactory.GetCoreTemplate(itemTemplateId);
                var rarity = ResolveRarity(itemTemplate, tableName);

                if (itemTemplate is SpellTemplate) {
                    lootItems.Add(new TreasureCardLootInfo {
                        m_lootType = LOOT_TYPE.LOOT_TYPE_TREASURE_CARD,
                        m_spellID = (uint) itemTemplateId,
                        m_numItems = 1
                    });
                    lootRarities.Add(new LootRarity {
                        m_rarity = rarity,
                        m_lootGid = (GID) itemTemplateId,
                        m_odds = 0
                    });

                    wizard.SpellbookBehavior.AddTreasureCard((uint) itemTemplateId);
                    WizardCollection.AddTreasureCard(wizard, (uint) itemTemplateId);

                    Logger.Information("Granted TC {0} ({1}) from slot {2}", Logger.Args(droppedItem.ItemName, droppedItem.ItemId, tableName));
                }
                else {
                    lootItems.Add(new ItemLootInfo {
                        m_lootType = LOOT_TYPE.LOOT_TYPE_ITEM,
                        m_itemID = (GID) itemTemplateId,
                        m_numItems = 1
                    });

                    lootRarities.Add(new LootRarity {
                        m_rarity = rarity,
                        m_lootGid = (GID) itemTemplateId,
                        m_odds = 0
                    });

                    if (wizard.AddItemToInventory(itemTemplateId, out WizClientObjectItem itemCoreObject)) {
                        if (s_coSerializer.Serialize(itemCoreObject, 24, out var serializedItem)) {
                            sessionActorRef.Tell(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
                                GlobalID = wizard.GameObjectID,
                                SerializedItem = serializedItem
                            });
                        }
                    }
                    else {
                        Logger.Warning("Could not add item {0} to inventory.", Logger.Args(itemTemplateId));
                    }
                }
            }
        }

        var lootInfoList = new LootInfoList() {
            m_loot = lootItems,
            m_goldInfo = null,
            m_lootRarityList = new LootRarityList() {
                m_loot = lootRarities
            }
        };

        if (s_objSerializer.Serialize(lootInfoList, 4, out var serializedLoot)) {
            sessionActorRef.Tell(new WIZARD_12_PROTOCOL.MSG_CROWNSBUYCONFIRM {
                Failure = 0,
                WebFailure = 0,
                Credits = wizard.Account.Crowns,
                Data = serializedLoot,
                TemplateID = packTemplateId
            });
        }
        else {
            Logger.Error("Failed to serialize LootInfoList for booster pack.");
        }
    }

    /// <summary>
    /// Resolves the card border rarity:
    /// - Items declare their own rarity via m_rarity.
    /// - Spells inherit rarity from the drop table name
    /// </summary>
    private static RarityType ResolveRarity(CoreTemplate? template, string tableName) {
        if (template is WizItemTemplate wizItem) {
            return wizItem.m_rarity;
        }

        if (tableName.Contains("Epic", StringComparison.OrdinalIgnoreCase)) return RarityType.RT_EPIC;
        if (tableName.Contains("UltraRare", StringComparison.OrdinalIgnoreCase)) return RarityType.RT_ULTRARARE;
        if (tableName.Contains("Rare", StringComparison.OrdinalIgnoreCase)) return RarityType.RT_RARE;
        if (tableName.Contains("Uncommon", StringComparison.OrdinalIgnoreCase)) return RarityType.RT_UNCOMMON;

        return RarityType.RT_COMMON;
    }
}
