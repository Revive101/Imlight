# Core Objects

A `CoreObject` is another type of object that is used to represent an actual in-world game object. It may be serialized and passed through [KiNP]( ../kinp/index.md) as it also derives from [PropertyClass](./propertyclass.md).

## Template Manifest
The template manifest is a banklet of key/value pairs tagging the `m_templateID` field of a `CoreObject` to the literal path of the template in the `Root.wad`.

## Behaviors
When you create a `CoreObject`, it's an empty entity that lacks any specific behaviors. To give a `CoreObject` specific abilities, you must assign behaviors that represent the interactions it can handle.

To assign behaviors to a `CoreObject`, the client (and its corresponding server) uses a template ID. This is the [object creation schema](../../schemas.md#object-creation). Each behavior found in this template will also follow this schema, meaning that each behavior found will be a template rather than the proper instance.

::: tip
It's recommended that a server implementation employ their own behavior manifest to map each behavior template to a behavior instance.
:::

Once a behavior instance is gathered, they may be added to the `m_inactiveBehaviors` field of the `CoreObject`.

## CoreObject Serialization
The serializer for a `CoreObject` is a variation of the [object serializer](./serialization.md), with some modifications to the `PreLoad`/`PreWrite` behavior.

Unlike the `PropertyClass` serializer, the `CoreObject` serializer always operates in compact mode.

The distinctive feature of this serializer lies in the way it handles hashes. Instead of the usual `uint32_t` `PropertyClass` hash, a 6-byte header is employed in its place.

It should be noted that even if a `PropertyClass` does not derive from `CoreObject`, the technique below is still used. If such a case occurs, both the `CoreObject` id and namespace id fields will be `0`, and the hash of the `PropertyClass` will be used instead of a template ID.

| Name | Type | Description |
| :--- | :--- | :---------- |
| `CoreObject` ID | `byte` | The ID of the CoreObject. |
| Namespace ID | `byte` | The namespace ID this `CoreObject` belongs to. |
| Template/Hash | `uint32_t`| The template ID or usual property class hash if not possible. |
| `PropertyClass` | `byte[]` | The following serialized `PropertyClass`. |

__Figure A.I__ - A table representing the `CoreObject` serialization technique.