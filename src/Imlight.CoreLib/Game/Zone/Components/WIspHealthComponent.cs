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
 * HEALTH WISP
 * ========================================================================
 * 
 * PURPOSE:
 * Manages health wisp interactions, providing healing mechanics 
 * for players within a specific interaction radius.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Supports different healing percentages for starter and non-starter wisps.
 * 
 * TODO:
 * - Improve state change information sourcing
 * 
 * Created by: Joji
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class WispHealthComponent : ZoneEntityComponent, IComponentFactory {

    private const float INTERACTION_RADIUS = 100.0f;
    private const float STARTING_WORLD_WISP_HEALTH = 0.40f;
    private const float WISP_HEALTH_PERCENT_INCREASE = 0.25f;

    private readonly bool _isStarterWisp;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_objectName.ToString().Contains("Wisp")
        && goTemplate.m_objectName.ToString().Contains("Health");

    // ctor
    public WispHealthComponent(ZoneEntity entity) : base(entity) {
        if (entity.Template is GameObjectTemplate goTemplate) {
            var objectName = goTemplate.m_objectName.ToString();
            _isStarterWisp = objectName.Contains("WC");
        }
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (IsInRadius(playerObj, INTERACTION_RADIUS)) {
            // Values with gear and effects.
            var baseHealth = playerWizard.GameStats.m_baseHitpoints;
            var currentHealth = playerWizard.GameStats.m_currentHitpoints;

            if (baseHealth == currentHealth) { // If health is full, no need to heal.
                return;
            }

            // Values before effects are applied.
            var clientGameStats = playerWizard.GameStats.GetClientTypeAlternative();
            var msgBaseHealth = clientGameStats.m_baseHitpoints;

            // 'WC' HP wisps only appear in Unicorn Way, and heal 40% instead of 25%. 'KT' HP wisps are used everywhere else.
            // 'UW' Mana wips only appear in Unicorn Way, and replenish 25% instead of 10%.
            var healthPercentage = _isStarterWisp
                ? STARTING_WORLD_WISP_HEALTH
                : WISP_HEALTH_PERCENT_INCREASE;

            var healthUpdate = (int) (baseHealth * healthPercentage);
            if (currentHealth + healthUpdate > baseHealth) { // Check for overheal.
                healthUpdate = baseHealth - currentHealth;
            }

            // Inform the player's game client that there health has been updated.
            var healthUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH {
                CharacterID = playerWizard.CharId,
                NewHealth = currentHealth + healthUpdate,
                NewHealthMax = msgBaseHealth,
                DisplayDiff = 1
            };
            playerActor.Tell(healthUpdateMsg);

            SendStateChange();
            SendDestroy(playerWizard.CharId);
            playerWizard.UpdateHealth(healthUpdate + currentHealth);
        }
    }

    private void SendStateChange() {
        // Todo: Get this info from somewhere else. Also, currently only works on first pickup.
        var stateHealth = new EmoteStateOverrideInfo {
            m_loop = false,
            m_particleAsset = "Character/FX_WispRed_Dsppr.nif",
            m_soundAsset = "Sound/GUI/ui_health_powerup_01.wav",
            m_stateNameID = 1896147676
        };
        Entity.ChangeState(1896147676, stateHealth);
    }

    private void SendDestroy(ulong killer) 
        => Entity.DeleteObject("WispDespawn", killer);

}