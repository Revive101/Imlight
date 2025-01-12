/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
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
    void Execute(IActorRef playerRef, IActorRef zoneRef);

}

public abstract class BaseResultHandler(ZoneTrigger trigger) : ReceiveProtocolDispatcher,
    IResultHandler,
    IResultHandlerFactory {
        
    protected ZoneTrigger Trigger { get; } = trigger;
    protected Result Result { get; init; }
    protected Core.Zone Zone => Trigger.Zone;
    protected IActorRef ZoneActor => Trigger.ZoneRef;

    public abstract void Execute(IActorRef playerRef, IActorRef zoneRef);

    public static bool ShouldAttachToEntity(ZoneTrigger trigger) => false;

}