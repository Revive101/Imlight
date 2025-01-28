/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.MessageLayer;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Represents a component within a zone that handles various player and creature interactions.
/// </summary>
public interface IZoneComponent {

    /// <summary>
    /// Sent by the <see cref="ZoneEntity"/> when all components have been initialized.
    /// </summary>
    void OnStart();

    /// <summary>
    /// Called once a <see cref="Zone"/> has completed initialization.
    /// </summary>
    void OnZoneStart();

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

    /// <summary>
    /// Enables the component.
    /// </summary>
    void Enable();

    /// <summary>
    /// Disables the component.
    /// </summary>
    void Disable();

    /// <summary>
    /// Called when the component is enabled.
    /// </summary>
    void OnEnabled();
    
    /// <summary>
    /// Called when the component is disabled.
    /// </summary>
    void OnDisabled();

}

/// <summary>
/// Base class for components that implements common functionality
/// </summary>
public abstract class ZoneEntityComponent(ZoneEntity entity) : ReceiveProtocolDispatcher, IZoneComponent {

    public IActorRef ActorRef { get; private set; }
    protected ZoneEntity Entity { get; private set; } = entity;
    protected Zone Zone => Entity.Zone;
    protected IActorRef ZoneActor => Entity.ZoneRef;
    private bool _enabled = true;

    public virtual void OnStart() { }
    public virtual void OnZoneStart() { }
    public virtual void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) { }
    public virtual void OnPlayerLeave(IActorRef playerActor, ulong id) { }
    public virtual void OnPlayerMove(CoreObject playerObj, IActorRef playerActor) { }
    public virtual void OnCreatureProximityEnter(CoreObject creature, IActorRef suspect) { }
    public virtual void OnEnabled() { }
    public virtual void OnDisabled() { }

    public void Enable() {
        _enabled = true;
        OnEnabled();
    }
    
    public void Disable() {
        _enabled = false;
        OnDisabled();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONESTART))]
    public void ReceiveZoneStart() {
        if (!_enabled) {
            return;
        }

        ActorRef = Self;
        OnZoneStart();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    public void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        if (!_enabled) {
            return;
        }

        OnPlayerJoin(message.PlayerObject, message.PlayerActor, message.Wizard);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    public void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        if (!_enabled) {
            return;
        }

        OnPlayerLeave(message.PlayerActor, message.GlobalId);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERMOVE))]
    public void ReceivePlayerMove(ZONE_102_PROTOCOL.MSG_PLAYERMOVE message) {
        if (!_enabled) {
            return;
        }

        OnPlayerMove(message.PlayerObject, message.PlayerActor);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ENTITYCOMPONENTREQUESTIDENTITY))]
    public void ReceiveRequestIdentity() {
        if (!_enabled) {
            return;
        }

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ENTITYCOMPONENTREQUESTIDENTITYRSP { Component = this });
    }
        
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTINITIALIZED))]
    public void ReceiveObjectStart() {
        if (!_enabled) {
            return;
        }

        OnStart();
    }

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