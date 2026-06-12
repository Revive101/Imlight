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
    
}
