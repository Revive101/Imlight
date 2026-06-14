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
 * TREASURE SHOP SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player treasure card shop interactions, including buying 
 * treasure cards from vendor NPCs.
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 */

using System;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Services;

internal class TreasureShopService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TreasureShopService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_TREASUREBUY))]
    private void ReceiveTreasureBuy(WIZARD_12_PROTOCOL.MSG_TREASUREBUY message) {
        // Ensure that the player has interacted with an object in the zone.
        var interactedObject = GetZoneObject(message.npcGlobalID);
        if (interactedObject is null) {
            Logger.Warning("Failed to find NPC {0} in zone for treasure card purchase",
                Logger.Args(message.npcGlobalID));
            var denyMsg = new WIZARD_12_PROTOCOL.MSG_TREASUREBUYCONFIRM { Failure = 1 };
            SendToSocket(denyMsg);

            return;
        }

        // Ensure that the interacted object is a treasure card vendor.
        var vendorComponent = interactedObject.GetComponentOfType<InteractTreasureVendorComponent>();
        if (vendorComponent is null) {
            Logger.Warning("Failed to find TreasureVendorComponent for NPC {0}",
                Logger.Args(message.npcGlobalID));
            var denyMsg = new WIZARD_12_PROTOCOL.MSG_TREASUREBUYCONFIRM { Failure = 1 };
            SendToSocket(denyMsg);

            return;
        }

        // Ensure that the vendor has the spell the player is trying to purchase.
        if (!vendorComponent.HasSpell(message.TreasureCardID)) {
            Logger.Warning("Player tried to purchase treasure card {0} from NPC {1} that is not in its inventory.",
                Logger.Args(message.TreasureCardID, message.npcGlobalID));

            var denyMsg = new WIZARD_12_PROTOCOL.MSG_TREASUREBUYCONFIRM { Failure = 1 };
            SendToSocket(denyMsg);

            return;
        }

        var playerWizard = GetActiveWizard();
        var goldCost = vendorComponent.GetSpellPrice(message.TreasureCardID);

        // Handle quantity — multiply cost by quantity.
        var quantity = Math.Max(1, message.Quantity);
        var totalCost = goldCost * quantity;

        // Check if the user can afford the purchase.
        if (playerWizard.GameStats.m_currentGold < totalCost) {
            Logger.Warning("Player could not afford treasure card {0} (cost: {1}, gold: {2})",
                Logger.Args(message.TreasureCardID, totalCost, playerWizard.GameStats.m_currentGold));

            var denyMsg = new WIZARD_12_PROTOCOL.MSG_TREASUREBUYCONFIRM { Failure = 1 };
            SendToSocket(denyMsg);

            return;
        }

        // Deduct gold.
        playerWizard.RemoveGold(totalCost);

        // Inform the game client that the player's gold has been updated.
        var goldUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = playerWizard.GameStats.m_currentGold,
            MaxGold = playerWizard.GameStats.m_baseGoldPouch
        };
        SendToSocket(goldUpdateMsg);

        // Resolve the spell template ID from the spell hash for persistence.
        var spellName = vendorComponent.GetSpellName(message.TreasureCardID);
        var spell = SpellFactory.GetSpell(spellName);
        if (spell == null) {
            Logger.Warning("Failed to resolve spell from hash {0} (name: {1})",
                Logger.Args(message.TreasureCardID, spellName));

            var denyMsg = new WIZARD_12_PROTOCOL.MSG_TREASUREBUYCONFIRM { Failure = 1 };
            SendToSocket(denyMsg);

            return;
        }

        var spellTemplateId = spell.m_templateID;

        // Add the treasure card to the player's treasure book for each quantity.
        for (var i = 0; i < quantity; i++) {
            var addSpellMsg = new WIZARD_12_PROTOCOL.MSG_ADDTREASURESPELLTOBOOK {
                SpellID = (int) message.TreasureCardID,
                EnchantmentID = 0,
            };
            SendToSocket(addSpellMsg);

            // Persist the treasure card in the wizard's database record.
            playerWizard.SpellbookBehavior.AddTreasureCard(spellTemplateId);
            WizardCollection.AddTreasureCard(playerWizard, spellTemplateId);
        }

        // Confirm the purchase to the client.
        var confirmMsg = new WIZARD_12_PROTOCOL.MSG_TREASUREBUYCONFIRM {
            Failure = 0,
            WebFailure = 0,
            Credits = 0,
        };
        SendToSocket(confirmMsg);
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();
        var treasureCards = wizard.SpellbookBehavior.TreasureCardTemplateIds;

        if (treasureCards is null || treasureCards.Count == 0) {
            return;
        }

        foreach (var templateId in treasureCards) {
            var template = CoreObjectFactory.GetCoreTemplate(templateId);
            if (template is not SpellTemplate spellTemplate) {
                continue;
            }

            var spellHash = StringHash.Compute(spellTemplate.m_name);
            var addSpellMsg = new WIZARD_12_PROTOCOL.MSG_ADDTREASURESPELLTOBOOK {
                SpellID = (int) spellHash,
                EnchantmentID = 0,
            };
            SendToSocket(addSpellMsg);
        }
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_DONESHOPPING))]
    private void ReceiveDoneShopping() {
        // A wizard has completed shopping and is leaving the shop.
        var wizard = GetActiveWizard();

        if (wizard.Zone.Contains("Phantom")) {
            return;
        }

        // Reenable player movement
        var enableMovementStateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
            GameObjectID = wizard.CharId,
            State = 1685237158,
            Data = "",
            IgnoreIfCurrentStateIsOff = 0
        };
        SendToSocket(enableMovementStateMsg);

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = wizard.CharId,
            WizBangID = (uint) WizBangs.None
        };
        ZoneBroadcast(wizBangMsg, false);
    }

}
