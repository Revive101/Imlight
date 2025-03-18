/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class WispManaComponent : ZoneEntityComponent, IComponentFactory {

    private const float INTERACTION_RADIUS = 100.0f;
    private const float STARTING_WORLD_WISP_MANA = 0.25f;
    private const float WISP_MANA_PERCENT_INCREASE = 0.10f;

    private readonly bool _isStarterWisp;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_objectName.ToString().Contains("Wisp")
        && goTemplate.m_objectName.ToString().Contains("Mana");

    // ctor
    public WispManaComponent(ZoneEntity entity) : base(entity) {
        if (entity.Template is GameObjectTemplate goTemplate) {
            var objectName = goTemplate.m_objectName.ToString();
            _isStarterWisp = objectName.Contains("UW");
        }
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (IsInRadius(playerObj, INTERACTION_RADIUS)) {
            // Values with gear and effects.
            var baseMana = playerWizard.GameStats.m_baseMana;
            var currentMana = playerWizard.GameStats.m_currentMana;

            if (baseMana == currentMana) { // If mana is full, no need to replenish.
                return;
            }

            // Values before effects are applied.
            var clientGameStats = playerWizard.GameStats.GetClientTypeAlternative();
            var msgBaseMana = clientGameStats.m_baseMana;

            var manaPercentage = _isStarterWisp
                ? STARTING_WORLD_WISP_MANA
                : WISP_MANA_PERCENT_INCREASE;

            var manaUpdate = (int) (baseMana * manaPercentage); // Check for overflow.
            if (currentMana + manaUpdate > baseMana) {
                manaUpdate = baseMana - currentMana;
            }

            var manaUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
                Mana = currentMana + manaUpdate,
                MaxMana = msgBaseMana,
                DisplayDiff = 1
            };
            playerActor.Tell(manaUpdateMsg);

            SendStateChange();
            SendDestroy(playerWizard.CharId);
            playerWizard.UpdateMana(currentMana + manaUpdate);
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