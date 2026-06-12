# Zone Architecture

## Overview

Zones in Imlight represent discrete areas of the game world, each managed by a dedicated `Zone` actor. The zone system uses an entity-component architecture where ZoneEntities (NPCs, objects, interactive elements) have modular behaviors attached through [components](./zone-components.md), all coordinated by specialized supervisor actors.

Every `Zone` actor exists as a child actor of the [GameWorld](./gameserver.md#game-world) actor.

## Zone Loading

As mentioned in the previous section on [Shared Data Systems](../concepts/shared-data-systems.md), zones load their configuration from shared archives.

Zones are loaded (or downloaded) on-demand when players enter them. Each zone corresponds to a specific path (e.g., "WorldName/ZoneName") and is managed by a `Zone` actor instance.

The `Zone` actor does not load itself. Instead, it delegates loading to a `ZoneLoader` actor, which retrieves zone data from shared archives and initializes the zone's entities and supervisors.

Once the `ZoneLoader` completes loading, it will return a `MSG_ZONELOADRESULTS` message to the `Zone` actor, which then creates the necessary supervisors and entities based on the loaded data. This same message is forwarded to each zone entity supervisor to finalize their setup.

### Zone Hierarchy

```
Zone Actor
├── ZoneLoader (loads zone data from archives)
├── ZoneObjectSupervisor (manages NPCs, interactive objects)
├── ZonePlayerSupervisor (tracks player movement and presence)
├── ZoneTriggerSupervisor (handles quest triggers, events)
├── ZoneVolumeSupervisor (manages area-based effects)
├── ZonePathSupervisor (controls movement paths)
└── ZoneSigilSupervisor (teleportation points)
```

## Entity System

### ZoneEntity

The `ZoneEntity` class is the base for all in-zone objects. It should be made clear that `ZoneEntity` is not a physical game object itself but a container for components that define its behavior.

`ZoneEntity` provides a number of helpful methods to manage it's active game object, if one is present. The majority of an entity's functionality is provided by [components](./zone-components.md), which can be added or removed dynamically.

### ZoneTrigger

`ZoneTrigger` is a unique derivative of `ZoneEntity` that does not have a physical representation in the game world. Instead, it is a container that defines an event. Events include zone transfers, cinematics, music changes, etc.

### ZonePath

`ZonePath` is another derivative of `ZoneEntity` that represents a series of waypoints. `ZoneEntity` instances are then instantiated along these paths to create NPC patrols or other movement patterns.

Any `ZoneEntity` that follows a path exists as a child of a `ZonePath` entity. This not only includes mobs, but also reagents such as wooden chests, that spawn randomly along a path but don't move. Anything that can exist along a path is what Imlight denotes as a "creature."

In the event a mob has been defeated in battle, or a reagent has been looted, the entity must be uniquely marked as destroyed in order to respawn on the path. If the message `MSG_DELETEOBJECT` is broadcasted to the zone, the `ZonePath` is already prepared to handle this and will mark the entity as destroyed in its internal state.

::: tip
Remember, a `ZoneEntity` that exists on a `ZonePath` is a child of that path and referred to as a "creature," even if it is not a living thing.
:::

## Supervisor Pattern

Supervisors are specialized actors that manage specific aspects of a zone:

### ZoneObjectSupervisor

Manages all zone entities like objects, NPCs, positionals, and particles. 

### ZonePlayerSupervisor

Tracks players within the zone, handling their entry, exit, and broadcasts.

::: tip
This supervisor does NOT manage [session actors](../concepts/sessionactor.md). Themselves. They only contain a reference to the session actor for each player in the zone.
:::

### Other Supervisors

* **ZoneTriggerSupervisor** - Quest events, scripted interactions
* **ZoneVolumeSupervisor** - Defines [areas](#zone-volumes) in zones that invoke events ([ZoneTrigger](#zonetrigger))
* **ZonePathSupervisor** - Movement paths
* **ZoneSigilSupervisor** - Combat sigils

## Zone Volumes

Zone volumes are defined areas within a zone that can trigger events when players enter or exit them. The most common example of a zone volume is a zone transfer area. The volume does not do anything on its own other than manage its interaction radius and posting enter/exit events to the `ZoneTriggerSupervisor`.

The [zone trigger](#zonetrigger) is what actually defines the event that occurs when a player enters or exits the volume.

## Zone Instances

In the server message `ZONE_102_PROTOCOL.MSG_ZONETRANSFER` is a property `IsPrivate`. If this is true, the `GameWorld` actor will do two things:
1. Create a new `InstanceContainer` actor
2. Place the requested zone into the instance container

Rather than creating a single, private zone, an instance container can hold multiple zones. This is useful for dungeons that have multiple floors or areas.