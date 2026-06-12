# Distributed message layer
The _distributed message layer_, also abbreviated as DML, is a subsidary data layer that the game client employs to distribute data packets through in-game systems.
It is also utilized to transmit game actions to and from the server in [KiNP](../kinp/index.md).

## Data Types
DML supports a wide array of data types. Below represents a table said formats.

| Name | Type | Binary |
| :--- | :--- | :----- |
| `BYT` | `int8_t` | Signed 1-byte integer. |
| `BOOL` | `int8_t` | Signed 1-byte boolean. |
| `UBYT` | `uint8_t` | Unsigned 1-byte integer. |
| `SHRT` | `int16_t` | Signed 2-byte integer. |
| `USHRT` | `uint16_t` | Unsigned 2-byte integer. |
| `INT` | `int32_t` | Signed 4-byte integer. |
| `UINT` | `uint32_t` | Unsigned 4-byte integer. |
| `STR` | `uint8_t[]` | A length prefix (`uint16_t`) string of UTF-8 characters.. |
| `WSTR` | `uint16_t[]` | A length prefix (`uint16_t`) string of UTF-16 characters. |
| `FLT` | `float` | 32-bit floating point. |
| `DBL` | `double` | 64-bit floating point. |
| `GID` | `uint64_t` | Unsigned 8-byte integer used exclusively for global IDs. |

__Figure A.I__ -- The binary sizes of each DML element type.

## DML Templates
For serialization purposes, a template must be known ahead of time to serialize binary data accordingly. In the case of the game client, the DML messages are kept internally, and can be represented as XML.

Using an unpacking tool such as [katsuba](https://github.com/vbe0201/katsuba), these message templates can be found in `Root.wad` in the `GameData` directory. Once unpacked,
a user may search for "*Messages.xml" to find the XML representation of these templates.