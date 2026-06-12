# Login Server

The **Login Server** is the master server and sole entry point into the Imlight network.

Alongside the patch server, it is one of the highest actors in the Imlight actor hierarchy. It is responsible for authenticating players, managing character data, and orchestrating game servers.

## Architecture Overview

Built on Imlight's [actor model](../concepts/actorfundamentals.md), the login server maintains a hierarchical structure with the **GameServerPool** as its primary child actor, which in turn supervises all game servers in the network.

<LoginServerArchitecture />

## Game Server Management

The login server's most critical responsibility is managing the pool of available game servers.

:::tip
This is a very high-level overview. Please review the [server discovery documentation](./server-discovery.md) for a more in-depth look at how game servers register and communicate with the login server.
:::

### Registration Process

When a game server starts up:
1. Game server sends `MSG_CREATEGAMESERVER` to login server
2. LoginServer forwards the message to `GameServerPool`
3. `GameServerPool` validates server parameters (name, port, capacity limits)
4. If valid, `GameServerPool` creates and tracks the new server reference
5. Game server is now available for client connections

### Load Balancing

The `GameServerPool` has its own load balancer:
- Monitors player count across all registered game servers
- Considers server capacity limits from configuration
- Uses health checks with configurable timeouts
- Selects servers based on current load and availability

### Health Monitoring

Regular health checks ensure game server availability:
- Periodic `MSG_QUERYSERVER` messages sent to each game server
- Timeout-based detection of unresponsive servers
- Automatic removal of failed servers from the pool
- Graceful handling of server failures without affecting other servers