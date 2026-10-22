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
 * INVENTORY SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player inventory interactions, including item management, 
 * quick selling, and special item handling.
 * 
 * USAGE EXAMPLE:
 * Internal service handling various inventory-related messages within 
 * the game server session.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty, Joji
 * Version: KALI 1.0
 * Last Updated: 09/27/2026
 */

using System;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class InventoryService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InventoryService(parentActor));

    #region Destroy/Feed Inventoryitem

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM))]
    private void ReceiveTrashInventoryItem(GAME_5_PROTOCOL.MSG_TRASHINVENTORYITEM message) {
        var wizard = GetActiveWizard();

        // Check if this is an equipped item (e.g. an active elixir).
        var slot = wizard.EquipmentBehavior.SlotList?.FirstOrDefault(s => s.ItemId.Value == message.GlobalID);
        if (slot is not null || wizard.EquipmentBehavior.HasItemEquipped(message.GlobalID)) {
            var item = wizard.EquipmentBehavior.GetItem(message.GlobalID);
            var template = ItemHelper.GetItemTemplate(item);

            if (template is null) {
                var dbItem = WizardItemCollection.GetItem(wizard.CharId, message.GlobalID);
                if (dbItem is not null) {
                    template = ItemHelper.GetItemTemplate(dbItem);
                }
            }

            if (slot is null) {
                Logger.Error("Tried to unequip item with global id {0} that does not exist in player equipment.",
                    Logger.Args(message.GlobalID)); 
                return;
            }

            if (template is null) {
                Logger.Error("Tried to unequip item with global id {0} but could not find its template.",
                    Logger.Args(message.GlobalID));
                return;
            }

            var slotName = slot.SlotType.ToString();
            var removedEffects = CharacterEffectHelper.RemoveEffectsFromWizard(wizard, template);
            wizard.EquipmentBehavior.UnequipItem(message.GlobalID);
            WizardItemCollection.RemoveItem(wizard.CharId, message.GlobalID);
            WizardCollection.UpdateCharacterItems(wizard);

            // For elixirs, send MSG_EQUIPMENTBEHAVIOR_UNEQUIPITEM so the client's ElixirGUIManager
            // receives the "EQUIPMENT_UnequipItem" event and destroys the HUD timer icon.
            if (slot.SlotType == EquipmentSlotType.Elixir) {
                SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_UNEQUIPITEM {
                    GlobalID = wizard.GameObjectID,
                    ItemID = message.GlobalID
                });
            }
            else {
                SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM {
                    ItemID = message.GlobalID,
                    SlotName = slotName,
                    IsEquip = 0
                });
            }

            SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM {
                GlobalID = wizard.GameObjectID,
                ItemID = message.GlobalID
            });

            if (removedEffects is not null) {
                foreach (var effect in removedEffects) {
                    SendToSocket(new GAME_5_PROTOCOL.MSG_REMOVEEFFECT {
                        GameObjectID = wizard.GameObjectID,
                        EffectNameID = effect.m_effectNameID,
                        InternalID = effect.m_internalID
                    });
                }
            }

            if (template.m_behaviors?.OfType<ElixirBehaviorTemplate>().FirstOrDefault() is { } elixirTemplate) {
                PlaySound(elixirTemplate.m_unequipSound);
            }

            // Sync health and mana display, clamping current values to the new maximums.
            var clientStats = wizard.GameStats.GetClientTypeAlternative();
            if (wizard.GameStats.m_currentHitpoints > clientStats.m_baseHitpoints) {
                wizard.GameStats.m_currentHitpoints = clientStats.m_baseHitpoints;
            }

            if (wizard.GameStats.m_currentMana > clientStats.m_baseMana) {
                wizard.GameStats.m_currentMana = clientStats.m_baseMana;
            }

            SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH {
                CharacterID = wizard.GameObjectID,
                NewHealth = wizard.GameStats.m_currentHitpoints,
                NewHealthMax = clientStats.m_baseHitpoints,
                DisplayDiff = 1
            });

            SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
                Mana = wizard.GameStats.m_currentMana,
                MaxMana = clientStats.m_baseMana,
                DisplayDiff = 1
            });

            return;
        }

        wizard.RemoveItemFromInventory(message.GlobalID);
        SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM() {
            GlobalID = wizard.GameObjectID,
            ItemID = message.GlobalID
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_FEEDINVENTORYITEM))]
    private void ReceiveFeedInventoryItem(GAME_5_PROTOCOL.MSG_FEEDINVENTORYITEM message) {
        SendToSocket(new GAME_5_PROTOCOL.MSG_FEEDINVENTORYITEM() {
            FedObjectID = message.FedObjectID,
            PetID = message.PetID,
        });
    }

    #endregion

    #region Quicksell from Inventory

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_REQUESTQUICKSELL))]
    private void ReceiveRequestQuickSell(WIZARD_12_PROTOCOL.MSG_REQUESTQUICKSELL message) {
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REQUESTQUICKSELL() {
            FromTemplateID = message.FromTemplateID,
            Section = message.Section,
            SellModifier = message.SellModifier + 0.05f, // (?) Live server uses ~0.05f.
        });
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_QUICKSELLREQUEST))]
    private void ReceiveQuickSellRequest(WIZARD2_53_PROTOCOL.MSG_QUICKSELLREQUEST message) {
        // Non-versionable: the client compact-encodes this Data blob, same as every other
        // hand-defined message's embedded ObjectProperty payload (CombatDuelComponent, etc.).
        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );

        var wizard = GetActiveWizard();
        int goldSum = 0;

        if (!serializer.Deserialize<QuickSellItemList>(message.Data, 4, out var quickSellItemList)
            || quickSellItemList.m_quickSellItemList is null) {
            Logger.Log.Error("Failed to deserialize quicksell item list.");

            return;
        }

        // Remove items from inventory and equipment, tally up gold sum.
        foreach (QuickSellItem quickSellItem in quickSellItemList.m_quickSellItemList) {
            var item = wizard.InventoryBehavior.GetItem(quickSellItem.m_sellItemGID);
            if (item is not null) {
                var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(item.m_templateID);

                wizard.RemoveItemFromInventory(item.m_globalID);

                // Some items (snack, reagents) are stackable.
                for (int i = 0; i < quickSellItem.m_quantity; i++) {
                    SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM() {
                        GlobalID = wizard.GameObjectID,
                        ItemID = quickSellItem.m_sellItemGID
                    });

                    var value = (int) Math.Ceiling(template.m_baseCost * 0.05f);
                    if (template.m_numPrimaryColors != 1 && template.m_numSecondaryColors != 0) {
                        value = (int) Math.Ceiling(value * 1.2275f); // Dyed items are more expensive.
                    }

                    goldSum += value;
                }

                continue;
            }

            // Treasure cards aren't in InventoryBehavior: the spellbook tracks them by
            // spell hash (see SpellBookService's treasure handlers), not a GID.
            var sellItemHash = (uint) (ulong) quickSellItem.m_sellItemGID;
            var treasureCardTemplateId = SpellFactory.GetTemplateIdByHash(sellItemHash);
            if (treasureCardTemplateId != 0
                && CoreObjectFactory.GetCoreTemplate(treasureCardTemplateId) is SpellTemplate spellTemplate) {
                for (int i = 0; i < quickSellItem.m_quantity; i++) {
                    if (!wizard.SpellbookBehavior.RemoveTreasureCard(treasureCardTemplateId)) {
                        break;
                    }

                    WizardCollection.RemoveTreasureCard(wizard, treasureCardTemplateId);

                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_REMOVETREASURESPELLFROMBOOK() {
                        SpellID = (int) sellItemHash,
                        EnchantmentID = 0
                    });

                    goldSum += (int) Math.Ceiling(spellTemplate.m_baseCost * 0.05f);
                }

                continue;
            }

            Logger.Log.Warning("Could not resolve quicksell item {0}; nothing was sold for it.",
                Logger.Args(quickSellItem.m_sellItemGID));
        }

        // Update player with their new gold balance.
        wizard.AddGold(goldSum);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD() {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch,
        });

        // End quicksell process with empty message.
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_QUICKSELLREQUEST());
    }

    #endregion

    #region Jewels

    // JEWELS
    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELREQUEST))]
    private void ReceiveEquipJewelRequest(WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELREQUEST message) {
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELREQUEST() {
            ItemGID = message.ItemGID,
            JewelGID = message.JewelGID,
            SocketNumber = message.SocketNumber,
        });

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_EQUIPJEWELTOITEM() {
            ItemGID = message.ItemGID,
            JewelGID = message.JewelGID,
            SocketNumber = message.SocketNumber,
            GlobalID = RandomGen.GenerateGUID()
        });
    }

    #endregion

    #region Snacks

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETSNACKREMOVEREQUEST))]
    private void ReceivePetSnackRemoveRequest(PET_9_PROTOCOL.MSG_PETSNACKREMOVEREQUEST message) {
        var wizard = GetActiveWizard();

        var hasSnack = wizard.PetSnackBehavior.HasSnackID(message.GlobalID);
        if (!hasSnack) {
            Logger.Log.Debug("Tried to remove snack with global id {0} that does not exist in player snack bag.",
                Logger.Args(message.GlobalID));

            return;
        }

        wizard.RemoveSnack(message.GlobalID, out var updatedSnack);

        if (updatedSnack.m_quantity > 0) {
            SendToSocket(new PET_9_PROTOCOL.MSG_PETSNACKUPDATE() {
                GlobalID = wizard.GameObjectID,
                ItemID = updatedSnack.m_globalID,
                Quantity = updatedSnack.m_quantity
            });

            return;
        }

        SendToSocket(new PET_9_PROTOCOL.MSG_PETSNACKREMOVE() {
            GlobalID = wizard.GameObjectID,
            ItemID = updatedSnack.m_globalID,
        });
    }

    #endregion

    #region Reagents

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_REAGENTREMOVEREQUEST))]
    private void ReceiveReagentRemoveRequest(WIZARD_12_PROTOCOL.MSG_REAGENTREMOVEREQUEST message) {
        var wizard = GetActiveWizard();

        var hasReagent = wizard.AlchemyBehavior.HasReageant(message.GlobalID);
        if (!hasReagent) {
            Logger.Log.Debug("Tried to remove reagent with global id {0} that does not exist in player reagent bag.",
                Logger.Args(message.GlobalID));

            return;
        }

        var reagent = wizard.AlchemyBehavior.GetReagent(message.GlobalID);
        wizard.RemoveReagent(reagent.m_globalID, out var updatedReagent);

        if (updatedReagent.m_quantity > 0) {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE() {
                GlobalID = wizard.GameObjectID,
                ItemID = updatedReagent.m_globalID,
                Quantity = updatedReagent.m_quantity
            });

            return;
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTREMOVE() {
            GlobalID = wizard.GameObjectID,
            ItemID = updatedReagent.m_globalID,
        });
    }

    #endregion

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PLAYERWIZBANG))]
    private void ReceivePlayerWizbang(WIZARD_12_PROTOCOL.MSG_PLAYERWIZBANG message) {
        var wizard = GetActiveWizard();

        switch (message.StateName) {
            case "SpellbookWizbang":
                ZoneBroadcast(new GAME_5_PROTOCOL.MSG_WIZBANG() {
                    GameObjectID = wizard.GameObjectID,
                    WizBangID = StringHash.Compute("Registrar")
                }, false);
                break;
            default:
                ZoneBroadcast(new GAME_5_PROTOCOL.MSG_WIZBANG() {
                    GameObjectID = wizard.GameObjectID,
                    WizBangID = 0
                }, false);
                break;
        }
    }

}
