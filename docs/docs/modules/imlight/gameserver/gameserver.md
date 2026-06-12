# Game Server

The **Game Server** handles game sessions, world management, and player interactions within the Imlight network.

Game servers exist as child actors managed by the login server's **GameServerPool**. They register themselves with the login server upon startup and receive player connections through the login server's load balancing system.

## Architecture Overview

Built on Imlight's [actor model](../concepts/actorfundamentals.md), each game server maintains several key child actors that handle different aspects of game functionality.

<GameServerArchitecture />

## Game World

The **GameWorld** actor is the central coordinator for all zones and instances within a game server. Only one GameWorld actor exists per game server instance.

### Zone Management

The GameWorld maintains two primary collections:

**Public Zones**
- Shared zones accessible by all players
- Created on-demand when first player requests access
- Persistent until server shutdown
- Stored in `_zoneActors` dictionary keyed by zone name

**Private Instances**
- Player-owned zone instances (houses, dungeons)
- Created per owner using `InstanceContainer` actors
- Keyed by `{zoneName}_{ownerCharId}` format
- Automatic cleanup when empty

### Zone Transfer Process

When a player requests zone transfer via `MSG_ZONETRANSFER`:

1. **Zone Type Check**: Determines if transfer is to public zone or private instance
2. **Zone Creation**: Creates new zone actor if it doesn't exist
3. **Transfer Forwarding**: Routes the transfer request to appropriate zone actor
4. **Instance Management**: For private zones, creates or retrieves `InstanceContainer`

### Instance Containers

Instance containers manage private zones:
- Handle multiple related zones (multi-floor dungeons)
- Track zone ownership via character ID
- Provide isolation between different player groups
- Enable content like player housing and private dungeon runs

### World Queries

The GameWorld responds to administrative queries:
- **Zone Lists**: Returns all active public zones and private instances
- **World Statistics**: Provides zone count, instance count, and player distribution
- **Player Tracking**: Coordinates player movement between zones

## Session Key Authentication

Game servers use cryptographic session keys for secure player authentication:

**Key Validation Process**
- Login server generates session keys during player transitions
- Game server validates keys before accepting player connections  
- Keys have configurable expiration times to prevent replay attacks
- Invalid keys result in connection rejection