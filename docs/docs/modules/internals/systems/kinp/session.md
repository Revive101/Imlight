# KiNP Sessions
The client and the server it is attempting to connect to must first establish a valid session before any data is sent across KiNP. The server will try to initiate a handshaking process as soon as a new connection is established, during which the client and the server agree on a session.

## Session Offer
Upon the arrival of a new connection, any server implementation should allocate a unique value between the minimum and maximum values of a `ushort` (0-65535). This also means that any server is hard-capped by this values and may only ever handle this maximum.

Once a unique ID has been allocated, the server should respond to the new client connection with a [session offer](./controlmessages.md#session-offer) message. Most importantly, the `SessionID` field.

The other fields are used for validation purposes, and while there is no fault in leaving them blank, it is recommend that any server properly employ these fields.

## Session Accept
Once the game client has received the aforementioned message, it will respond with the [session accept](./controlmessages.md#session-accept) message echoing the session ID given.

The values returned by this message are used later during the [authentication](../../auth/auth.md) process and a server should remember these values for the session.

## Keep Alive
Once a session is established, the [keep alive](./controlmessages.md#keep-alive) messages are bounced between the client and server every so often to make sure both connections are still active.

The game client will send `KeepAlive` every 10 seconds to the server. The server may respond with it's own `KeepAlive` on any interval.

Both the server and the client will respond with the [keep alive response](./controlmessages.md#keep-alive-response) message.

::: info
For frame of reference, Imlight sends the `KeepAlive` to a session once every 60 seconds.
:::