/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 * 
 * ========================================================================
 * WIZARD SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages wizard character state, including location tracking, 
 * level management, and game state synchronization.
 * 
 * USAGE EXAMPLE:
 * Internal service handling wizard-specific interactions within 
 * the game server session.
 * 
 * NOTE:
 * - Supports wizard location and orientation caching
 * - Manages level-up mechanics and stat updates
 * - Provides internal wizard state management
 * 
 * TODO:
 * - Enhance level-up stat calculation
 * - Review location and orientation compression logic
 * 
 * Created by: Jooty, Joji
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.Shared.Character;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Math;
using Microsoft.Extensions.ObjectPool;

namespace Imlight.CoreLib.Game.Services;

internal class WizardService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const float ORIENTATION_TOLERANCE = 1.035f;

    private Wizard _activeWizard;
    private CoreObject _activeWizardGameObject;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new WizardService(parentActor));

    protected override void OnDispose() {
        ((System.IDisposable) _activeWizard)?.Dispose();
        base.OnDispose();
    }

    #region Internal Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP))]
    private void ReceiveZoneAddPlayerResponse(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP message)
        => _activeWizardGameObject = message.WizardGameObject;

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_SETACTIVEWIZARD))]
    private void ReceiveSetActiveWizard(CHARACTER_103_PROTOCOL.MSG_SETACTIVEWIZARD message)
        => _activeWizard = message.Wizard;

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD))]
    private void ReceiveQueryActiveWIzard(CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD message)
        => Sender.Tell(new CHARACTER_103_PROTOCOL.MSG_CHARACTER() {
            Wizard = _activeWizard,
            WizardGameObject = _activeWizardGameObject
        });

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_LEVELUP))]
    private void ReceiveSetLevel(CHARACTER_103_PROTOCOL.MSG_LEVELUP message) {
        // This is the internal level up message. It most likely happened due to a developer command.
        var levelUpSuccess = _activeWizard.SetLevel(message.NewLevel);
        if (!levelUpSuccess) {
            return;
        }

        var levelUpMessage = new WIZARD_12_PROTOCOL.MSG_LEVELUP {
            GlobalID = _activeWizard.CharId,
            NewLevel = _activeWizard.MagicSchoolBehavior.Level,
            Data = "0000000000"
        };
        ZoneBroadcast(levelUpMessage, false);

        // Leveling up the player will set their new stats and heal them.
        // We need to do the code below to echo those changes to the client.
        var magicSchool = _activeWizard.MagicSchoolBehavior.MagicSchool;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, message.NewLevel);

        // Update health.
        var healthMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH() {
            CharacterID = _activeWizardGameObject.m_globalID,
            NewHealth = baseStats.m_hitpoints,
            NewHealthMax = baseStats.m_hitpoints,
            DisplayDiff = 1,
        };
        SendToSocket(healthMessage);

        // Update mana.
        var manaMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEMANA() {
            Mana = baseStats.m_mana,
            MaxMana = baseStats.m_mana,
            DisplayDiff = 1,
        };
        SendToSocket(manaMessage);

        // Update power pips
        var powerPipsMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEPOWERPIP() { PowerPip = baseStats.m_pipChance };
        SendToSocket(powerPipsMessage);

        // Update energy.
        var petEnergyMessage = new PET_9_PROTOCOL.MSG_PETENERGYMAX() {
            MaxEnergy = baseStats.m_petEnergy
        };
        SendToSocket(petEnergyMessage);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_GAINXP))]
    private void ReceiveGainXP(CHARACTER_103_PROTOCOL.MSG_GAINXP message) {
        var beforeLevel = _activeWizard.MagicSchoolBehavior.Level;
        var beforeXP = _activeWizard.MagicSchoolBehavior.ExperiencePoints;
        var xpGained = message.XP;

        _activeWizard.AddExperiencePoints(xpGained);
        var afterLevel = _activeWizard.MagicSchoolBehavior.Level;

        if (beforeLevel != afterLevel) {
            // The player leveled up. AddExperiencePoints already called SetLevel internally,
            // so we just need to broadcast the level-up and send stat updates to the client.
            var levelUpMessage = new WIZARD_12_PROTOCOL.MSG_LEVELUP {
                GlobalID = _activeWizard.CharId,
                NewLevel = afterLevel,
                Data = "0000000000"
            };
            ZoneBroadcast(levelUpMessage, false);

            // Send stat updates to client (health, mana, power pips, energy).
            var magicSchool = _activeWizard.MagicSchoolBehavior.MagicSchool;
            var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, afterLevel);

            var healthMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH() {
                CharacterID = _activeWizardGameObject.m_globalID,
                NewHealth = baseStats.m_hitpoints,
                NewHealthMax = baseStats.m_hitpoints,
                DisplayDiff = 1,
            };
            SendToSocket(healthMessage);

            var manaMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEMANA() {
                Mana = baseStats.m_mana,
                MaxMana = baseStats.m_mana,
                DisplayDiff = 1,
            };
            SendToSocket(manaMessage);

            var powerPipsMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEPOWERPIP() {
                PowerPip = baseStats.m_pipChance
            };
            SendToSocket(powerPipsMessage);

            var petEnergyMessage = new PET_9_PROTOCOL.MSG_PETENERGYMAX() {
                MaxEnergy = baseStats.m_petEnergy
            };
            SendToSocket(petEnergyMessage);
        }

        // Inform the client of the XP change.
        var addXpMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEXP {
            GlobalID = _activeWizard.CharId,
            XP = xpGained,
            OldXP = beforeXP,
        };
        SendToSocket(addXpMsg);
    }

    #endregion

    #region Game Handlers

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE))]
    private void ReceiveClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message) {
        // Save the player's location and direction on interval.
        // Restore actual location information, as it is compressed by a factor of 4 and unsigned.
        // Yaw is represented in radians in the client, but transmitted to the server as degrees.
        var position = new Vector3(
            unchecked((short) message.LocationX * 4),
            unchecked((short) message.LocationY * 4),
            unchecked((short) message.LocationZ * 4));
        _activeWizard.SetCachedLocation(position);

        // Direction is a byte and it's packed. Unpack it and convert it to radians.
        var initDir = message.Direction;
        var degrees = initDir * (360f / byte.MaxValue) * ORIENTATION_TOLERANCE;
        var radians = degrees * (System.Math.PI / 180f);
        _activeWizard.SetCachedOrientation((byte) radians);
    }

    #endregion
    
}
