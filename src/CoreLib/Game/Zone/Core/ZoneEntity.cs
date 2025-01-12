/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
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
public sealed class ZoneEntity(
    CoreObject activeGameObject,
    CoreTemplate template,
    IActorRef zoneRef,
    Zone zone) : ReceiveProtocolDispatcher {

    public CoreObject ActiveGameObject { get; private set; } = activeGameObject;
    public CoreTemplate Template { get; private set; } = template;
    public Zone Zone { get; private set; } = zone;
    public IActorRef SupervisorRef { get; private set; } = Context.Parent;
    public IActorRef ZoneRef { get; private set; } = zoneRef;

    private readonly List<IActorRef> _components = [];

    #region Message Handlers

    [MessageHandler(typeof(IServerMessage))]
    private void ReceiveElse(IServerMessage message) {
        if (message is ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN) {
            return;
        }

        foreach (var component in _components) {
            component.Tell(message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    private void ReceiveObjectLoadBegin() {
        AutoAttachComponents();
        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS());
    }

    #endregion

    /// <summary>
    /// Broadcasts a message to the zone.
    /// </summary>
    /// <param name="message"> The message to broadcast. </param>
    /// <param name="selfless"> Whether or not the message should be sent to the sender. </param>
    internal void BroadcastToZone(IServerMessage message, bool selfless = true) {
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = (Common.MessageLayer.IMessage) message,
            Selfless = selfless,
            Sender = Self
        };
        ZoneRef.Tell(broadcastMsg);
    }

    private void AutoAttachComponents() {
        var template = Template;

        foreach (var (componentType, shouldAttachMethod) in ComponentRegistry.GetRegisteredComponents()) {
            var shouldAttach = (bool) shouldAttachMethod.Invoke(null, [template]);
            if (shouldAttach) {
                AddComponent(componentType);
            }
        }
    }

    private void AddComponent(Type type) {
        var props = Props.Create(type, this);
        var component = Context.ActorOf(props, type.Name);
        _components.Add(component);
    }

    public WizClientObject GetClientTypeAlternative() {
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
        foreach (var component in _components) {
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