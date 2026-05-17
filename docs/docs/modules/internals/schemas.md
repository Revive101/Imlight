# Schemas
This page is used to document the commonly used mechanisms the game client employs.

## Object Creation
The game client merely calls upon or passes around data that is expressly required for a purpose; in other words, it distributes data sparingly.
To do this, three distinct items are employed in succession:
* __Template__: Expresses default values and overall structure of a specific object.
* __Info__: Describes the specific values that may be modified in the template of a specific object.
* __Object__: The end result object.

A manifest is used to first obtain the template. These templates are located in the game client's `Root.wad` file, and a particular template can be located by looking up a certain value.
* For [CoreObjects](./systems/op/coreobject.md), the [Template Manifest](./systems/op/coreobject.md#template-manifest) is used.
* Game effects use a collection of manifests found in `GameEffectData/` and `GameEffectRuleData/` directories.

## String ID

For commonly used string literals, the game client will instead employ a `uint32_t` hash rather than the actual string for convenient purposes.

```csharp
public static uint HashString(string input)
{
    int result = 0;

    var shift1 = 0;
    var shift2 = 32;
    foreach (char c in input)
    {
        var cb = (byte)c;

        result ^= (cb - 32) << shift1;

        if (shift1 > 24)
        {
            result ^= (cb - 32) >> shift2;
            if (shift1 >= 27)
            {
                shift1 -= 32;
                shift2 += 32;
            }
        }
        shift1 += 5;
        shift2 -= 5;
    }

    if (result < 0)
         result = -result;

    return (uint)result;
}
```
__Figure A.I__ - A string ID hash method from Imlight's cryptography library.