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

    public CoreObject ActiveGameObject { get; private set; } = activeGameObject;
    public CoreTemplate Template { get; private set; } = template;
    public Zone Zone { get; private set; } = zone;
    public IActorRef SupervisorRef { get; private set; } = Context.Parent;
    public IActorRef ZoneRef { get; private set; } = zoneRef;
    public bool NoTransfer { get; set; } = false;

    protected readonly List<IActorRef> Components = [];

    #region Message Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    protected virtual void ReceiveObjectLoadBegin() {
        AutoAttachComponents();
        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS());
    }

    [MessageHandler(typeof(IServerMessage))]
    private void ReceiveElse(IServerMessage message) {
        if (message is ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN) {
            return;
        }

        foreach (var component in Components) {
            component.Tell(message);
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
        var component = Context.ActorOf(props, type.Name);
        Components.Add(component);
    }

    public WizClientObject GetClientBehaviorInstance() {
        var clientObj = new WizClientObject {
            m_debugName = ActiveGameObject.m_debugName,
            m_globalID = ActiveGameObject.m_globalID,
            m_location = ActiveGameObject.m_location,
            m_nMobileID = ActiveGameObject.m_nMobileID,
            m_orientation = ActiveGameObject.m_orientation,
            m_permID = ActiveGameObject.m_permID,
            m_templateID = ActiveGameObject.m_templateID,
            m_zoneTagID = ActiveGameObject.m_zoneTagID,
            m_inactiveBehaviors = []
        };

        // Let each component contribute its behaviors.
        foreach (var component in Components) {
            if (component is IClientBehaviorProvider<BehaviorInstance> serverBehavior) {
                if (serverBehavior.NoTransfer) {
                    continue;
                }

                var clientInstance = serverBehavior.GetClientBehaviorInstance();

                // Check to see if there is already a behavior of this type in the list.
                // If there is, replace it.
                var existing = ActiveGameObject.m_inactiveBehaviors
                    .Where(x => x is not null)
                    .FirstOrDefault(x => x.GetType() == clientInstance.GetType());
                if (existing != null) {
                    var idx = ActiveGameObject.m_inactiveBehaviors.IndexOf(existing);
                    ActiveGameObject.m_inactiveBehaviors[idx] = clientInstance;
                }
                else {
                    // This will cause the client to crash if the behavior does not exist in the template.
                    Logger.Fatal("{0} contains behavior {1} that does not exist in the template.",
                        Logger.Args(ActiveGameObject.m_debugName, clientInstance.GetType().Name));
                }
            }
        }

        return clientObj;
    }

}