# DML Protocols
Upon extracting the DML messages from the `Root.wad`, we can observe that every file is marked with an XML node named `_ProtocolInfo` right away. The DML protocol that the messages in the following list relate to is indicated by this node.

DML Protocols are a [KiNP](../kinp/index.md) subcategory that aids in assigning specific game actions to corresponding protocols.

## ProtocolInfo
Each message file begins with an XML node labeled as `_ProtocolInfo`. This is a metadata node which always contains 4 elements.

| Name | Description |
| :--- | :---------- |
| Service ID | The unique ID of this protocol, used for encoding and decoding. |
| Protocol Type | A one-word descriptor of the protocol. |
| Protocol Version | The version of the protocol. |
| Protocol Description | A short summary detailing the protocol. |

__Figure A.I__ -- The fields a protocol may contain.

```xml
<_ProtocolInfo>
    <RECORD>
        <ServiceID TYPE="UBYT">5</ServiceID>
        <ProtocolType TYPE="STR">GAME</ProtocolType>
        <ProtocolVersion TYPE="INT">1</ProtocolVersion>
        <ProtocolDescription TYPE="STR">Game Messages</ProtcolDescription>
    </RECORD>
</_ProtocolInfo>
```
__Figure A.II__ - An example of a `_ProtocolInfo` node.

### List of Protocols
The table below represents all the protocols that currently exist internally.

| Service ID | Protocol |
| :--------- | :------- |
| `1` | SYSTEM |
| `2` | EXTENDEDBASE |
| `5` | GAME | 
| `7` | LOGIN |
| `8` | PATCH | 
| `9` | PET |
| `10` | SCRIPT |
| `11` | TESTMANAGER |
| `12` | WIZARD |
| `15`| MOVEBEHAVIOR |
| `16` | PHYSICS |
| `19` | AISCLIENT |
| `25` | SOBLOCKS |
| `40` | SKULLRIDERS |
| `41` | DOODLEDOUG |
| `42` | MG1 |
| `43` | MG2 |
| `44` | MG3 |
| `45` | MG4 |
| `46` | MG5 |
| `47` | MG6 |
| `50` | WIZARDHOUSING |
| `51` | WIZARDCOMBAT |
| `52` | QUEST |
| `53` | WIZARD2 | 
| `54` | MG9 |
| `55` | GAME2 |
| `56` | WIZARD3 |
| `57` | CANTRIPS |

__Figure C.I__ -- A table of all known prototypes.

## Protocol Records
The following nodes will be the hard-coded template for every message the client is capable of encoding and decoding through DML.