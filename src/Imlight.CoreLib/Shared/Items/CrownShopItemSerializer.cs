using Imcodec.CoreObject;
using Imcodec.IO;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Shared.Items;

/// <summary>Core-object wire format with the pinned codec's pet-snack fallback corrected.</summary>
public sealed class CrownShopItemSerializer() : ObjectSerializer(false, SerializerFlags.None) {
    private readonly CoreObjectSerializer _core = new(behaviors: SerializerFlags.None);

    public override PreloadResult PreloadObject(BitReader reader, out PropertyClass propertyClass)
        => _core.PreloadObject(reader, out propertyClass);

    public override bool PreWriteObject(BitWriter writer, PropertyClass propertyClass) {
        if (propertyClass is not ClientPetSnackItem) return _core.PreWriteObject(writer, propertyClass);
        // ClientPetSnackItem has no core block/type mapping. The 0/0 envelope
        // identifies an ordinary class hash, not the item's template ID.
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        writer.WriteUInt32(propertyClass.GetHash());
        return true;
    }
}
