# Serialization
When serializing ObjectProperty, a set of bit flags may be employed to change serialization behavior.

| Bit | Name | Description |
| :-- | :--- | :---------- |
| `0` | UseFlags | Indicates that the set of flags should be used during data processing. |
| `1` | CompactPrefix | Enables the use of smaller data types for length prefixes whenever possible. |
| `2` | StringEnum | Indicates that enums should be represented as strings. |
| `3` | ZLibCompress | Enables data compression using the Zlibrary. |
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