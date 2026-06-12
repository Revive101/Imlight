# Imcodec

[Imcodec](https://github.com/Jooty/Imcodec) is a standalone library and command-line utility that enables understanding of game client structures and data formats. It serves as the foundation for Imlight's ability to process and interpret game data.

Imlight imports Imcodec as a git submodule.

### Command-Line Interface

Imcodec also offers a command-line tool for direct interaction. Since Imlight imports Imcodec as a submodule, the CLI can be built and run from the Imlight repository.

#### Archive Commands (`wad`)
```
imcodec wad unpack <archivePath> [outputPath] [--deser] [--verbose]
```

* `archivePath`: Path to the KIWAD archive file
* `outputPath`: Optional path to the output directory. If this is not present, defaults to the archive name in the same directory.
* `--deser`: Attempt to deserialize archive files
* `--verbose`: Enable detailed output (may impact performance on large archives)

#### Object Property Commands (`op`)
```
imcodec op file <inputPath> [outputPath]
```

* `inputPath`: Path to the binary file to deserialize
* `outputPath`: Optional path to save deserialized JSON (defaults to input path + "_deser.json" suffix)

```
imcodec op blob <hexblob>
```

* `hexBlob`: Hexadecimal string representing binary data to deserialize

:::tip
This is far from an exhaustive list. For a full set of features, see the [Imcodec GitHub repository](https://github.com/Jooty/Imcodec).
:::

### Key Libraries

The primary use for Imcodec in Imlight is as a library. The following components are most relevant:

| Library | Purpose |
|---------|---------|
| **Imcodec.Wad** | KIWAD archive unpacking and file extraction |
| **Imcodec.ObjectProperty** | Binary serialization/deserialization system |
| **Imcodec.MessageLayer** | Client-server message protocol handling |
| **Imcodec.CoreObject** | Game object serialization utilities |
| **Imcodec.Cryptography** | String hashing and cryptographic utilities |


## Source Generation

It's vital for Imlight to understand the game client's data structures. Imcodec uses a source generation approach to create strongly-typed C# classes from type definitions extracted from the game client.

These classes are generated at compile-time, allowing Imlight to work with known types without relying on runtime reflection.

#### Object Property

The game client uses a monumental amount of custom data types. Each of these types derives [PropertyClass](../../internals/systems/op/propertyclass.md), which marks them as serializable by the [ObjectProperty](../../internals/systems/op/serialization.md) system. This system is used to serialize and deserialize game data which will then be sent over the network.

#### Partial Definitions

The property class system only marks properties capable of undergoing binary serialization. It does not define behaviors or methods. Therefore, Imcodec generates these classes as `partial`, allowing Imlight to extend them with additional functionality.

Imcodec also generates the `Encode` and `Decode` methods required for serialization. This can allow developers to even create their own new properties in these partial classes without breaking the game client's understanding of the definitions.

**Type Definitions → Runtime Classes**

```csharp
// Input: JSON type dump from wiztype
{
  "className": "WizClientObjectItem",
  "properties": [
    { "name": "m_templateID", "type": "GID" },
    { "name": "m_globalID", "type": "GID" }
  ]
}

// Generated: Strongly-typed C# class
public partial class WizClientObjectItem : PropertyClass {
    public GID m_templateID { get; set; }
    public GID m_globalID { get; set; }
}
```

:::tip
A JSON type dump can be obtained using the [WizType](https://github.com/wizspoil/wiztype) tool.
:::

#### Message Records

The game client and server communicate using a series of messages, each with a specific structure. These messages are defined in XML files within the game client. Imcodec processes these XML definitions to generate C# classes representing each message type.

**Message Records → Protocol Classes**

```csharp
// Generated from XML message definitions
namespace GAME_5_PROTOCOL {
    public class MSG_EQUIPITEM : IMessage {
        public GID ItemID { get; set; }
        public EquipmentSlotType SlotType { get; set; }
    }
}
```

:::tip
These message record files can be extracted using Imcodec itself from the [Root.wad](./shared-data-systems.md#rootwad---the-foundation) file. Each file will match a wildcard search of `*Messages*.xml`.
:::

## Data Flow Integration

### In Imlight Context

```
Game Client → Imlight Server → Imcodec Library
    ↓             ↓              ↓
  Raw Data   Binary Messages   Data Parsing & Type Mapping
```

The game client sends binary data to the Imlight server. Imlight uses Imcodec as a library to parse, deserialize, and map this data into usable game objects and logic.

**Client Data Processing**
1. Game client sends binary-serialized messages
2. Imcodec deserializes messages using generated type definitions
3. Imlight processes structured data through [actor system](./actorfundamentals.md) and [message handling](./messagehandling.md)

## Emitted Classes

Imcodec will, by default, emit all the generated classes. You can find this directory at:

```
submodule/Imcodec/src/Imcodec.ObjectProperty/obj
```

Depending on your build configuration, this may be `Debug` or `Release`. You can find the generated files in the `generated` directory of the build.

:::tip
Debugger breakpoints do work in these generated files, allowing you to step into serialization and deserialization logic.
:::