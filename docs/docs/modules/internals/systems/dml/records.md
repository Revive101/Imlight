# DML Records
To decode a binary representation of a DML message, a serializer must understand a standardized template ahead of time in order to compute readable data. To do this, the game client employs an entire representation of said data in a _DML record_.

Each record will also have it's own template as found in the nodes that follow the [protocol](./protocols.md) information node.

## Record Syntax
Each DML record starts as an XML node named as a short identifier, prefixed with `MSG_[...]`. This node holds one child node, `RECORD`, where each DML element is written in order of encoding.

```xml
<MSG_CREATECHARACTERRESPONSE>
    <RECORD>
      <_MsgOrder TYPE="UBYT" NOXFER="TRUE">5</_MsgOrder>
      <_MsgName TYPE="STR" NOXFER="TRUE">MSG_CREATECHARACTERRESPONSE</_MsgName>
      <_MsgDescription TYPE="STR" NOXFER="TRUE">Server sends this when character creation is completed.</_MsgDescription>
      <_MsgHandler TYPE="STR" NOXFER="TRUE">MSG_CreateCharacterResponse</_MsgHandler>
      <ErrorCode TYPE="INT"></ErrorCode>
    </RECORD>
</MSG_CREATECHARACTERRESPONSE>
```
__Figure A.I__ - An example of a DML Record.

A message may have a metadata node named `MsgAccessLevel`, which is a value between `0` and `1`. In its absence, it is safe to assume the access level of the message is `0`.

An access level of `1` declares that this message may only be processed once a valid [session](../kinp/session.md) has been established.

::: info
There will be some metadata in every record. The majority is developer semantics, but one crucial field—either `_MsgOrder` or `_MsgType`—is utilized immediately during encoding.
The game client does not _not_ provide this metadata piece for all protocols, for whatever reason.

In the event that such a situation occurs, each record's unique message ID will be the order in which they appear when ordinally sorted.
:::

## DML Elements
```xml
<UserID TYPE="GID" [NOXFER="TRUE"]></UserID>
```
__Figure A.II__ - An example of a DML element.

Each DML element has a `TYPE` attribute to signfy how it is serialized binarily during the encoding process.

The `NOXFER` attribute, which indicates a metadata node that is not included in the encoding process, is optional.

::: warning
These records do not enforce any sort of lint, and attributes may be misspelled. 
:::
