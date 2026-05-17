# Authentication & Character Management

## Overview

The login server implements a two-phase authentication system followed by comprehensive character management. 

All authentication and character operations are handled by specialized [message services](../concepts/messageservices.md) within each player's [session actor](../concepts/sessionactor.md), maintaining strict separation of concerns and fault tolerance. These services are instantiated after the [session handshake](../../internals/systems/kinp/session.md#session-accept) is completed.

## Authentication Process

### Two-Phase Authentication

Imlight uses a dual-step authentication system for security and session management:

**Phase 1: Authentication (`MSG_USER_AUTHEN_V3`)**
- Client provides username and password credentials
- `AuthenticatorService` validates credentials against the account database
- Server generates cryptographic response (`Rec1`) for session security
- Returns user ID and account flags upon successful authentication

**Phase 2: Validation (`MSG_USER_VALIDATE`)**
- Client proves session validity using previously received authentication data
- Server verifies the session hasn't been compromised or duplicated
- Confirms the user isn't already logged in elsewhere in the network
- Establishes the authenticated session for character operations

### Authentication Flow

```
Client                    AuthenticatorService              Database
  │                              │                            │
  ├─── MSG_USER_AUTHEN_V3 ──────►│                            │
  │                              ├─── Validate Credentials ──►│
  │                              │◄─── Account Data ──────────┤
  │◄─── MSG_USER_AUTHEN_RSP ─────┤                            │
  │                              │                            │
  ├─── MSG_USER_VALIDATE ───────►│                            │
  │                              ├─── Check Online Status ───►│
  │                              │◄─── Session Validation ────┤
  │◄─── MSG_USER_VALIDATE_RSP ───┤                            │
  │                              │                            │
  │◄─── MSG_USER_ADMIT_IND ──────┤ (Queue Status)             │
```

### Security Features

**"Anti-Ambrose" Protection**
- `UserValidator` checks for duplicate connections by IP address
- Prevents multiple concurrent sessions from the same network

**Session Keys**
- Cryptographic session identifiers for server transitions
- Generated using `SessionKey` and `PassKey3` algorithms
- Secure handoff between login and game servers

**Account Flags**
- Role-based permissions and account status tracking
- Supports administrative privileges and account restrictions
- Integrated with the broader Imlight permission system

## Character Management

The `CharacterService` handles all character-related operations during the login phase.

### Character Creation

Character creation uses Imcodec's [object serialization system](../../internals/systems/op/serialization.md) to handle complex character data, working with the [player architecture](../concepts/playerarchitecture.md) to create new [Wizard](../concepts/playerarchitecture.md#wizard) objects:

1. **Client Data**: Character creation info serialized by the game client
2. **Deserialization**: Server deserializes the object data using `ObjectSerializer`
3. **Character Construction**: `CharacterHelper` converts creation data into a full [Wizard](../concepts/playerarchitecture.md#wizard) object with all necessary behaviors
4. **Database Storage**: New character is added to both account and character collections
5. **Response**: Client receives creation success/failure notification

::: warning
As mentioned in the [shared data systems documentation](../concepts/shared-data-systems.md), Imlight will *not* enforce client data integrity by default. On the chance the game client is using a different version than the server, it is possible for invalid character data to be sent during creation.

Imlight may also send data to the client that it does not understand, which can lead to deserialization failures.
:::

### Character Operations

Upon successful session validation, a brief exchange of messages happen between the client and the server:
1. The client requests the character list with `MSG_REQUESTCHARACTERLIST`
2. The server responds with `MSG_STARTCHARACTERLIST`
3. The server sends each character individually with `MSG_CHARACTERINFO`, which contains a serialized buffer with minimal character data only relevant to the character selection screen
4. The server ends the list with `MSG_CHARACTERLIST`

A user may also create a new character, delete an existing one, or select a character to enter the game world.

## Session Management

### Online Player Tracking

The login server maintains tracking of online players:

**OnlinePlayerCollection Integration**
- Tracks session ID, account ID, and current realm for each player
- Stores actor path for direct communication with player sessions
- Enables duplicate login detection and cross-server player lookup
- Provides foundation for administrative tools and player location services

**Session Lifecycle Management**
- Creates session tracking upon successful authentication
- Updates realm information during server transitions
- Removes tracking data when sessions end or timeout
- Handles orphaned sessions from unexpected disconnections

### Queue Management

When game servers reach capacity, the login server implements a queue system:

**Queue Position Tracking**
- Players receive their position in the queue via `MSG_USER_ADMIT_IND`
- Queue status updated as capacity becomes available
- Cached transition messages sent when queue position is reached

**AFK Detection**
- `LoginAFKService` monitors player activity during queue waits
- Automatic removal of inactive players to maintain queue integrity
- Configurable timeout values for different queue scenarios