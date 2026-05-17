# Player Architecture

When a player connects to Imlight, their presence is represented across three distinct components, each serving a different purpose.

## The Three Components

### Wizard

The **Wizard** is the persistent data representation of a player character. This object contains all of the durable state and behaviors relating to a character, including:
* [ServerMagicSchoolBehavior](https://github.com/Revive101/Imlight/blob/quality-assurance/src/Imlight.CoreLib/Shared/Behaviors/ServerMagicSchoolBehavior.cs): Their level and experience points 
* [ServerWizEquipmentBehavior](https://github.com/Revive101/Imlight/blob/quality-assurance/src/Imlight.CoreLib/Shared/Behaviors/ServerWizEquipmentBehavior.cs): Their equipped items
* [ServerWizInventoryBehavior](https://github.com/Revive101/Imlight/blob/quality-assurance/src/Imlight.CoreLib/Shared/Behaviors/ServerWizInventoryBehavior.cs): Their inventory items
* [ServerWizGameStats](https://github.com/Revive101/Imlight/blob/quality-assurance/src/Imlight.CoreLib/Shared/Behaviors/ServerWizGameStats.cs): Their health, mana, gold, and other game stats

Among other things.

It's worthwhile to understand that behaviors are modular components that may not be unique to the `Wizard` object. For example, the `ServerWizEquipmentBehavior` is also used by zone creatures. That being said, behaviors *should not make database actions*. They should only represent state and behavior.

The `Wizard` class is responsible for database actions. It loads and saves the behaviors it contains, and it is the only object that should be doing so.

::: tip
Think of the `Wizard` as the "save file" for a character.
:::

#### Explaining Persistence

As mentioned, both player characters (wizards) and zone creatures use the `ServerWizEquipmentBehavior`. The reason we don't want database actions within the behavior itself is because of this mutual use.

The difference lies in how the data is sourced:
* For wizards, the data is loaded from and saved to the database
* For zone creatures, the data is loaded from a template 

The behavior must be agnostic to the source of its data. It simply represents the state and behavior of equipment, regardless of whether that equipment belongs to a player character or a zone creature. By centralizing database actions in the `Wizard` class, we ensure that only player characters interact with the database.

### Session Actor

The **Session Actor** is a transient object that represents a player's connection to the server. It is created when a player connects and destroyed when they disconnect. The Session Actor is responsible for managing the player's network connection, handling incoming messages, and sending outgoing messages.

See the [Session Actor article](/imlight/concepts/sessionactor) for more information.

### Game Object

The **Game Object** is a transient object that represents the player's presence in the game world. It is created when a player enters a zone and destroyed when they leave. The Game Object is responsible for managing the player's position, movement, and interactions with other objects in the game world.

## Lifecycle and Relationships

### Connection Flow

When a player connects to Imlight, these components come into existence at different stages:

1. TCP Connection → Session Actor is created
2. Authentication → Session Actor links to an Account
3. Character Selection → Session Actor may load Wizard data for preview
4. Enter World → Session Actor activates Wizard and creates Game Object
5. Disconnect → Game Object destroyed, Wizard saved, Session Actor terminated