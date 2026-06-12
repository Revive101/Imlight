# Packet Framing

This paper provides a broad description of the game client packet framing used by [KiNP](./index.md). 
Game packets packets will hereafter be referred to as "magic packets."

Assume that every byte sequence will be in little-endian order unless otherwise stated.

## Magic Header
Each magic packet is denoted with a 2-byte sequence: `0xf00d` (or, `0x0d 0xf0`). This is referred to as the _start signal_.
Following the start signal is another 2-byte sequence, which denotes the length of the following message.

### Normal Header

| Name | Type | Description |
|:---- | :--- | :---------- |
| Start Signal | `uint16_t` | Always `0xf00d`. Indicates the start of a KiNP packet. |
| Length | `uint16_t` | The length of the following body. |

__Figure A.I__ -- The structure of a packet with a length less than `0x8000`.


### Big Header
If the following body size is larger than the bounds of a `uint16_t`, or `0x7fff`, the header will transform to include an extra `uint32_t` denoting the proper binary size.

| Name | Type | Description |
| :--- | :--- | :---------- |
| Start Signal | `uint16_t` | Always `0xf00d`. Indicates the start of a KiNP packet. |
| Length | `uint16_t`| Becomes `0x8000`. |
| Big Length | `uint32_t` | The actual length of the following body. |

__Figure A.II__ -- The structure of a packet with a length greaterh than `0x8000`.


## Body
The body of the magic packet is identified as the following binary data. To better elucidate sent data, this states raw binary data that has been divided into two subcategories. These kinds are referred to as data and control messages. As a result of the two potential architecture types, KiNP uses an additional preparatory header called the body header.

### Body Structure

| Name | Type | Description |
| :--- | :--- | :---------- |
| IsControl | `byte` | Denotes if this body is a control message or a data message. |
| Operation Code (opcode) | `byte` | Denotes the operation code, if the message is a control message. `0` for data. |
| Padding | `uint16_t` | Padding bytes. |
| Payload | `byte[]` | The transmitted data. |

__Figure A.III__ -- The body structure of a packet.

If the message is a [control](controlmessages.md) message, then the fields of the message are immediately serialized into the payload. In the case of a DML message, a secondary header is serialized into the buffer during its own [encoding](../dml/serialization.md) process.