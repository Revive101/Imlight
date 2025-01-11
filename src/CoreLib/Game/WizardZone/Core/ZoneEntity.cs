/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.WizardZone.Components;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.WizardZone.Core;

/// <summary>
/// Base entity class for all zone objects. Uses a component-based architecture
/// to handle different behaviors and functionality.
/// </summary>
public class ZoneEntity : ReceiveProtocolDispatcher {

    public CoreObject ActiveGameObject { get; private set; }
    public CoreTemplate Template { get; private set; }
    public float InteractionRadius { get; protected set; } = 300f;
    public Zone Zone { get; private set; }
    protected IActorRef SupervisorRef;
    protected IActorRef WizardZoneRef;

    private readonly Dictionary<Type, IZoneComponent> _components = [];

    // ctor
    public ZoneEntity(CoreObject activeGameObject, CoreTemplate template, IActorRef zoneRef, Zone zone) {
        ActiveGameObject = activeGameObject;
        Template = template;
        SupervisorRef = Context.Parent;
        WizardZoneRef = zoneRef;
        Zone = zone;

        AutoAttachComponents();
    }

    /// <summary>
    /// Gets a component of the specified type.
    /// </summary>
    public bool TryGetComponent<T>(out T component) where T : IZoneComponent {
        if (_components.TryGetValue(typeof(T), out var baseComponent)) {
            component = (T) baseComponent;

            return true;
        }
        component = default;

        return false;
    }

    #region Message Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    protected virtual void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        // Notify all components of the player's arrival.
        foreach (var component in _components.Values) {
            component.OnPlayerJoin(message.PlayerObject, message.Player, message.Wizard);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    protected virtual void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message) {
        // Notify all components of the player's departure.
        foreach (var component in _components.Values) {
            component.OnPlayerLeave(message.Player, message.GlobalId);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERMOVE))]
    protected virtual void ReceivePlayerMove(ZONE_102_PROTOCOL.MSG_PLAYERMOVE message) {
        if (message.CoreObject == null) {
            return;
        }

        // Notify all components of the player's movement.
        foreach (var component in _components.Values) {
            component.OnPlayerMove(message.CoreObject, message.PlayerActor);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    protected virtual void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message)
        => WizardZoneRef.Tell(message);

    #endregion

    /// <summary>
    /// Broadcasts a message to the zone.
    /// </summary>
    /// <param name="message"> The message to broadcast. </param>
    /// <param name="selfless"> Whether or not the message should be sent to the sender. </param>
    protected void BroadcastToZone(IServerMessage message, bool selfless = true) {
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = (Common.MessageLayer.IMessage) message,
            Selfless = selfless,
            Sender = Self
        };
        WizardZoneRef.Tell(broadcastMsg);
    }

    private void AutoAttachComponents() {
        foreach (var componentType in ComponentRegistry.GetRegisteredComponents()) {
            // Create instance and check if it should be attached.
            var component = (IComponentFactory) Activator.CreateInstance(componentType);
            if (component.ShouldAttachToEntity(Template)) {
                AddComponent((IZoneComponent) component);
            }
        }
    }

    private void AddComponent(IZoneComponent component) {
        component.Initialize(this);
        _components[component.GetType()] = component;
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
        foreach (var component in _components.Values) {
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