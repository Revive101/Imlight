/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// This is a zone object which manages itself as an actor.
/// </summary>
public class WizardZoneObject : ReceiveProtocolDispatcher, IClientTypeProvider<WizClientObject> {
    public readonly CoreObject ActiveGameObject;
    public readonly CoreTemplate Template;
    public IActorRef ActorRef;

    protected readonly IActorRef WizardZoneRef;
    protected readonly List<BehaviorInstance> Behaviors = new();
    protected float InteractionRadius = 300f;
    protected ServerObjectStateBehavior StateBehavior;

    private readonly List<CoreObject> _objsInRadius;
    private readonly CoreObjectSerializer _zoneObjectSerializer = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                | SerializerOptions.PropertyFlags.Transmit
                | SerializerOptions.PropertyFlags.AuthorityTransmit);

    // ctor
    public WizardZoneObject(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef) {
        this.ActiveGameObject = activeGameObject;
        this.Template = template;
        this.WizardZoneRef = wizardZoneRef;
        this._objsInRadius = new List<CoreObject>();
        this.ActorRef = Context.Self;

        if (template is not null &&
            template.m_behaviors.Any(x => x is ObjectStateBehaviorTemplate)) {
            CreateStateBehavior(template.m_behaviors.OfType<ObjectStateBehaviorTemplate>().First());
        }
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneObject(activeGameObject, template, wizardZoneRef));

    /// <summary>
    /// Tries to retrieve the behavior of type T from the list of behaviors.
    /// </summary>
    /// <typeparam name="T">The type of behavior to retrieve.</typeparam>
    /// <param name="behavior">The behavior of type T if found, otherwise null.</param>
    /// <returns>True if the behavior of type T is found, otherwise false.</returns>
    public bool TryGetBehavior<T>(out T behavior) where T : BehaviorInstance {
        foreach (var b in Behaviors) {
            if (b is T t) {
                behavior = t;
                return true;
            }
        }

        behavior = null;
        return false;
    }

    /// <summary>
    /// Called when a player joins the wizard zone. Sends the player the object data and adds the object to the zone.
    /// </summary>
    /// <param name="player">The player object that joined the zone.</param>
    /// <param name="suspect">The actor reference of the player that joined the zone.</param>
    protected virtual void OnPlayerJoin(CoreObject player, IActorRef suspect, Wizard wizard) {
        // If the player spawns within this object, add them to the list of objects in radius.
        if (IsInRadius(player)) {
            _objsInRadius.Add(player);
        }

        // When a new player joins, we need to send them the object data.
        var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = _zoneObjectSerializer.Serialize(GetClientTypeAlternative()) };
        suspect.Tell(msg);

        // Check this player's dynamic modifications. If they have any, apply them.
        var dynamod = wizard.DynamodSet.Dynamods
            .FirstOrDefault(x => StringHash.Compute(x.ClientTag) == ActiveGameObject.m_zoneTagID);
        if (dynamod != null) {
            EnterState(dynamod.ModState, suspect);
        }

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
    protected virtual void OnPlayerProximityEnter(CoreObject player, IActorRef suspect) {

    }

    /// <summary>
    /// Called when a player exits the interaction range of this object.
    /// </summary>
    /// <param name="player">The player who exited the interaction range.</param>
    /// <param name="suspect">The suspect actor reference.</param>
    protected virtual void OnPlayerProximityExit(CoreObject player, IActorRef suspect) {

    }

    /// <summary>
    /// Called when a player interactions (by pressing the interact key) with this object.
    /// </summary>
    /// <param name="message">The message containing the interaction data.</param>
    /// <param name="suspect">The actor reference of the player.</param>
    protected virtual void OnPlayerInteraction(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message, IActorRef suspect) {

    }

    /// <summary>
    /// Called when a creature enters the interaction zone of this object.
    /// </summary>
    /// <param name="creature">The CoreObject representing the creature.</param>
    /// <param name="suspect">The IActorRef representing the suspect.</param>
    protected virtual void OnCreatureProximityEnter(CoreObject creature, IActorRef suspect) {

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
        => OnPlayerJoin(message.PlayerObject, message.Player, message.Wizard);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    protected virtual void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
        => OnPlayerLeave(message.Player, message.GlobalId);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_FISHINTERACTION))]
    protected void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_FISHINTERACTION message) {
        if (message.CoreObject is null) {
            return;
        }

        // An actor is asking if our current game object is within a certain interaction radius.
        if (IsInRadius(message.CoreObject)) {
            // Keep track of the objects already within radius as to not trigger duplicate events.
            if (_objsInRadius.Contains(message.CoreObject)) {
                return;
            }

            // Do enter events.
            _objsInRadius.Add(message.CoreObject);
            if (message.IsCreature) {
                OnCreatureProximityEnter(message.CoreObject, message.Suspect);
            }
            else {
                OnPlayerProximityEnter(message.CoreObject, message.Suspect);
            }
        }
        else if (_objsInRadius.Contains(message.CoreObject) && !IsInRadius(message.CoreObject)) {
            // Do exit events.
            _objsInRadius.Remove(message.CoreObject);
            if (!message.IsCreature) {
                OnPlayerProximityExit(message.CoreObject, message.Suspect);
            }
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    protected void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message) =>
        WizardZoneRef.Tell(message);

    [MessageHandler(typeof(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC))]
    protected void ReceiveInteractNPC(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message)
        => OnPlayerInteraction(message, Sender);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECK))]
    protected void ReceiveStatusCheck(ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECK message) =>
        OnStatusCheck();

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_ENTERSTATE))]
    private void ReceiveEnterState(CHARACTER_103_PROTOCOL.MSG_ENTERSTATE message) {
        EnterState(message.StateName, Sender);

        // Broadcast the state change to all players.
        var stateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = ActiveGameObject.m_globalID,
            State = StringHash.Compute(message.StateName)
        };

        var zoneBroadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = stateMsg,
            Selfless = true,
            Sender = Self
        };
        WizardZoneRef.Tell(zoneBroadcastMsg);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD))]
    private void ReceiveAddDynaMod(CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD message)
        => EnterState(message.DynaMod.m_dynaModState, message.ContextActor);

    protected void SpawnSelf() {
        var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = _zoneObjectSerializer.Serialize(GetClientTypeAlternative()) };

        // Broadcast the spawn of this creature to all players.
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = msg,
            Selfless = true,
            Sender = Self
        };
        WizardZoneRef.Tell(broadcastMsg);
    }

    protected void EnterState(string newStateName, IActorRef sender) {
        var objState = StateBehavior.SetState(newStateName);
        if (objState is null) {
            Logger.Error("Failed to enter state {0} for creature {1}", Logger.Args(newStateName, ActiveGameObject.m_debugName));
            return;
        }

        var stateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = ActiveGameObject.m_globalID,
            State = StringHash.Compute(newStateName)
        };

        sender.Tell(stateMsg);
    }

    private bool IsInRadius(CoreObject obj1) {
        var sqrtDist = (obj1.m_location - GetPosition()).LengthSquared();
        var sqrtRadius = InteractionRadius * InteractionRadius;

        return sqrtDist <= sqrtRadius;
    }

    private void CreateStateBehavior(ObjectStateBehaviorTemplate objectStateBehaviorTemplate) {
        StateBehavior = new ServerObjectStateBehavior(objectStateBehaviorTemplate.m_stateSetName);
        this.Behaviors.Add(StateBehavior);
    }

    public WizClientObject GetClientTypeAlternative() {
        var gameObj = new WizClientObject() {
            m_debugName = ActiveGameObject.m_debugName,
            m_globalID = ActiveGameObject.m_globalID,
            m_location = ActiveGameObject.m_location,
            m_nMobileID = ActiveGameObject.m_nMobileID,
            m_orientation = ActiveGameObject.m_orientation,
            m_permID = ActiveGameObject.m_permID,
            m_templateID = ActiveGameObject.m_templateID,
            m_zoneTagID = ActiveGameObject.m_zoneTagID,
            m_inactiveBehaviors = ActiveGameObject.m_inactiveBehaviors ?? [],
        };

        foreach (var behaviorInstance in this.Behaviors) {
            BehaviorInstance instance = behaviorInstance;

            // If this is a server behavior, it may not play nicely in the client when we serialize it.
            // We need to convert it to a client behavior.
            if (instance is ServerBehaviorInstance serverBehaviorInstance) {
                if (serverBehaviorInstance.NoTransfer) {
                    continue;
                }

                instance = serverBehaviorInstance.GetClientBehaviorInstance();
            }

            // Check to see if there is already a behavior of this type in the list.
            // If there is, replace it.
            var existing = gameObj.m_inactiveBehaviors
                .Where(x => x is not null)
                .FirstOrDefault(x => x.GetType() == instance.GetType());
            if (existing != null) {
                var idx = gameObj.m_inactiveBehaviors.IndexOf(existing);
                gameObj.m_inactiveBehaviors[idx] = instance;
            }
            else {
                // This will cause the client to crash if the behavior does not exist in the template.
                Logger.Fatal("{0} contains behavior {1} that does not exist in the template.",
                    Logger.Args(ActiveGameObject.m_debugName, behaviorInstance.GetType().Name));
            }
        }

        return gameObj;
    }
}
