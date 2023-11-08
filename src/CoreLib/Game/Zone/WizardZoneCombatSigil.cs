using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

public class WizardZoneCombatSigil : WizardZoneObject {
    private const uint SigilTemplateId = 1901671683;

    private IActorRef _activeDuelActor;
    private Duel _activeDuel;

    public WizardZoneCombatSigil(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) {
    }

    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef) {
        return Akka.Actor.Props.Create(() => new WizardZoneCombatSigil(activeGameObject, template, wizardZoneRef));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL))]
    private void ReceiveSpawnSigil(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message) {
        var duelCreated = CreateDuelActor(message.Participants);
        SpawnCombatSigilObject(duelCreated);
    }

    private Duel CreateDuelActor(Dictionary<IActorRef, CoreObject> participants) {
        var createMsg = new COMBAT_106_PROTOCOL.MSG_STARTDUEL {
            Participants = participants,
            SigilId = ActiveGameObject.m_globalID,
            SigilLocation = ActiveGameObject.m_location,
            SigilOrientation = ActiveGameObject.m_orientation,
        };
        var createRsp = WizardZoneRef
            .Ask<COMBAT_106_PROTOCOL.MSG_DUELDETAILS>(createMsg)
            .Result;

        _activeDuelActor = createRsp.DuelActor;
        _activeDuel = createRsp.Duel;
        return createRsp.Duel;
    }

    private void SpawnCombatSigilObject(Duel duel) {
        if (_activeDuel is null || _activeDuelActor is null) {
            throw new Exception("Duel or DuelActor is null. Cannot spawn combat sigil object.");
        }

        // Initialize the behaviors on the object. One of them is the DuelBehavior, which we
        // need to adjust.
        CoreObjectFactory.InitializeCoreObjectBehaviors(ActiveGameObject, 560);
        if (CoreObjectFactory.FindBehaviorInstance(ActiveGameObject, out DuelBehavior duelBehavior)) {
            duelBehavior.m_sigilTemplateID = SigilTemplateId;
            duelBehavior.m_pDuel = _activeDuel;
        }
        else {
            throw new Exception("Could not find DuelBehavior on CoreObject.");
        }

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
}
