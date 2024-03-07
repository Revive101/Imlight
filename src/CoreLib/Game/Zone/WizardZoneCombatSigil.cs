/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Sigils;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// This class is responsible for creating a duel actor and spawning the combat sigil object.
/// It only represents the combat sigil object, not the duel itself.
/// </summary>
public class WizardZoneCombatSigil : WizardZoneObject {
    private const uint SigilTemplateId = 1901671683;

    private readonly DuelBehavior _duelBehavior;
    private readonly CombatSigilTemplate _combatSigilTemplate;
    private IActorRef _activeDuelActor;
    private Duel _activeDuel;

    public WizardZoneCombatSigil(CoreObject activeGameObject, string sigilType, CoreTemplate template, IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) {
        // Load the combat sigil template.
        _combatSigilTemplate = (CombatSigilTemplate) SigilFactory.GetSigilTemplate(sigilType);
        if (_combatSigilTemplate is null) {
            Logger.Error("Could not find combat sigil template with ID {0}.", Logger.Args(SigilTemplateId));
        }

        // Initialize the behaviors on the object. One of them is the DuelBehavior,.
        CoreObjectFactory.InitializeCoreObjectBehaviors(ActiveGameObject, template);
        if (CoreObjectFactory.FindBehaviorInstance(ActiveGameObject, out DuelBehavior duelBehavior)) {
            duelBehavior.m_sigilTemplateID = SigilTemplateId;
            duelBehavior.m_pDuel = _activeDuel;
            _duelBehavior = duelBehavior;
        }
        else {
            throw new Exception("Could not find DuelBehavior on CoreObject.");
        }
    }

    public static Props Props(CoreObject activeGameObject, string sigilType, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneCombatSigil(activeGameObject, sigilType, template, wizardZoneRef));

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect) {
        if (_activeDuel is not null) {
            base.OnPlayerJoin(player, suspect);
        }
    }

    protected override void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) {
        if (_activeDuel is null || _activeDuelActor is null) {
            return;
        }

        if (IsPlayerSlotAvailable()) {
            AddParticipant(suspect, player);
        }
        else {
            Logger.Debug("Cannot add player {0} to duel {1} because there are already 4 players.",
                Logger.Args(player.m_globalID, _activeDuelActor));
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
            Logger.Debug("Cannot add creature {0} to duel {1} is full.",
                Logger.Args(creature.m_globalID, _activeDuelActor));
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
        base.InteractionRadius = _combatSigilTemplate.m_engageRadius;;

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

    private void SpawnCombatSigilObject() {
        if (_activeDuel is null || _activeDuelActor is null) {
            throw new Exception("Duel or DuelActor is null. Cannot spawn combat sigil object.");
        }

        // Set the DuelBehavior's properties.
        _duelBehavior.m_pDuel = _activeDuel;

        // Serialize the object and broadcast it to the zone.
        var serializer = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                | SerializerOptions.PropertyFlags.Transmit
                | SerializerOptions.PropertyFlags.AuthorityTransmit);
        var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(ActiveGameObject) };

        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Selfless = true,
            Message = msg,
            Sender = Self,
        };
        base.WizardZoneRef.Tell(broadcastMsg);
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

        var checkForSlotMsg = new COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLE {
            Team = Combat.CombatTeam.Player
        };
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

        var checkForSlotMsg = new COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLE {
            Team = Combat.CombatTeam.Monster
        };
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
