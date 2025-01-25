/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using DotNetty.Transport.Channels;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.MessageLayer;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

/// <summary>
/// Represents a component within a zone that handles various player and creature interactions.
/// </summary>
public interface IZoneComponent {

    /// <summary>
    /// Called once a <see cref="Zone"/> has completed initialization.
    /// </summary>
    void OnStart();

    /// <summary>
    /// Called when a player joins the zone.
    /// </summary>
    /// <param name="playerObj">The core object representing the player.</param>
    /// <param name="playerActor">The actor reference of the player.</param>
    /// <param name="playerWizard">The wizard associated with the player.</param>
    void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard);

    /// <summary>
    /// Called when a player leaves the zone.
    /// </summary>
    /// <param name="playerActor">The actor reference of the player.</param>
    /// <param name="id">The unique identifier of the player.</param>
    void OnPlayerLeave(IActorRef playerActor, ulong id);

    /// <summary>
    /// Called when a player moves within the zone.
    /// </summary>
    /// <param name="playerObj">The core object representing the player.</param>
    /// <param name="playerActor">The actor reference of the player.</param>
    void OnPlayerMove(CoreObject playerObj, IActorRef playerActor);

    /// <summary>
    /// Called when a creature enters the proximity of the zone.
    /// </summary>
    /// <param name="creature">The core object representing the creature.</param>
    /// <param name="suspect">The actor reference of the suspect.</param>
    void OnCreatureProximityEnter(CoreObject creature, IActorRef suspect);
    
}

/// <summary>
/// Base class for components that implements common functionality
/// </summary>
public abstract class BaseZoneComponent(ZoneEntity entity) : ReceiveProtocolDispatcher, IZoneComponent {

    public IActorRef ActorRef { get; private set; }
    protected ZoneEntity Entity { get; private set; } = entity;
    protected Core.Zone Zone => Entity.Zone;
    protected IActorRef ZoneActor => Entity.ZoneRef;

    public virtual void OnStart() { }
    public virtual void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) { }
    public virtual void OnPlayerLeave(IActorRef playerActor, ulong id) { }
    public virtual void OnPlayerMove(CoreObject playerObj, IActorRef playerActor) { }
    public virtual void OnCreatureProximityEnter(CoreObject creature, IActorRef suspect) { }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONESTART))]
    public void ReceiveZoneStart() {
        ActorRef = Self;
        OnStart();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    public void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) 
        => OnPlayerJoin(message.PlayerObject, message.PlayerActor, message.Wizard);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    public void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) 
        => OnPlayerLeave(message.PlayerActor, message.GlobalId);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERMOVE))]
    public void ReceivePlayerMove(ZONE_102_PROTOCOL.MSG_PLAYERMOVE message) 
        => OnPlayerMove(message.PlayerObject, message.PlayerActor);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ENTITYCOMPONENTREQUESTIDENTITY))]
    public void ReceiveRequestIdentity() 
        => Sender.Tell(new ZONE_102_PROTOCOL.MSG_ENTITYCOMPONENTREQUESTIDENTITYRSP { Component = this });

    /// <summary>
    /// Checks if the specified object is within the radius of the entity.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>True if the object is within the radius, otherwise false.</returns>
    protected bool IsInRadius(CoreObject obj, float distance) {
        var sqrtDist = (obj.m_location - Entity.ActiveGameObject.m_location).LengthSquared();
        var sqrtRadius = distance * distance;
        return sqrtDist <= sqrtRadius;
    }

    /// <summary>
    /// Broadcasts a message to all players within the zone.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    protected void PlayerBroadcast(IMessage message) {
        // Wrap the message in a zone broadcast message.
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEPLAYERBROADCAST { Message = message };
        ZoneActor.Tell(broadcastMsg);
    }

}