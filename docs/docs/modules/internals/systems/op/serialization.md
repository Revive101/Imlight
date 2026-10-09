# Serialization
When serializing ObjectProperty, a set of bit flags may be employed to change serialization behavior.

| Bit | Name | Description |
| :-- | :--- | :---------- |
| `0` | UseFlags | Indicates that the set of flags should be used during data processing. |
| `1` | CompactPrefix | Enables the use of smaller data types for length prefixes whenever possible. |
| `2` | StringEnum | Indicates that enums should be represented as strings. |
| `3` | ZLibCompress | Enables data compression using the Zlibrary. See [Compression](#compression). |
| `4` | Encode | Indicates that all properties with bitflag of `8` must be included. |

__Figure A.I__ - A table of all possible bit flags for an object serializer.

## Serialization Modes
ObjectProperty serializers should anticipate two separate types of serialization: verbose, and compact.

In either case, a `PropertyClass` buffer will always prefix itself with its [property hash](./propertyclass.md#property-hash).

### Compact
In passage through the [DML](../dml/index.md), a _compact_ serializer is used. In compact mode, property hashes and their length are skipped during serialization. Properties are listed in order as they are written in reflection.

::: warning
If the field is another PropertyClass and it is null, an empty `uint32_t` will be serialized in place of the missing object.
:::

It should also be noted that any property with the deprecated flag is still serialized in this mode.

### Verbose
In all other cases, a _verbose_ serializer is employed. This is an exhaustive serialization method that allows for out of order properties.

In this mode, property hashes and their exact binary size will be serialized.

## Compression
A serialized buffer may be compressed in one of two ways. The code that reads the buffer decides which one applies, not the buffer itself, so the two are not interchangeable.

### Serializer Compression
When the `ZLibCompress` flag is set, the serializer reads and writes a single bit ahead of the object, stating whether the buffer is compressed. Everything after that bit starts on the next byte boundary.

| Field | Type | Description |
| :---- | :--- | :---------- |
| Flags | `uint32_t` | Only present when `UseFlags` is set. Replaces the flags the serializer was created with. |
| Compressed | 1 bit | Set when the rest of the buffer is compressed. |
| Size | `uint32_t` | The uncompressed size. Only present when the buffer is compressed. |
| Data | bytes | A zlib stream when the buffer is compressed. Otherwise, the object itself. |

__Figure A.II__ - The layout of a buffer serialized with `ZLibCompress`.

The mail list in `MSG_MAIL_DATA` is read this way.

### Standalone Compression
Some messages compress a buffer outside of the serializer. The receiver inflates the field first, then deserializes the result without the `ZLibCompress` flag. There is no leading bit.

| Field | Type | Description |
| :---- | :--- | :---------- |
| Size | `uint32_t` | The uncompressed size. When the highest bit is set, the data is stored uncompressed and the size is the remaining 31 bits. |
| Data | bytes | A zlib stream, or the raw buffer when the highest bit of the size is set. |

__Figure A.III__ - The layout of a standalone compressed buffer.

The character object in `MSG_LOGINCOMPLETE` is read this way, as are badge data and the character editing messages.

::: warning
Sending one layout where the other is expected does not fail cleanly. A standalone buffer read with `ZLibCompress` can report an unknown type hash of `0x78000000`: the reader takes the lowest bit of the size as the compressed bit, then reads the hash one byte in, across the rest of the size and the zlib header.
:::

In Imcodec, serializer compression is `SerializerFlags.Compress`. Standalone compression is `Compression.CompressWithLength` and `Compression.DecompressWithLength`, applied to a buffer that was serialized without the flag.