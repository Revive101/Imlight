/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.WizardZone.Components;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.WizardZone.Core;

/// <summary>
/// Represents a component within a zone that handles various player and creature interactions.
/// </summary>
public interface IZoneComponent {

    /// <summary>
    /// Initializes the component with the specified zone entity.
    /// </summary>
    /// <param name="entity">The zone entity to initialize.</param>
    void Initialize(ZoneEntity entity);

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
    /// Called when a player enters the proximity of the zone.
    /// </summary>
    /// <param name="playerObj">The core object representing the player.</param>
    /// <param name="playerActor">The actor reference of the player.</param>
    void OnPlayerProximityEnter(CoreObject playerObj, IActorRef playerActor);

    /// <summary>
    /// Called when a player exits the proximity of the zone.
    /// </summary>
    /// <param name="playerObj">The core object representing the player.</param>
    /// <param name="playerActor">The actor reference of the player.</param>
    void OnPlayerProximityExit(CoreObject playerObj, IActorRef playerActor);

    /// <summary>
    /// Called when a player interacts with an NPC in the zone.
    /// </summary>
    /// <param name="message">The interaction message.</param>
    /// <param name="playerActor">The actor reference of the player.</param>
    void OnPlayerInteraction(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message, IActorRef playerActor);

    /// <summary>
    /// Called when a creature enters the proximity of the zone.
    /// </summary>
    /// <param name="creature">The core object representing the creature.</param>
    /// <param name="suspect">The actor reference of the suspect.</param>
    void OnCreatureProximityEnter(CoreObject creature, IActorRef suspect);
    
}

/// <summary>
/// Base class for components that implements common functionality
/// </summary>
public abstract class BaseZoneComponent : ServerBehaviorInstance, IZoneComponent, IComponentFactory {

    protected ZoneEntity Entity { get; private set; }

    public virtual void Initialize(ZoneEntity entity) 
        => Entity = entity;

    public virtual void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) { }
    public virtual void OnPlayerLeave(IActorRef playerActor, ulong id) { }
    public virtual void OnPlayerProximityEnter(CoreObject playerObj, IActorRef playerActor) { }
    public virtual void OnPlayerProximityExit(CoreObject playerObj, IActorRef playerActor) { }
    public virtual void OnPlayerInteraction(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message, IActorRef playerActor) { }
    public virtual void OnCreatureProximityEnter(CoreObject creature, IActorRef suspect) { }

    public abstract bool ShouldAttachToEntity(CoreTemplate template);

}