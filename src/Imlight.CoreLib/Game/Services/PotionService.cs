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
 * POTION SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * This service handles the potion usage mechanics in the game.
 * 
 * USAGE EXAMPLE:
 * Internal service handling various cantrip-related messages and actions 
 * within the game server's session management system.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: valiantmeraki
 * Version: KALI 1.0
 * Last Updated: 05/04/2025
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.CoreLib.Game.Services;

internal class PotionService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new PotionService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_USEPOTION))]
    private void UsePotion(WIZARD_12_PROTOCOL.MSG_USEPOTION message) {
        // Inform the player's game client they're using a potion.
        var usePotionMsg = new WIZARD_12_PROTOCOL.MSG_USEPOTION() { };
        SendToSocket(usePotionMsg);

        var wizard = GetActiveWizard();
        var potionCharge = wizard.GameStats.m_potionCharge;
        var potionMax = wizard.GameStats.m_potionMax;

        if (potionCharge < 1.0) { // If the player has no potion charge, they cannot heal.
            return;
        }

        // Values with gear and effects.
        var baseHealth = wizard.GameStats.m_baseHitpoints;
        var currentHealth = wizard.GameStats.m_currentHitpoints;

        var baseMana = wizard.GameStats.m_baseMana;
        var currentMana = wizard.GameStats.m_currentMana;

        if (currentHealth >= baseHealth && currentMana >= baseMana) { // If health and mana are full, no need to heal.
            return;
        }

        // Values before effects are applied.
        var clientGameStats = wizard.GameStats.GetClientTypeAlternative();
        var msgBaseHealth = clientGameStats.m_baseHitpoints;
        var msgBaseMana = clientGameStats.m_baseMana;

        if (currentHealth < baseHealth) { // If health is full, no need to heal.
            // Inform the player's game client that their health has been updated.
            var healthUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH() {
                CharacterID = wizard.CharId,
                NewHealth = baseHealth,
                NewHealthMax = msgBaseHealth,
                DisplayDiff = 1
            };
            SendToSocket(healthUpdateMsg);
            wizard.UpdateHealth(baseHealth);
        }

        if (currentMana < baseMana) { // If mana is full, no need to heal.
            // Inform the player's game client that their mana has been updated.
            var manaUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEMANA() {
                Mana = baseMana,
                MaxMana = msgBaseMana,
                DisplayDiff = 1
            };
            SendToSocket(manaUpdateMsg);
            wizard.UpdateMana(baseMana);
        }

        var newPotionCharge = potionCharge - 1.0f;

        // Inform the player's game client that their potion charge has been updated.
        var potionChargeUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEPOTIONS {
            PotionMax = potionMax,
            PotionCharge = newPotionCharge
        };
        SendToSocket(potionChargeUpdateMsg);
        wizard.UpdatePotions(newPotionCharge, potionMax);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_POTIONBUYREQUEST))]
    private void ReceivePotionBuyRequest(WIZARD_12_PROTOCOL.MSG_POTIONBUYREQUEST message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var stats = wizard.GameStats;
        var charge = stats.m_potionCharge;
        var max = stats.m_potionMax;

        // Whole potions still missing. Nothing to fill, so confirm success as a benign no-op.
        var missing = (int) Math.Ceiling(max - charge);
        if (missing <= 0) {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_POTIONBUYCONFIRM { Failure = 0 });

            return;
        }

        // todo: in-game verify the AmountEnum mapping (assumed 0 = Fill One, else Fill All).
        var fillAll = message.AmountEnum != 0;
        var potionsToFill = fillAll ? missing : 1;

        var level = Math.Max(1, stats.Level);
        var perPotion = PotionCostPerBottle(level);
        var cost = perPotion * potionsToFill;

        // Can't afford. AddGold only clamps at the pouch max, so a negative delta would dip below zero.
        if (stats.m_currentGold < cost) {
            Logger.Information("Wizard {0} can't afford {1} potion(s): {2} gold needed, {3} held (level {4}).",
                Logger.Args(wizard.CharId, potionsToFill, cost, stats.m_currentGold, level));
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_POTIONBUYCONFIRM { Failure = 1 });

            return;
        }

        // Charge gold and echo it.
        wizard.AddGold(-cost);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = stats.m_currentGold,
            MaxGold = stats.m_baseGoldPouch,
        });

        // Refill the potion charge (capped at max) and echo it.
        var newCharge = Math.Min(max, charge + potionsToFill);
        wizard.UpdatePotions(newCharge, max);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEPOTIONS {
            PotionMax = max,
            PotionCharge = newCharge,
        });

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_POTIONBUYCONFIRM { Failure = 0 });
        Logger.Information("Wizard {0} filled {1} potion(s) for {2} gold (level {3}, {4} each); charge {5} to {6} of {7}.",
            Logger.Args(wizard.CharId, potionsToFill, cost, level, perPotion, charge, newCharge, max));
    }

    private static int PotionCostPerBottle(int level)
        => level < 11 ? 100
         : level < 21 ? level * 10
         : level < 31 ? level * 15
         : level < 41 ? level * 20
         : level * 30;

}
