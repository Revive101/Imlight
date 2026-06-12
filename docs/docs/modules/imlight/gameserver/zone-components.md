# Zone Components

## Overview

As mentioned in the previous section on [Zone Architecture](./zone-architecture.md), zones in Imlight use an entity-component system where `ZoneEntity` instances have modular behaviors attached through components.

Components are specialized classes that add specific behaviors to entities. They are attached to `ZoneEntity` instances based on criteria defined in the component class itself.

## Service Memento Base

A "service memento base" will always be attached to a `ZoneEntity` so long as it has a physical game object. However it will not compute anything unless there is at least one `IServiceComponent` attached to the entity.

Service components implement the `IServiceComponent` interface, allowing them to provide interactive services to players.

Service components usually create "WizBangs" over the entity's head to indicate interactivity. The service memento will automatically prioritize and manage these wizbangs based on the attached service components.

When a player interacts with the entity, the service memento delegates the interaction to the appropriate service component.

## Creating New Components

Creating a new component is as simple as creating a new class that extends `ZoneEntityComponent`. 

As a practical example, we'll recreate the `WorldTeleportDoorComponent`, which manages world teleportation interactions for universe map doors. 

```csharp
/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * WORLD TELEPORT DOOR
 * ========================================================================
 * 
 * PURPOSE:
 * Manages world teleportation interactions for universe map doors. 
 * 
 * Created by: Jeff
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class WorldTeleportDoorComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const uint WORLD_DOOR_TEMPLATE_ID = 84113;

    // All of these are required by IServiceComponent.
    // The service memento will automatically determine priorities:
    // they only need to be defined here, for this specific service.
    public string ServiceName     => "UniverseMapService";
    public string NpcIcon         => "GUI/Buttons/Button_Spiral.dds";
    public string NpcNameKey      => "WizardGameObjects_00000070";
    public string NpcTextKey      => "GUI_ObjectInteract";
    public WizBangs WizBang       => WizBangs.None;
    public string StateName       => "UniverseTeleport";
    public string InteractWizBang => "Registrar";
    public string DisplayKey      => "GUI_UniverseMap";

    // Determines if this component should be attached to the given entity.
    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate goTemplate 
        && goTemplate.m_templateID == WORLD_DOOR_TEMPLATE_ID;

    // Also required by IComponentFactory. When the player approaches the entity,
    // the service memento will call this to determine what dialog to show the player.
    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _) 
        => [
            new UniverseMapOption() {
                m_displayKey = DisplayKey,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
            }
        ];

    // This override is provided by IServiceComponent. It is called when a player interacts with the entity.
    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        // For the sake of this example, we won't actually
        // define these functions

        // Show the player the world selection UI.
        SendWorldTeleportOptions(playerActor);

        // Transition the player to have the appropriate icon
        // above their head.
        SendPlayerIntoWizbang(playerObject.m_globalID);
    }

}
```

## Component Methods

Components define a number of helpful methods to manage their lifecycle and interactions:

### `OnAwake()`
Called by the `ZoneEntity` after all components have been initialized, but before `OnStart()`.

```csharp
public override void OnAwake() {
    // Called when the component is awakened
}
```

### `OnStart()`
Invoked after `OnAwake()`, signaling that the component is ready to begin its behavior.

```csharp
public override void OnStart() {
    // Called when the component starts
}
```

### `OnZoneStart()`
Called once the `Zone` has completed its initialization.

```csharp
public override void OnZoneStart() {
    // Called when the zone starts
}
```

### `OnPlayerJoin(playerObj, playerActor, playerWizard)`
Called when a player joins the zone.

```csharp
public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
    // Called when a player joins the zone
}
```

### `OnPlayerLeave(playerActor, id)`
Called when a player leaves the zone.

```csharp
public override void OnPlayerLeave(IActorRef playerActor, ulong id) {
    // Called when a player leaves the zone
}
```

### `OnPlayerMove(playerObj, playerActor, playerWizard)`
Called when a player moves within the zone.

```csharp
public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
    // Called when a player moves within the zone
}
```

### `OnCreatureMove(creature, suspect, entity)`
Called when a creature moves within the zone.

```csharp
public override void OnCreatureMove(CoreObject creature, IActorRef suspect, ZoneEntity entity) {
    // Called when a creature moves within the zone
}
```

### `Enable()`
Enables the component.

```csharp
public void TurnBackOn() {
    // Do some initialization work here ...

    Enable();
}
```

### `Disable()`
Disables the component.

```csharp
public void TurnBackOff() {
    // Do some cleanup work here ...

    Disable();
}
```

### `OnEnabled()`
Called when the component is enabled.

```csharp
public override void OnEnabled() {
    // Called when the component is enabled
}
```

### `OnDisabled()`
Called when the component is disabled.

```csharp
public override void OnDisabled() {
    // Called when the component is disabled
}
```