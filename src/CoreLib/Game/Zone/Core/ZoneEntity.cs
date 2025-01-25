/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

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

    private const uint MOBILE_ID_REQUEST_TIMEOUT_IN_MS = 1000;

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

    protected readonly Dictionary<BaseZoneComponent, IActorRef> Components = [];

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

    #region Message Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    protected virtual void ReceiveObjectLoadBegin() {
        if (ActiveGameObject is not null) {
            MobileID = GetMobileIDFromZone();
        }

        AutoAttachComponents();
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
            actor.Tell(message);
        }
    }

    #endregion

    /// <summary>
    /// Automatically attaches components to this entity based on the template.
    /// </summary>
    protected virtual void AutoAttachComponents() {
        var template = Template;

        foreach (var (componentType, shouldAttachMethod) in ComponentRegistry.GetRegisteredComponents()) {
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
    protected void AddComponent(Type type) {
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
            if (!char.IsLetterOrDigit(c) && "-_.*$+:@&=,!~';()".IndexOf(c) == -1) {
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
            m_characterId = (Common.ObjectProperty.PropertyReflection.GID) ActiveGameObject.m_globalID,
        };

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
                else {
                    // This will cause the client to crash if the behavior does not exist in the template.
                    Logger.Fatal("{0} contains behavior {1} that does not exist in the template.",
                        Logger.Args(gameObj.m_debugName, clientInstance.GetType().Name));
                }
            }
        }

        return gameObj;
    }

}