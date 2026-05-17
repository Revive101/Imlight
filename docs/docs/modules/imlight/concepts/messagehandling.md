# Message Handling

## Overview

Imlight's message handling system supports custom protocols and dispatchers that route messages between the actors that exist in the system. The system uses **strongly-typed** messages to ensure type safety and clarity in communication.

## Message Types

There are two distinct types of messages that a session actor can process:
* **Client Messages (`IMessage`)**
* **Server Messages (`IServerMessage`)**

Messages are categorized into message protocols. A protocol is a group of related messages that share a common purpose or functionality. Each protocol is identified by a unique service ID.

Client protocols have service IDs ranging from 1 to 99, while server protocols have service IDs greater than 100. Protocols are a class, and their service ID is embedded into their name. For example:

```csharp
ACCOUNT_104_PROTOCOL
```

With this example, we can determine that this protocol has a service ID of 104, making it a server protocol.

### Client Messages

Client messages are generated from the client's XML definitions via [Imcodec](https://github.com/Jooty/Imcodec). Each message corresponds to a specific action or request that the game client can make.

### Server Messages

Server messages are defined by Imlight and follow the exact same structure as client messages. Their service ID will always be > 100.

Server messages differ in that they are never sent over the network, and are used for internal communication only. They are typically used for one [Message Service](./messageservices.md) to communicate with another, or session actor -> server actor communication.

Server messages are defined in the `Imlight.CoreLib.Shared.Packets` namespace.

## Receive Protocol Dispatcher

The `ReceiveProtocolDispatcher` class is an abstraction for the [actor model](./actorfundamentals.md#actor-fundamentals) that allows for message handlers in a class to instead be identified using C# attributes.

For example:
```csharp
[MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_DELETECHARACTER))]
private void ReceiveDeleteCharacter(LOGIN_7_PROTOCOL.MSG_DELETECHARACTER message) {
    // Do work here
}
```