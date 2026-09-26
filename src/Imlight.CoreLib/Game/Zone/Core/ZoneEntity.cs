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
 * ENTITY COMPONENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides a component-based architecture for all zone objects, enabling modular
 * behavior composition and extensibility for game entities.
 * 
 * USAGE EXAMPLE:
 * // Entities are typically created through the zone system
 * // To access: zoneRef.Ask<ZONE_102_PROTOCOL.MSG_QUERYZONEENTITYRSP>(queryMsg)
 * var component = entity.GetComponentOfType<PathMovementComponent>();
 * 
 * NOTE:
 * Each entity is one actor. Its components are plain objects it hosts: they are created, awoken
 * and started synchronously on load, and every message for them runs on this actor's thread.
 * Components are attached automatically based on entity templates.
 * Mobile IDs come straight from the zone's reserved range.
 *
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.IO;
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
    CoreObjectInfo info,
    IActorRef zoneRef,
    Zone zone) : ReceiveProtocolDispatcher, IClientBehaviorProvider<WizClientObject>, IWithTimers {

    public ITimerScheduler Timers { get; set; }
    public IActorRef SelfRef { get; protected set; } 
    public CoreObject ActiveGameObject { get; protected set; } = activeGameObject;
    public CoreTemplate Template { get; protected set; } = template;
    public CoreObjectInfo Info { get; protected set; } = info;
    public Zone Zone { get; protected set; } = zone;
    public IActorRef SupervisorRef { get; protected set; } = Context.Parent;

    public virtual bool IsCombatOnlyMinion => false;
    public IActorRef ZoneRef { get; protected set; } = zoneRef;
    public bool NoTransfer { get; set; } = false;
    public ushort MobileID { 
        get => ActiveGameObject.m_nMobileID; 
        private set => ActiveGameObject.m_nMobileID = value; 
    }

    protected readonly Dictionary<ZoneEntityComponent, IActorRef> Components = [];
    private readonly List<ZoneEntityComponent> _componentOrder = [];

    internal IActorRef CurrentSender => Sender;

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

        var serializer = new ObjectSerializer(
            Behaviors: SerializerFlags.None
        );
        if (!serializer.Serialize(despawnEffects, 1, out var serializedData)) {
            Logger.Error("Failed to serialize despawn");
            Context.Stop(Self);

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
            Versionable: false,
            Behaviors: SerializerFlags.None
        );

        ByteString emoteData = new ByteString();
        if (emoteStateOverrideInfo is not null && !serializer.Serialize(emoteStateOverrideInfo, 1, out emoteData)) {
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

    /// <summary>
    /// Changes the state of the entity, sending the message exclusively to a specific sender.
    /// </summary>
    /// <param name="stateName">The name of the state to change to.</param>
    /// <param name="sender">The actor reference of the sender to send the message to.</param>
    /// <param name="emoteStateOverrideInfo">The override information for the emote state.</param>
    /// <param name="ignoreIfCurrentStateIsOff">Whether to ignore the state change if the current state is off.</param>
    public void ChangeStateExclusiveSender(string stateName,
                                           IActorRef sender,
                                           EmoteStateOverrideInfo emoteStateOverrideInfo = null,
                                           bool ignoreIfCurrentStateIsOff = false) {
        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );
        
        ByteString emoteData = new ByteString();
        if (emoteStateOverrideInfo is not null && !serializer.Serialize(emoteStateOverrideInfo, 1, out emoteData)) {
            Logger.Error("Failed to serialize emote state override info");

            return;
        }

        var stateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = ActiveGameObject.m_globalID,
            State = StringHash.Compute(stateName),
            Data = emoteData,
            IgnoreIfCurrentStateIsOff = (byte) (ignoreIfCurrentStateIsOff ? 1 : 0),
        };

        sender.Tell(stateMsg);
    }

    #region Message Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    protected virtual void ReceiveObjectLoadBegin() {
        this.SelfRef = Self;

        if (ActiveGameObject is not null) {
            MobileID = ReserveMobileId();
        }

        AutoAttachComponents();

        // Two passes: every component is awoken (OnAwake) before any is started (OnStart), so OnStart
        // may rely on every other component being configured.
        foreach (var component in _componentOrder) {
            RunLifecycleStep(component);
        }

        foreach (var component in _componentOrder) {
            RunLifecycleStep(component);
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

        foreach (var component in _componentOrder) {
            DispatchToComponent(component, message);
        }
    }

    [MessageHandler(typeof(ComponentMessage))]
    private protected void ReceiveComponentMessage(ComponentMessage envelope)
        => DispatchToComponent(envelope.Component, envelope.Message);

    #endregion

    /// <summary>
    /// Automatically attaches components to this entity based on the template.
    /// </summary>
    protected virtual void AutoAttachComponents() {
        var template = Template;

        foreach (var (componentType, shouldAttachToEntity) in ZoneEntityComponentRegistry.GetRegisteredComponents()) {
            // One component's ShouldAttachToEntity (or constructor) throwing must never abort the rest of
            // this entity's components or wedge the zone load. Activator wraps a constructor's error in a
            // TargetInvocationException; log the inner message and skip only that one component.
            try {
                if (shouldAttachToEntity(template)) {
                    AddComponent(componentType);
                }
            } catch (Exception ex) {
                Logger.Warning("Component {0} threw while attaching to a '{1}' entity, skipping it: {2}",
                    Logger.Args(componentType.Name, template?.GetType().Name ?? "?", (ex.InnerException ?? ex).Message));
            }
        }
    }

    /// <summary>
    /// Spawns a creature entity actor as a child of this entity, used for combat minions. Mirrors
    /// ZonePath.CreateEntityActor's load handshake; returns null on init failure.
    /// </summary>
    public IActorRef SpawnCombatMinionActor(CoreObject coreObject, CoreTemplate template) {
        var actorName = $"Minion_{coreObject.m_globalID.Full}";
        if (!IsValidActorName(actorName)) {
            actorName = $"Minion_{Guid.NewGuid():N}";
        }

        // CombatMinionEntity suppresses PathMovement and the distance-cull.
        var minionActor = Context.ActorOf(
            Props.Create(() => new CombatMinionEntity(coreObject, template, null, ZoneRef, Zone)), actorName);
        try {
            var timeout = TimeSpan.FromMilliseconds(5000);
            _ = minionActor.Ask<ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS>(
                new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN(), timeout).Result;
        }
        catch (Exception ex) {
            Logger.Error("Failed to spawn combat minion actor ({0}).", Logger.Args(ex.Message));
            minionActor.Tell(PoisonPill.Instance);

            return null;
        }

        return minionActor;
    }

    /// <summary>
    /// Adds a component to this entity.
    /// </summary>
    /// <param name="type">The type of the component to add.</param>
    protected void AddComponent(System.Type type) {
        var component = (ZoneEntityComponent) Activator.CreateInstance(type, this);
        var componentRef = new ComponentActorRef((IInternalActorRef) Self, component);
        component.AttachTo(componentRef);
        if (component is IWithTimers withTimers) {
            withTimers.Timers = new ComponentTimerScheduler(Timers, component);
        }

        Components.Add(component, componentRef);
        _componentOrder.Add(component);
    }

    private void RunLifecycleStep(ZoneEntityComponent component) {
        try {
            component.RunLifecycleStep();
        } catch (Exception ex) {
            Logger.Error("Component {Component} of {Entity} failed to initialize: {Exception}",
                Logger.Args(component.GetType().Name, DescribeForLog(), ex));
        }
    }

    private void DispatchToComponent(ZoneEntityComponent component, object message) {
        var handler = MessageHandlerTable.DispatcherFor(component.GetType(), message.GetType());
        if (handler is null) {
            return;
        }

        // A failing handler must not take the entity and its other components down with it.
        try {
            handler(component, message);
        } catch (Exception ex) {
            Logger.Error("Component {Component} of {Entity} failed on {Message}: {Exception}",
                Logger.Args(component.GetType().Name, DescribeForLog(), message.GetType().Name, ex));
        }
    }

    private string DescribeForLog()
        => ActiveGameObject?.m_debugName?.ToString() ?? GetType().Name;

    private static bool IsValidActorName(string name) {
        foreach (char c in name) {
            if (!char.IsLetterOrDigit(c) && !"-_.*$+:@&=,!~';()".Contains(c)) {
                return false;
            }
        }
        return true;
    }

    private ushort ReserveMobileId() {
        try {
            return Zone.ReserveMobileId();
        } catch (Exception e) {
            Logger.Error("Failed to get mobile ID from zone: {Reason}", Logger.Args(e.Message));

            return 0;
        }
    }

    public WizClientObject GetClientBehaviorInstance() {
        var gameObj = BuildClientObject<WizClientObject>();

        // This one must be done manually.
        var statsComponent = GetComponentOfType<StatsComponent>();
        if (statsComponent is not null) {
            gameObj.m_gameStats = statsComponent.Stats.GetCombatGameStats();
        }

        return gameObj;
    }

    /// <summary>
    /// Gets the object sent to clients in MSG_NEWOBJECT. Its class follows the entity's own object, because
    /// the client builds the object from the template and reads that class's properties from the payload.
    /// </summary>
    public ClientObject GetClientObject() => ActiveGameObject switch {
        ClientReagentItem => BuildClientObject<ClientReagentItem>(),
        ClientPetSnackItem => BuildClientObject<ClientPetSnackItem>(),
        WizClientObjectItem => BuildClientObject<WizClientObjectItem>(),
        _ => GetClientBehaviorInstance(),
    };

    private T BuildClientObject<T>() where T : ClientObject, new() {
        var gameObj = new T() {
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

        return gameObj;
    }

}