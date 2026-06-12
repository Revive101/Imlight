# PropertyClass
The game client uses an abstract data type, known as a _PropertyClass_, to designate complex types capable of undergoing binary serialization. Internally, this is an assembly [reflection](https://learn.microsoft.com/en-us/cpp/dotnet/reflection-cpp-cli?view=msvc-170). Each PropertyClass must be labeled with a property hash.

The property class hash is a [string ID](../../schemas.md#string-id) of the PropertyClass name, prefixed with it's internal representation, such as `class` or `struct`.

## Property
Each property of the `PropertyClass` is simply labeled as `Property`, and serves as specific field elaboration to the binary encoding process. 

Each `Property` element denotes two important attributes: `Hash`, and `Flags`.

### Property Hash
Each property may be prefixed with an attribute detailing it's property hash. This is an expansion of a [string ID](../../schemas.md#string-id).

```csharp
public static uint HashPropertyName(string name, string type)
{
    uint typeHash = HashString(type);
    var propHash = DJB2(name) & 0x7FFF_FFFF;

    // Dropping the most-significant byte.
    return (typeHash + propHash) & 0xFFFF_FFFF;
}

public static uint DJB2(string str)
{
    uint hash = 5381;

    foreach (char c in str)
    {
        hash = ((hash << 5) + hash) + c;
    }

    return hash;
}
```
__Figure A.I__ - Imlight's cryptography for hashing a property.

### Property Flags
Each property may also be prefixed with a bitflag operator attribute detailing how it should be processed through a serializer.

```csharp
[Flags]
public enum PropertyFlags
{
    Save = 1              << 0,
    Copy = 1              << 1,
    Public = 1            << 2,
    Transmit = 1          << 3,
    AuthorityTransmit = 1 << 4,
    Persistent = 1        << 5,
    Deprecated = 1        << 6,
    NoScript = 1          << 7,
    Encode = 1            << 8,
    Blob = 1              << 9,

    Immutable = 1         << 16,
    FileName = 1          << 17,
    Color = 1             << 18,

    Bits = 1              << 20,
    Enum = 1              << 21,
    Localized = 1         << 22,
    StringKey = 1         << 23,
    ObjectID = 1          << 24,
    ReferenceID = 1       << 25,
                                  
    ObjectName = 1        << 27,
    HasBaseClass = 1      << 28,
}
```
__Figure B.I__ - An enum of all possible property bit flags.

## Finding each PropertyClass
Third-party tools such as [WizWalker](https://github.com/wizwalker/wizwalker) or [WizType](https://github.com/wizspoil/wiztype) may suffice in dumping the in-game types.