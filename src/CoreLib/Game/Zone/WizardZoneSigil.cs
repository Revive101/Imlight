/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Sigils;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// This class is responsible for creating a duel actor and spawning the combat sigil object.
/// It only represents the combat sigil object, not the duel itself.
/// </summary>
public class WizardZoneSigil : WizardZoneObject {
    private const uint SigilTemplateId = 1901671683;

    private readonly CombatSigilTemplate _combatSigilTemplate;
    private WizardClientDuelBehavior _duelBehavior;
    private IActorRef _activeDuelActor;
    private Duel _activeDuel;

    public WizardZoneSigil(CoreObject activeGameObject, string sigilType, CoreTemplate template, IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) {
        // Load the combat sigil template.
        _combatSigilTemplate = (CombatSigilTemplate) SigilFactory.GetSigilTemplate(sigilType);
        if (_combatSigilTemplate is null) {
            Logger.Error("Could not find combat sigil template with ID {0}.", Logger.Args(SigilTemplateId));
        }

        InitializeDuelBehavior();
    }

    public static Props Props(CoreObject activeGameObject, string sigilType, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneSigil(activeGameObject, sigilType, template, wizardZoneRef));

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect, Wizard wizard) {
        if (_activeDuel is not null) {
            base.OnPlayerJoin(player, suspect, wizard);

            // Inform the active duel of the new player.
            var msg = new ZONE_102_PROTOCOL.MSG_ADDPLAYER {
                Player = suspect,
                PlayerObject = player,
            };
            _activeDuelActor.Tell(msg);
        }
    }

    protected override void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) {
        if (_activeDuel is null || _activeDuelActor is null) {
            return;
        }

        if (IsPlayerSlotAvailable()) {
            AddParticipant(suspect, player);
        }
    }

    protected override void OnCreatureInteractionEnter(CoreObject creature, IActorRef suspect) {
        if (_activeDuel is null || _activeDuelActor is null) {
            return;
        }

        if (IsCreatureSlotAvailable()) {
            AddParticipant(suspect, creature);
        }
        else {
            var combatDeathMsg = new COMBAT_106_PROTOCOL.MSG_COMBATDEATH();
            suspect.Tell(combatDeathMsg);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL))]
    private void ReceiveRequestSigil(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message) {
        // If there is already an active duel, we'll add them to it.
        if (_activeDuel is not null) {
            foreach (var participant in message.StartingParticipants) {
                var participantActor = participant.Key;
                var participantObject = participant.Value;
                var isCreature = participantObject.m_templateID != 1;

                var isSlotAvailable = isCreature ? IsCreatureSlotAvailable() : IsPlayerSlotAvailable();

                if (isSlotAvailable) {
                    AddParticipant(participantActor, participantObject);
                }
            }
            return;
        }

        // Otherwise, request a new duel from the WizardZone's duel supervisor.
        var createMsg = new COMBAT_106_PROTOCOL.MSG_STARTDUEL {
            Participants = message.StartingParticipants,
            SigilActor = Self,
            SigilId = ActiveGameObject.m_globalID,
            SigilLocation = ActiveGameObject.m_location,
            SigilOrientation = ActiveGameObject.m_orientation,
            SigilTemplate = _combatSigilTemplate,
        };

        // The zone is going to be the one to create the duel. Await its reply here.
        var createRsp = WizardZoneRef
            .Ask<COMBAT_106_PROTOCOL.MSG_DUELDETAILS>(createMsg)
            .Result;

        _activeDuelActor = createRsp.DuelActor;
        _activeDuel = createRsp.Duel;
        base.InteractionRadius = _combatSigilTemplate.m_engageRadius;

        SpawnCombatSigilObject();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVECOMBATSIGIL))]
    private void ReceiveRemoveCombatSigil(ZONE_102_PROTOCOL.MSG_REMOVECOMBATSIGIL message) {
        // The duel is over. Despawn the combat sigil object.
        DespawnCombatSigilObject();

        // Clear the active duel and duel actor.
        _activeDuel = null;
        _activeDuelActor = null;
        _duelBehavior.m_pDuel = null;
    }

    private void InitializeDuelBehavior() {
        var duelBehavior = new WizardClientDuelBehavior() {
            m_sigilTemplateID = SigilTemplateId,
            m_pDuel = _activeDuel,
        };

        base.Behaviors.Add(duelBehavior);
        this._duelBehavior = duelBehavior;
    }

    private void SpawnCombatSigilObject() {
        if (_activeDuel is null || _activeDuelActor is null) {
            throw new Exception("Duel or DuelActor is null. Cannot spawn combat sigil object.");
        }

        // Set the DuelBehavior's properties.
        _duelBehavior.m_pDuel = _activeDuel;

        SpawnSelf();
    }

    private void DespawnCombatSigilObject() {
        var msg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT { GameObjectID = ActiveGameObject.m_globalID };
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Selfless = true,
            Message = msg,
            Sender = Self,
        };
        base.WizardZoneRef.Tell(broadcastMsg);
    }

    private bool IsPlayerSlotAvailable() {
        if (_activeDuel is null || _activeDuelActor is null) {
            return false;
        }

        var checkForSlotMsg = new COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLE { Team = Combat.CombatTeam.Player };
        var slotAvailable = _activeDuelActor
            .Ask<COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLERSP>(checkForSlotMsg)
            .Result
            .Available;

        return slotAvailable;
    }

    private bool IsCreatureSlotAvailable() {
        if (_activeDuel is null || _activeDuelActor is null) {
            return false;
        }

        var checkForSlotMsg = new COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLE { Team = Combat.CombatTeam.Monster };
        var slotAvailable = _activeDuelActor
            .Ask<COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLERSP>(checkForSlotMsg)
            .Result
            .Available;

        return slotAvailable;
    }

    private void AddParticipant(IActorRef actorRef, CoreObject obj) {
        if (_activeDuel is null || _activeDuelActor is null) {
            return;
        }

        var msg = new COMBAT_106_PROTOCOL.MSG_ADDPARTICIPANT {
            Participant = actorRef,
            ParticipantObject = obj,
        };
        _activeDuelActor.Tell(msg);
    }
}
