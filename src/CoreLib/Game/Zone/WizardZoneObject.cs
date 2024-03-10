/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// This is a zone object which manages itself as an actor.
/// </summary>
public class WizardZoneObject : ReceiveProtocolDispatcher {
    public readonly CoreObject ActiveGameObject;
    public readonly CoreTemplate Template;

    protected readonly IActorRef WizardZoneRef;
    protected float InteractionRadius = 300f;

    private readonly List<CoreObject> _objsInRadius;

    // ctor
    public WizardZoneObject(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef) {
        this.ActiveGameObject = activeGameObject;
        this.Template = template;
        this.WizardZoneRef = wizardZoneRef;
        this._objsInRadius = new List<CoreObject>();
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef) => Akka.Actor.Props.Create(() => new WizardZoneObject(activeGameObject, template, wizardZoneRef));

    /// <summary>
    /// Called when a player joins the wizard zone. Sends the player the object data and adds the object to the zone.
    /// </summary>
    /// <param name="player">The player object that joined the zone.</param>
    /// <param name="suspect">The actor reference of the player that joined the zone.</param>
    protected virtual void OnPlayerJoin(CoreObject player, IActorRef suspect) {
        // If the player spawns within this object, add them to the list of objects in radius.
        if (IsInRadius(player)) {
            _objsInRadius.Add(player);
        }

        // When a new player joins, we need to send them the object data.
        var serializer = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                | SerializerOptions.PropertyFlags.Transmit
                | SerializerOptions.PropertyFlags.AuthorityTransmit);
        var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(ActiveGameObject) };
        suspect.Tell(msg);

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP());
    }

    /// <summary>
    /// Called when a player leaves the wizard zone object.
    /// </summary>
    /// <param name="suspect">The actor reference of the player who left.</param>
    protected virtual void OnPlayerLeave(IActorRef suspect, ulong id) {
        // Tell the player client to remove this object from the world.
        var msg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = ActiveGameObject.m_globalID
        };
        suspect.Tell(msg);
        Sender.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLAYERRSP());

        // Remove the player object from our radius.
        _objsInRadius.RemoveAll(x => x.m_globalID == id);
    }

    /// <summary>
    /// Called when a player enters the interaction zone of this object.
    /// </summary>
    /// <param name="player">The player object that entered the zone.</param>
    /// <param name="suspect">The actor reference of the suspect.</param>
    protected virtual void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) {

    }

    /// <summary>
    /// Called when a player exits the interaction range of this object.
    /// </summary>
    /// <param name="player">The player who exited the interaction range.</param>
    /// <param name="suspect">The suspect actor reference.</param>
    protected virtual void OnPlayerInteractionExit(CoreObject player, IActorRef suspect) {

    }

    /// <summary>
    /// Called when a creature enters the interaction zone of this object.
    /// </summary>
    /// <param name="creature">The CoreObject representing the creature.</param>
    /// <param name="suspect">The IActorRef representing the suspect.</param>
    protected virtual void OnCreatureInteractionEnter(CoreObject creature, IActorRef suspect) {

    }

    /// <summary>
    /// Something has queried the status of this object.
    /// </summary>
    protected virtual void OnStatusCheck() {
        var failure = ActiveGameObject == null || Template == null;
        var reason = failure ? "Object or template is null." : null;

        var rsp = new ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECKRSP {
            ZoneObject = this,
            CoreObject = ActiveGameObject,
            Failure = failure,
            Error = reason
        };

        Sender.Tell(rsp);
    }

    /// <summary>
    /// Gets the position of the active game object.
    /// </summary>
    /// <returns>The position as a Vector3.</returns>
    protected virtual Vector3 GetPosition() => ActiveGameObject.m_location;

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    protected virtual void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
        => OnPlayerJoin(message.PlayerObject, message.Player);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    protected virtual void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
        => OnPlayerLeave(message.Player, message.GlobalId);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_FISHINTERACTION))]
    protected void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_FISHINTERACTION message) {
        // An actor is asking if our current game object is within a certain interaction radius.
        if (IsInRadius(message.CoreObject)) {
            // Keep track of the objects already within radius as to not trigger duplicate events.
            if (_objsInRadius.Contains(message.CoreObject)) {
                return;
            }

            // Do enter events.
            _objsInRadius.Add(message.CoreObject);
            if (message.IsCreature) {
                OnCreatureInteractionEnter(message.CoreObject, message.Suspect);
            }
            else {
                OnPlayerInteractionEnter(message.CoreObject, message.Suspect);
            }
        }
        else if (_objsInRadius.Contains(message.CoreObject) && !IsInRadius(message.CoreObject)) {
            // Do exit events.
            _objsInRadius.Remove(message.CoreObject);
            if (!message.IsCreature) {
                OnPlayerInteractionExit(message.CoreObject, message.Suspect);
            }
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    protected void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message) =>
        WizardZoneRef.Tell(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECK))]
    protected void ReceiveStatusCheck(ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECK message) =>
        OnStatusCheck();

    private bool IsInRadius(CoreObject obj1) {
        var sqrtDist = (obj1.m_location - GetPosition()).LengthSquared();
        var sqrtRadius = InteractionRadius * InteractionRadius;

        return sqrtDist <= sqrtRadius;
    }
}
