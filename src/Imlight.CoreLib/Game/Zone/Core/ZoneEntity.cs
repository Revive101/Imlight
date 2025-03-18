/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Base entity class for all zone objects. Uses a component-based architecture
/// to handle different behaviors and functionality.
/// </summary>
/// <param name="activeGameObject">The active game object that this entity represents.</param>
/// <param name="template">The template that this entity is based on.</param>
/// <param name="zoneRef">The reference to the zone that this entity is a part of.</param>
/// <param name="zone">The zone that this entity is a part of.</param>
public class ZoneEntity(
    CoreObject activeGameObject,
    CoreTemplate template,
    IActorRef zoneRef,
    Zone zone) : ReceiveProtocolDispatcher, IClientBehaviorProvider<WizClientObject> {

    private const uint MOBILE_ID_REQUEST_TIMEOUT_IN_MS = 2500;

    public IActorRef SelfRef { get; protected set; } 
    public CoreObject ActiveGameObject { get; protected set; } = activeGameObject;
    public CoreTemplate Template { get; protected set; } = template;
    public Zone Zone { get; protected set; } = zone;
    public IActorRef SupervisorRef { get; protected set; } = Context.Parent;
    public IActorRef ZoneRef { get; protected set; } = zoneRef;
    public bool NoTransfer { get; set; } = false;
    public ushort MobileID { 
        get => ActiveGameObject.m_nMobileID; 
        private set => ActiveGameObject.m_nMobileID = value; 
    }

    protected readonly Dictionary<ZoneEntityComponent, IActorRef> Components = [];

    /// <summary>
    /// Gets a list of components of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the components to get.</typeparam>
    /// <returns>A list of actor references of the components of the specified type.</returns>
    public List<T> GetComponentsOfType<T>()
        => [.. Components.Keys.Where(x => typeof(T).IsAssignableFrom(x.GetType())).Cast<T>()];

    /// <summary>
    /// Gets a component of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the component to get.</typeparam>
    /// <returns>The actor reference of the component of the specified type, or null if it does not exist.</returns>
    public T GetComponentOfType<T>() where T : class
        => Components.Keys.FirstOrDefault(x => typeof(T).IsAssignableFrom(x.GetType())) as T;

    /// <summary>
    /// Deletes this entity from the zone.
    /// </summary>
    /// <param name="killer">The global ID of the entity that killed this entity.</param>
    /// <param name="despawnEffect">The name of the despawn effect to play.</param>
    public void DeleteObject(string effectName = "", ulong killer = 0) {
        var despawnEffects = new DespawnInfo {
            m_killer = (GID) killer,
            m_despawnEffect = StringHash.Compute(effectName),
        };

        // Send kill-pill to all components.
        foreach (var (_, actor) in Components) {
            actor.Tell(PoisonPill.Instance);
        }
        
        var serializer = new ObjectSerializer(
            Behaviors: SerializerFlags.None
        );
        if (!serializer.Serialize(despawnEffects, 1, out var serializedData)) {
            Logger.Error("Failed to serialize despawn");

            return;
        }

        var despawnMsg = new GAME_5_PROTOCOL.MSG_DELETEOBJECT {
            GameObjectID = ActiveGameObject.m_globalID,
            Data = serializedData,
        };

        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = despawnMsg,
            Sender = SelfRef,
        };
        ZoneRef.Tell(broadcastMsg);

        // Kill the actor.
        Context.Stop(Self);
    }

    /// <summary>
    /// Despawns this entity from the zone, without destroying it.
    /// </summary>
    public void DespawnObject() {
        var removeMsg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = ActiveGameObject.m_globalID
        };

        ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = removeMsg,
            Selfless = false
        });
    }

    /// <summary>
    /// Changes the state of the entity.
    /// </summary>
    /// <param name="stateName">The name of the state to change to.</param>
    /// <param name="emoteStateOverrideInfo">The override information for the emote state.</param>
    /// <param name="ignoreIfCurrentStateIsOff">Whether to ignore the state change if the current state is off.</param>
    public void ChangeState(string stateName, EmoteStateOverrideInfo emoteStateOverrideInfo = null, bool ignoreIfCurrentStateIsOff = false) {
        var stateHash = StringHash.Compute(stateName);
        ChangeState(stateHash, emoteStateOverrideInfo, ignoreIfCurrentStateIsOff);
    }

    /// <summary>
    /// Changes the state of the entity.
    /// </summary>
    /// <param name="stateHash">The hash of the state to change to.</param>
    /// <param name="emoteStateOverrideInfo">The override information for the emote state.</param>
    /// <param name="ignoreIfCurrentStateIsOff">Whether to ignore the state change if the current state is off.</param>
    public void ChangeState(uint stateHash, EmoteStateOverrideInfo emoteStateOverrideInfo = null, bool ignoreIfCurrentStateIsOff = false) {
        var serializer = new ObjectSerializer(
            Behaviors: SerializerFlags.None
        );
        if (!serializer.Serialize(emoteStateOverrideInfo, 1, out var emoteData)) {
            Logger.Error("Failed to serialize emote state override info");

            return;
        }

        var stateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = ActiveGameObject.m_globalID,
            State = stateHash,
            Data = emoteData,
            IgnoreIfCurrentStateIsOff = (byte) (ignoreIfCurrentStateIsOff ? 1 : 0),
        };

        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = stateMsg,
            Selfless = false
        };
        ZoneRef.Tell(broadcastMsg);
    }

    #region Message Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    protected virtual void ReceiveObjectLoadBegin() {
        if (ActiveGameObject is not null) {
            MobileID = GetMobileIDFromZone();
        }

        AutoAttachComponents();

        this.SelfRef = Self;

        // Send two start messages here: OnAwake and OnStart.
        // OnAwake is early initialization. It's meant to configure dependent components that are guaranteed to be present.
        // OnStart is late initialization. 
        // We're also asking instead of telling, to ensure that all components receive `OnAwake` before `OnStart`.

        // Notify each component that the entity has been initialized (OnAwake).
        var initializedMsg = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTINITIALIZED();
        foreach (var (_, actor) in Components) {
            var _ = actor.Ask(initializedMsg).Result;
        }

        // Send a message to the zone to indicate that the entity has been loaded (OnStart).
        foreach (var (_, actor) in Components) {
            var _ = actor.Ask(initializedMsg).Result;
        }

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS());
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY))]
    protected virtual void ReceiveQueryEntityObject(ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY message) {
        if (ActiveGameObject is null) {
            return;
        }

        if (ActiveGameObject.m_globalID == message.GlobalID || MobileID == message.MobileID) {
            Sender.Tell(new ZONE_102_PROTOCOL.MSG_QUERYZONEENTITYRSP() {
                ZoneObject = this,
                Found = true
            });
        }
    }

    [MessageHandler(typeof(IServerMessage))]
    protected virtual void ReceiveElse(IServerMessage message) {
        if (message is ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN) {
            return;
        }

        foreach (var (_, actor) in Components) {
            actor.Forward(message);
        }
    }

    #endregion

    /// <summary>
    /// Automatically attaches components to this entity based on the template.
    /// </summary>
    protected virtual void AutoAttachComponents() {
        var template = Template;

        foreach (var (componentType, shouldAttachMethod) in ZoneEntityComponentRegistry.GetRegisteredComponents()) {
            var shouldAttach = (bool) shouldAttachMethod.Invoke(null, [template]);
            if (shouldAttach) {
                AddComponent(componentType);
            }
        }
    }

    /// <summary>
    /// Adds a component to this entity.
    /// </summary>
    /// <param name="type">The type of the component to add.</param>
    protected void AddComponent(System.Type type) {
        var props = Props.Create(type, this);
        var componentName = type.Name;

        // Ensure the component name is valid for an actor path.
        if (string.IsNullOrEmpty(componentName) || componentName.StartsWith('$') || !IsValidActorName(componentName)) {
            componentName = $"Component_{Guid.NewGuid()}";
        }

        // Create the component actor and request its identity.
        var componentActor = Context.ActorOf(props, componentName);
        var identityMsg = new ZONE_102_PROTOCOL.MSG_ENTITYCOMPONENTREQUESTIDENTITY();
        var identityRsp = componentActor.Ask<ZONE_102_PROTOCOL.MSG_ENTITYCOMPONENTREQUESTIDENTITYRSP>(identityMsg).Result;

        Components.Add(identityRsp.Component, componentActor);
    }

    private static bool IsValidActorName(string name) {
        foreach (char c in name) {
            if (!char.IsLetterOrDigit(c) && !"-_.*$+:@&=,!~';()".Contains(c)) {
                return false;
            }
        }
        return true;
    }

    private ushort GetMobileIDFromZone() {
        try {
            var requestMsg = new ZONE_102_PROTOCOL.MSG_GETRESERVEDMOBILEID();
            var timeout = TimeSpan.FromMilliseconds(MOBILE_ID_REQUEST_TIMEOUT_IN_MS);
            var requestRsp = ZoneRef.Ask<ZONE_102_PROTOCOL.MSG_GETRESERVEDMOBILEIDRSP>(requestMsg, timeout).Result;

            return requestRsp.MobileID;
        }
        catch (Exception e) {
            Logger.Error("Failed to get mobile ID from zone: {0}", Logger.Args(e.Message));

            return 0;
        }
    }

    public WizClientObject GetClientBehaviorInstance() {
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
            m_fScale = 1,
            m_characterId = ActiveGameObject.m_globalID,
        };

        gameObj = CoreObjectFactory.InitializeCoreObjectBehaviors(gameObj, Template);

        // Let each component contribute its behaviors.
        foreach (var (component, _) in Components) {
            if (component is IClientBehaviorProvider<BehaviorInstance> serverBehavior) {
                if (serverBehavior.NoTransfer) {
                    continue;
                }

                var clientInstance = serverBehavior.GetClientBehaviorInstance();

                // Check to see if there is already a behavior of this type in the list.
                // If there is, replace it.
                var existing = gameObj.m_inactiveBehaviors
                    .Where(x => x is not null)
                    .FirstOrDefault(x => x.GetType() == clientInstance.GetType());
                if (existing != null) {
                    var idx = gameObj.m_inactiveBehaviors.IndexOf(existing);
                    gameObj.m_inactiveBehaviors[idx] = clientInstance;
                }
            }
        }

        // This one must be done manually.
        var statsComponent = GetComponentOfType<StatsComponent>();
        if (statsComponent is not null) {
            gameObj.m_gameStats = statsComponent.Stats.GetCombatGameStats();
        }

        return gameObj;
    }

}