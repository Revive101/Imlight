# Control Messages
Similar to [DML protocols](../dml/protocols.md), there is a category for control messages which Imlight identifies with a protocol id of `0`.

These messages are used to establish a proper [session](./session.md) between the game client and a server through a handshake process.

## Session Offer
OpCode: `0`
| Name | Type | Description |
| ---- | ---- | ----------- |
| SessionId | USHRT | The session ID allocated by the server |
| Timestamp Upper | INT | The upper 4 bytes of a timestamp. |
| Timestamp Lower | INT | The lower 4 bytes of a timestamp. |
| Milliseconds | UINT | The number of milliseconds into the current second. |

__Figure A.I__ -- The `SessionOffer` message structure.

::: warning
There are additional fields in this message that are not understood by us yet. They relate to the packet encryption introduced in 2021. However in the absence of these fields, a game/client session can still be initiated without encryption.
:::

## Keep Alive
OpCode: `3`
| Name | Type | Description |
| ---- | ---- | ----------- |
| SessionId | USHRT | The agreed upon session ID. |
| Milliseconds | UINT | The number of milliseconds into the current second. |
| ElapsedSessionTime | USHRT | The amount of time elapsed on the current session |

__Figure A.II__ -- The `KeepAlive` message structure.

## Keep Alive (Server)
OpCode: `3`
| Name | Type | Description |
| ---- | ---- | ----------- |
| SessionId | USHRT | The agreed upon session ID. |
| Milliseconds | UINT | The number of milliseconds into the current second. |

__Figure A.III__ -- The `KeepAlive` (server) message structure.

This packet is sent from the server to the client, and will always have a `SessionId` field of `0`.

## Keep Alive Response
OpCode: `4`
| Name | Type | Description |
| ---- | ---- | ----------- |
| SessionId | USHRT | The agreed upon session ID. |
| Milliseconds | UINT | The number of milliseconds into the current second. |
| ElapsedSessionTime | USHRT | The amount of time elapsed on the current session |

__Figure A.I__ -- The `KeepAliveResponse` message structure.

## Session Accept
OpCode: `5`
| Name | Type | Description |
| ---- | ---- | ----------- |
| Reserved | USHRT | ... |
| Timestamp Upper | INT | The upper 4 bytes of a timestamp. |
| Timestamp Lower | INT | The lower 4 bytes of a timestamp. |
| Milliseconds | UINT | The number of milliseconds into the current second. |
| SessionId | USHRT | The agreed upon session ID. |

__Figure A.IV__ -- The `SessionAccept` message structure.

::: warning
There are additional fields in this message that are not understood by us yet. They relate to the packet encryption introduced in 2021. However in the absence of these fields, a game/client session can still be initiated without encryption.
:::