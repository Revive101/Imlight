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
/// Exists as a child of <see cref="WizardZoneLegacy"/> and supervises
/// a bunch of child <see cref="WizardZoneSigil"/> actors.
/// </summary>
public class WizardZoneSigilSupervisor : ReceiveProtocolDispatcher {
    private readonly IActorRef _wizardZoneRef;
    private readonly Dictionary<IActorRef, CoreObject> _sigils;
    private readonly TimeSpan _statusCheckTimeout = TimeSpan.FromSeconds(5);
    private readonly Dictionary<IActorRef, DateTime> _actorCache = new();
    private readonly TimeSpan _actorCacheTimeout = TimeSpan.FromSeconds(5);

    // ctor
    public WizardZoneSigilSupervisor(IActorRef wizardZoneRef) {
        this._wizardZoneRef = wizardZoneRef;
        this._sigils = new Dictionary<IActorRef, CoreObject>();
    }

    // Akka.NET ctor
    public static Props Props(IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneSigilSupervisor(wizardZoneRef));

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGIL))]
    private void ReceiveAddCombatSigil(ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGIL message) {
        var props = WizardZoneSigil.Props(message.CoreObject, message.SigilType, message.Template, _wizardZoneRef);
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
        // Check if we have a cached actor. If we do, check to see if their timeout has expired.
        // If it hasn't, it must have interacted with two different suspects at once.
        if (_actorCache.TryGetValue(Sender, out var cacheTime)) {
            if (DateTime.Now - cacheTime < _actorCacheTimeout) {
                return;
            }
            else {
                _actorCache.Remove(Sender);
            }
        }
        else {
            _actorCache.Add(Sender, DateTime.Now);
        }

        // Find the closest sigil to the primary suspect.
        var primarySuspect = message.StartingParticipants.First().Value;
        var closestSigilActor = FindClosestSigil(primarySuspect);

        // Forward the message to the closest sigil.
        closestSigilActor.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST))]
    private void ReceiveZoneObjectBroadcast(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST message) {
        foreach (var sigilActor in _sigils.Keys) {
            foreach (var msg in message.Messages) {
                sigilActor.Forward(msg);
            }
        }
    }

    private IActorRef CreateChildActor(Props props) => Context.ActorOf(props);

    private IActorRef FindClosestSigil(CoreObject primarySuspect) {
        var closestSigil = _sigils
            .OrderBy(x => Vector3.Distance(x.Value.m_location, primarySuspect.m_location))
            .First();

        return closestSigil.Key;
    }
}
