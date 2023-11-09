/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;

namespace Imlight.Common.ObjectProperty;

public class CoreObjectSerializer : ObjectSerializer {
    public CoreObjectSerializer() {
        base.Options.SerializerMode = SerializerOptions.Mode.Compact;
        base.Options.BehaviorFlags  = SerializerOptions.Behaviors.UseFlags | SerializerOptions.Behaviors.Compress;
        base.Options.PropertyMask   = SerializerOptions.PropertyFlags.Transmit | SerializerOptions.PropertyFlags.AuthorityTransmit;
    }

    /// <summary>
    /// Returns a new instance of the CoreObjectSerializer with the specified SerializerOptions.Mode.
    /// </summary>
    /// <param name="mode">The SerializerOptions.Mode to set.</param>
    /// <returns>A new instance of the CoreObjectSerializer with the specified SerializerOptions.Mode.</returns>
    public override CoreObjectSerializer OnMode(SerializerOptions.Mode mode) {
        this.Options.SerializerMode = mode;
        return this;
    }

    /// <summary>
    /// Returns a new instance of the CoreObjectSerializer with the specified SerializerOptions.Behaviors flags.
    /// </summary>readbits
    /// <param name="flags">The SerializerOptions.Behaviors flags to set.</param>
    /// <returns>A new instance of the CoreObjectSerializer with the specified SerializerOptions.Behaviors flags.</returns>
    public override CoreObjectSerializer OnBehaviors(SerializerOptions.Behaviors flags) {
        this.Options.BehaviorFlags = flags;
        return this;
    }

    /// <summary>
    /// Returns a new instance of the CoreObjectSerializer with the specified property flags.
    /// </summary>
    /// <param name="flags">The property flags to set.</param>
    /// <returns>A new instance of the CoreObjectSerializer with the specified property flags.</returns>
    public override CoreObjectSerializer OnPropertyMask(SerializerOptions.PropertyFlags flags) {
        this.Options.PropertyMask = flags;
        return this;
    }

    protected override bool PreWriteObject(BitWriter writer, PropertyClass propClass) {
        if (propClass is null) {
            writer.WriteInt8(0);
            writer.WriteInt8(0);
            writer.WriteUInt32(0);
            return false;
        }

        var coreObjectData = GetCoreObjectData(propClass);

        writer.WriteUInt8(coreObjectData.Item1);  // class ID
        writer.WriteUInt8(coreObjectData.Item2);  // Namespace ID

        // Write the template ID if this is a CoreObject. Otherwise, write the hash.
        if (propClass is TypeCache.CoreObject co) {
            writer.WriteUInt32((uint) (co.m_templateID & 0xFFFFFFFF));
        }
        else {
            writer.WriteUInt32(propClass.GetHash());
        }

        return true;
    }

    protected override bool PreloadObject(BitReader buffer, out PropertyClass? propClass) {
        // First, read a temporary hash as if it were a PropertyClass.
        // If we find a property class from the hash, we know it's not a CoreObject.
        var startingPos = buffer.BitPos();
        var tempHash = buffer.ReadUInt32();
        if (tempHash == 0) {
            propClass = null;
            return false;
        }

        var tempProp = TypeCache.Dispatch(tempHash);
        if (tempProp is not null) {
            propClass = tempProp;

            return true;
        }

        buffer.SeekBit(startingPos);
        propClass = GetCoreObjectFromHeader(buffer);

        return propClass != null;
    }

    private static (byte, byte) GetCoreObjectData(PropertyClass propClass) {
        if (propClass is null) {
            return (0, 0);
        }

        return propClass.GetHash() switch {
            350837933 => // ClientObject
                (2, 2),
            766500222 => // WizClientObject
                (104, 2),
            1653772158 => // WizClientObjectItem
                (115, 9),
            1167581154 => // WizClientPet
                (106, 2),
            2109552587 => // WizClientMount
                (108, 2),
            398229815 => // ClientReagentItem
                (132, 9),
            958775582 => // ClientRecipe
                (131, 131),
            _ => (0, 0)
        };
    }

    private static PropertyClass? GetCoreObjectFromHeader(BitReader buffer) {
        var classId = buffer.ReadUInt8();
        var namespaceId = buffer.ReadUInt8();
        var templateId = buffer.ReadUInt32();

        if (classId == 0 && namespaceId == 0) {
            return TypeCache.Dispatch(templateId);
        }

        return (classId, namespaceId, templateId) switch {
            (104, 2, 1) => // WizClientObject
                new TypeCache.WizClientObject(),
            _ => null
        };
    }

}
