/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Triggers;

/// <summary>
/// Interface that defines a trigger's ability to execute a trigger event
/// </summary>
public interface IResultHandler {

    /// <summary>
    /// Executes the trigger event.
    /// </summary>
    /// <param name="playerRef">The reference to the player that triggered the event.</param>
    /// <param name="zoneRef">The reference to the zone that the trigger is a part of.</param>
    void Execute(IActorRef playerRef, CoreObject playerObj);

}

/// <summary>
/// Base class for all result handlers.
/// </summary>
public abstract class BaseResultHandler(ZoneTrigger trigger) : ReceiveProtocolDispatcher, IResultHandler {
        
    protected ZoneTrigger Trigger { get; } = trigger;
    protected Result Result { get; init; }
    protected Core.Zone Zone => Trigger.Zone;
    protected IActorRef ZoneActor => Trigger.ZoneRef;

    public abstract void Execute(IActorRef playerRef, CoreObject playerObj);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    public void HandlePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) 
        => Execute(message.PlayerActor, message.PlayerGameObject);

}