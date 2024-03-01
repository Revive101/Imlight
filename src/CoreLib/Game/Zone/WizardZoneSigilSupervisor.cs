/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// Exists as a child of <see cref="WizardZone"/> and supervises
/// a bunch of child <see cref="WizardZoneCombatSigil"/> actors.
/// </summary>
public class WizardZoneSigilSupervisor : ReceiveProtocolDispatcher {
    private readonly IActorRef _wizardZoneRef;
    private readonly Dictionary<IActorRef, CoreObject> _sigils;
    private readonly TimeSpan _statusCheckTimeout = TimeSpan.FromSeconds(5);

    // ctor
    public WizardZoneSigilSupervisor(IActorRef wizardZoneRef) {
        this._wizardZoneRef = wizardZoneRef;
        this._sigils = new Dictionary<IActorRef, CoreObject>();
    }

    // Akka.NET ctor
    public static Props Props(IActorRef wizardZoneRef) {
        return Akka.Actor.Props.Create(() => new WizardZoneSigilSupervisor(wizardZoneRef));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGIL))]
    private void ReceiveAddCombatSigil(ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGIL message) {
        var props = WizardZoneCombatSigil.Props(message.CoreObject, message.Template, _wizardZoneRef);
        var actorCreated = CreateChildActor(props);

        // We need to actually store the CoreObject so we can use it later.
        var statusCheckMsg = new ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECK();
        var statusCheckRsp = actorCreated.
            Ask<ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECKRSP>(statusCheckMsg, _statusCheckTimeout)
            .Result;

        // Add the sigil locally.
        _sigils.Add(actorCreated, statusCheckRsp.CoreObject);

        // Tell the sender that the object was created.
        var rsp = new ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGILRSP { ActorRef = actorCreated };
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL))]
    private void ReceiveRequestCombatSigil(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message) {
        // Find the closest sigil to the primary suspect.
        var primarySuspect = message.StartingParticipants.First().Value;
        var closestSigilActor = FindClosestSigil(primarySuspect);

        // Forward the message to the closest sigil.
        closestSigilActor.Forward(message);
    }

    private IActorRef CreateChildActor(Props props) => Context.ActorOf(props);

    private IActorRef FindClosestSigil(CoreObject primarySuspect) {
        var closestSigil = _sigils
            .OrderBy(x => Vector3.Distance(x.Value.m_location, primarySuspect.m_location))
            .First();

        return closestSigil.Key;
    }
}
