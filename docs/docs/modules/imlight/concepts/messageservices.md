# Message Services

## Overview

The game client and its respective server communicate through a series of message object defined by message records. The game client is notoriously lazy, and the server is responsible for a lot of the heavy lifting. 

The bulk of work to be done in Imlight is by defining handlers for these messages.

To comfortably handle these messages, Imlight provides a concept called **Message Services**. These are actors that exist as child actors under a [Session Actor](./sessionactor.md) and are responsible for handling a specific set of messages.

### Message Services Tree

::: info
This is a non-exhaustive list of message services that merely showcase the concept. 
:::

<MessageServicesTree />

## Defining a Message Service

When creating a new message service, you need to:
1. Determine if you should create a new service, or your feature can be handled by an existing service.
2. Create a new actor class that extends `MessageService`.
3. Register your message service in the server's [service factory](./sessionactor.md#getting-message-services).

That's it. Imlight will automatically route messages to your service based on the message types it handles. It will also manage the lifecycle of your service, starting it when the session starts and stopping it when the session ends.

::: info
Two different services can handle the same message type. In this case, both services will receive the message when it is dispatched. However, a single service cannot handle the same message type twice.
:::

### Example

Lets define a practical example. Say we want to create a message service to add and remove spells from our deck.


```csharp

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Game.Services;

// Define a class that extends MessageService.
internal class SpellbookService(SessionActor sessionActor) : MessageService(sessionActor) {

    // Define a static Props method to create instances of the service.
    // This is just Akka.NET's way of calling the constructor.
    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new SpellbookService(parentActor));

    // Use the MessageHandler attribute to specify which message types this method handles.
    // The method signature must match the message type.
    // The method can be private, as the MessageHandler attribute will still work.
    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK))]
    private void ReceiveAddSpellToDeck(WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK message) {

    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK))]
    private void ReceiveRemoveSpellFromDeck(WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK message) {

    }
    
}
```

Then, we need to register our service in the server's service factory. We'll define it in the game server, since this message is only received while the player is actually moving around in the game world.

```csharp
namespace Imlight.CoreLib.Game;

public class GameServiceFactory : ServiceFactory {

    protected override HashSet<Type> ServiceTypes { get; set; } = [
        typeof(...),
        typeof(...),

        // Register our new service here.
        typeof(SpellbookService),
    ];

    public static Props Props() 
        => Akka.Actor.Props.Create(() => new GameServiceFactory());

}
```

That's it, for now. You have now created handlers for adding and removing spells from the player's deck. You can now implement the logic inside the handler methods to actually add and remove spells from the player's deck.

#### Full Example

```csharp
﻿/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * SPELLBOOK SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player spell deck modifications, including adding and 
 * removing spells from spell decks.
 * 
 * USAGE EXAMPLE:
 * Internal service handling spellbook-related messages within 
 * the game server session.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Game.Services;

internal class SpellbookService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new SpellbookService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK))]
    private void ReceiveAddSpellToDeck(WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK message) {
        // Get the active wizard (player character) associated with this session.
        // Save the result of adding the spell to the deck.
        var wizard = GetActiveWizard();
        var deckAddSuccess = wizard.AddSpellToDeck((uint) message.SpellID, message.DeckID);

        // Return a response to the client indicating success or failure.
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK() {
            SpellID = message.SpellID,
            DeckID = message.DeckID,
            Success = (byte) (deckAddSuccess ? 1 : 0)
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK))]
    private void ReceiveRemoveSpellFromDeck(WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK message) {
        // Get the active wizard (player character) associated with this session.
        // Save the result of removing the spell from the deck.
        var wizard = GetActiveWizard();
        var deckRemoveSuccess = wizard.RemoveSpellFromDeck((uint) message.SpellID, message.DeckID);

        // Return a response to the client indicating success or failure.
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK() {
            SpellID = message.SpellID,
            DeckID = message.DeckID,
            Success = (byte) (deckRemoveSuccess ? 1 : 0)
        });
    }
    
}
```

## Exceptions

Message services must be built with fault tolerance in mind. If a message service crashes, it will not bring down the entire session actor. Instead, the session actor will restart the crashed service and continue functioning normally.

Two exception types exist to help with this:
* `SessionFatalException`: This exception indicates a critical error that requires the entire session to be terminated. When a message service throws this exception, the session actor will stop itself and all its children, effectively ending the session.
* `ServiceRetryException`: This exception indicates a recoverable error that allows the message service to be restarted. When a message service throws this exception, the session actor will restart the service, allowing it to recover from the error and continue processing messages.

::: warning
The Session Actor will only allow so many failures from a specific message service before it gives up and terminates the entire session. This is to prevent infinite restart loops that could lead to resource exhaustion.
:::

## Informing Other Services

### TellOtherServices

Sends a server message to all other message services within the same session actor, excluding itself. This is useful when you want to notify other services about an event without triggering your own handlers.

```csharp
TellOtherServices(IServerMessage message)
```

### TellAllServices

Sends a server message to all message services within the same session actor, including itself. Use this when you want every service, including your own, to process the message.

```csharp
TellAllServices(IServerMessage message)
```

### AskOtherService

Sends a server message to all other message services and awaits a response of type `T` from the first service that responds. This is helpful for querying other services for information or requesting an action.

```csharp
AskOtherService<T>(IServerMessage message) where T : IServerMessage
```

### AskServer

Sends a server message to the server and awaits a response of type `T`. This is useful for querying the server for information or requesting an action.

```csharp
AskServer<T>(IServerMessage message) where T : IServerMessage
```

## Helpful Methods

### `GetActiveWizard()`

Retrieves the active [wizard](./playerarchitecture.md#wizard) (player character) associated with the current session.

```csharp
var wizard = GetActiveWizard();
```

### `GetActiveGameObject()`

Retrieves the active [game object](./playerarchitecture.md#game-object) associated with the current session.

```csharp
var gameObject = GetActiveGameObject();
```

### `GetActiveAccount()`

Retrieves the account associated with the current session.

```csharp
var account = GetActiveAccount();
```

### `CloseSession()`

Closes the current session, effectively logging the player out.

```csharp
CloseSession();
```

### `GetZoneObject()`

Retrieves a zone object by its global ID.

```csharp
var zoneObject = GetZoneObject(ulong globalId);
```

### `Teleport()`

Teleports the active wizard to a specified destination zone and location.

```csharp
Teleport(string destinationZone,
         bool doTeleportEffects = true,
         bool makePrivate = false, // If true, this zone will be instanced
         ulong ownerCharId = 0,
         string destinationLocation = "");
```

### `TryGetOnlinePlayer()`

Attempts to retrieve an online player by their character ID.

```csharp
bool TryGetOnlinePlayer(ulong characterId, out OnlinePlayer onlinePlayer);
```

### `InformGameClient()`

Sends a message to the game client to inform the player about a specific reason, with an option to mark it as important.

```csharp
InformGameClient(string reason, bool isImportant = false);
```