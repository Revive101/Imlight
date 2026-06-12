# Game Server Discovery & Transitions

## Overview

The login server's **GameServerPool** actor serves as the orchestrator for game server management and player distribution across the Imlight network. It implements sophisticated load balancing algorithms, health monitoring, and player transitions to ensure optimal game server utilization and player experience.

As a child actor of the login server, the GameServerPool maintains the authoritative registry of all available game servers, making critical decisions about where players should be routed based on real-time server metrics and capacity.

When a game client selects a character and is ready to enter the game world, the login server will coordinate with the GameServerPool to determine the best game server for that player.

## GameServerPool Architecture

### Server Registry Management

**Registration Process**

When a game server starts up and wants to join the network:

1. **Registration Request**: Game server sends `MSG_CREATEGAMESERVER` to login server
2. **Validation**: GameServerPool validates server parameters (name, port, limits)
3. **Capacity Check**: Ensures total server count doesn't exceed configured maximum
4. **Port Conflict Check**: Verifies the requested port isn't already in use
5. **Actor Creation**: Creates a new `GameServer` actor reference in the pool
6. **Registration**: Adds server to the active server registry

```csharp
// Game server registration message flow
MSG_CREATEGAMESERVER → LoginServer → GameServerPool → Server Registry
```

::: warning
The GameServerPool enforces strict limits on the number of concurrent game servers to prevent resource exhaustion.
:::

**Server Deregistration**

Game servers are automatically removed from the pool when:
- Health checks fail consistently (timeout-based detection)
- Server actors stop responding to queries
- Explicit shutdown messages are received
- Network connectivity issues prevent communication

## Load Balancing Algorithm

### Server Selection Strategy

The GameServerPool implements a server selection algorithm:
```
1. Query all registered game servers for current status
2. Filter out unresponsive or unhealthy servers
3. Sort servers by player count (ascending order)
4. Select first non-full server for new connections
5. If all servers are full, select random server from available pool
```

### Health Monitoring

**Continuous Health Checks**

The GameServerPool will perform regular health assessments for each connected game server.

```
GameServerPool → Game Server: MSG_QUERYSERVER
Game Server → GameServerPool: MSG_SERVERINFO (player count, capacity, status)
```

## Game Transition Process

### Character Selection to Game Server

The `GameTransitionService` orchestrates the complex process of moving players from the login server to appropriate game servers:

**Transition Flow**

1. **Character Selection**: Player selects character via `MSG_SELECTCHARACTER`
2. **Server Discovery**: Service queries GameServerPool for best available server
3. **Session Key Generation**: Creates secure session key for server handoff
4. **Zone Determination**: Calculates appropriate spawn zone and location
5. **Transition Message**: Sends connection details to client
6. **Session Cleanup**: Terminates login server session

### Zone and Location Determination

**Zone Selection Logic**

The transition service also determines zone placement, working with the [zone architecture](../gameserver/zone-architecture.md) to determine appropriate spawn locations:

```csharp
// Pseudo-code for zone determination
if (wizard.Zone.Contains("Phantom")) {
    // Player was in a minigame - use fallback or previous zone
    return string.IsNullOrEmpty(wizard.PreviousZone) ? 
           "WizardCity/WC_Hub" : wizard.PreviousZone;
}
return wizard.Zone; // Use player's last known zone
```

**Location Handling**

Player spawn locations are determined based on context:
- **Normal Zones**: Use player's last known position and orientation
- **Special Zones**: Reset to zone's default "Start" location
- **Invalid Locations**: Fallback to safe spawn points

### Session Key Security

**Cryptographic Session Handoff**

The transition process uses secure session keys to prevent unauthorized access:

1. **Key Generation**: Login server generates unique session key using cryptographic algorithms
2. **Server Registration**: Session key is registered with the target game server
3. **Client Handoff**: Key is provided to client for game server authentication
4. **Validation**: Game server validates the key before accepting the connection
5. **Key Expiration**: Keys have limited lifetimes to prevent replay attacks

## Configuration and Tuning

### Server Pool Configuration

The GameServerPool behavior is controlled through configuration settings:

```ini
[Game Server]
MaxGameServersAllowed = 10          # Maximum concurrent game servers
GameServerPlayerLimit = 100         # Players per server capacity
GameServerQueryTimeout = 10         # Health check timeout (seconds)
```