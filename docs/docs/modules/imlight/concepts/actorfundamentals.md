# Actor Fundamentals

## Overview

Imlight is built on the **Actor Model**, a model that defines single units of computation called "actors."

An **actor** is an isolated, stateful component that:
* Process messages one at a time from its mailbox
* Maintains its own private state
* Can create child actors
* Communicates only through messages in lieu of calling methods/functions

## Actor System

The **Actor System** is the top-level container for all actors in Imlight. It manages the lifecycle of actors, including their creation, supervision, and termination. Actors cannot exist on their own: they must be part of an Actor System, and as a child of another actor.

## Actor Supervisors

Since actors are isolated and do not share state, they can fail independently. To handle failures gracefully, Imlight employs a supervision strategy where parent actors (supervisors) monitor their child actors. If a child actor fails, the supervisor can decide to restart it, stop it, or escalate the failure.

## Actor Hierarchy in Imlight

Imlight consists of three main server types:
* **Login Server**: Handles user authentication, character selection, and initial connection to game servers.
* **Game Server**: Manages game worlds, zones, player interactions, NPCs, and game logic.
* **Patch Server**: Serves game patches and updates to clients.

::: info
The login server is the supervisor of all game servers. When a game server starts, it registers itself with the login server to make itself available for client connections.
:::

The three server types define the same basic actor hierarchy:
* **TcpListenerActor**: Listens for incoming TCP connections.
* **MessageServices**: Handle specific functionalities (e.g., authentication, character management, quests).
* **SocketListener** and **SocketSender**: Manage low-level socket communication.

### Hierarchy Diagram

For the sake of clarity, the aforementioned actors are not shown in the hierarchy below, but they exist in each server type.

<ActorHierarchy />

## Message Handling

In vanilla Akka.NET, actors communicate by sending and receiving untyped messages. Imlight extends this concept by implementing a strongly-typed message handling system that supports custom protocols and dispatchers.

Please review the [Message Handling](./messagehandling.md) documentation for more details.

## Session Actors

When a new client connects to a server, a [Session Actor](./sessionactor.md) is created to manage the connection.