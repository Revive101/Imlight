# DML Serialization
In order to prepare a [DML record](./records.md) for passing via [KiNP](../kinp/index.md), the object is preemptively encoded, which converts the data into a binary representation. The values of each DML element are literally serialized as binary in the order that they appear in the template.

## DML Header
Continuing from the [packet framing](../kinp/packet-framing.md) of our magic packets; after the body header has been created, a secondary payload header is employed, known as the DML header.
This DML header helps a decoder to map the encoded binary data to its respective protocol.

| Name | Type | Description |
| :--- | :--- | :---------- |
| Service ID | `byte` | The unique protocol ID a message originates from. |
| Message ID | `byte` | The unique message ID relative to the protocol. |
| Length | `uint16_t` | The length of the following payload, including this header. |
| Payload | `byte[]` | The following DML record, with each DML element serialized in order of writing. |

__Figure A.I__ - A table denoting the DML header.