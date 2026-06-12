/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * COMPONENT FRAMEWORK
 * ========================================================================
 * 
 * PURPOSE:
 * Provides the base implementation for all entity components, handling lifecycle
 * events and entity-component communication within the zone system.
 * 
 * USAGE EXAMPLE:
 * public class CustomComponent : ZoneEntityComponent {
 *     public CustomComponent(ZoneEntity entity) : base(entity) { }
 *     
 *     public override void OnStart() {
 *         // Initialize component
 *     }
 * }
 * 
 * NOTE:
 * Components use Akka message handlers for event communication.
 * Lifecycle methods follow a specific order: OnAwake, OnStart, OnZoneStart.
 *
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Represents a component within a zone that handles various player and creature interactions.
/// </summary>
public interface IZoneComponent {

    /// <summary>
    /// Sent by the <see cref="ZoneEntity"/> when all components have been initialized.
    /// Called before <see cref="OnStart"/>.
    /// </summary>
    void OnAwake();

    /// <summary>
    /// Sent by the <see cref="ZoneEntity"/> when all components have been initialized.
    /// Called after <see cref="OnAwake"/>.
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
    /// <param name="playerWizard">The wizard associated with the player.</param>
    void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard);

    /// <summary>
    /// Called when a creature moves within the zone.
    /// </summary>
    /// <param name="creature">The core object representing the creature.</param>
    /// <param name="suspect">The actor reference of the suspect.</param>
    /// <param name="entity">The zone entity associated with the creature.</param>
    void OnCreatureMove(CoreObject creature, IActorRef suspect, ZoneEntity entity);

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
    private bool _awakeCalled = false;

    public virtual void OnAwake() { }
    public virtual void OnStart() { }
    public virtual void OnZoneStart() { }
    public virtual void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) { }
    public virtual void OnPlayerLeave(IActorRef playerActor, ulong id) { }
    public virtual void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) { }
    public virtual void OnCreatureMove(CoreObject creature, IActorRef suspect, ZoneEntity entity) { }
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

        OnPlayerMove(message.PlayerObject, message.PlayerActor, message.PlayerWizard);
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
        ActorRef = Self;
        
        if (!_enabled) {
            Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTINITIALIZED());
            
            return;
        }

        if (!_awakeCalled) {
            _awakeCalled = true;
            OnAwake();
            Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTINITIALIZED());
        }
        else {
            OnStart();
            Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTINITIALIZED());
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATUREMOVE))]
    public void ReceiveCreatureMove(ZONE_102_PROTOCOL.MSG_CREATUREMOVE message) {
        if (!_enabled) {
            return;
        }

        OnCreatureMove(message.CreatureObject, message.CreatureActor, message.CreatureEntity);
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
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST { Message = message };
        ZoneActor.Tell(broadcastMsg);
    }

}