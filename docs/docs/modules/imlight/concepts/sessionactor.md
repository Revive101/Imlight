# Session Actor

A **Session Actor** is a specialized actor that manages the lifecycle and communication of a single client connection to a server. Each time a client connects, a new Session Actor is instantiated to handle all interactions with that client. This actor exists as a child of the server's main actor.

Session actors are generic and are used in all server types. What separates their logic are the [message services](./messageservices.md) that exist as child actors under them.

## Responsibilities

Session actors are an actor representation of a connected client. Their responsibilites should reflect this. Ideally, you should refrain from any business logic in session actors or their [message services](./messageservices.md).

Instead, session actors should focus on receiving messages from the client, validating them, and routing any actual logic to [system actors](./shared-data-systems.md#factory-system-abstractions).

This applies inwards as well. A session actor is the *only* way to speak to a connected client. 

## Message Services

[Message Services](./messageservices.md) are child actors of Session Actors that handle specific functionalities, such as authentication, character management, and in-game actions. Each message service is responsible for processing messages related to its domain.

::: warning
The `SessionActor` routes client messages only on network receive. It will not route client messages that are sent to it internally. If you send a client message to a session actor, it will immediately be sent over the network to the client.
:::

### Getting Message Services

Message services are gathered after the [session handshake](../../internals/systems/kinp/session.md#session-accept) is completed.

Each server independently defines which message services are created for each session actor in their service factory. You can find this static factory class at the root of each server namespace.