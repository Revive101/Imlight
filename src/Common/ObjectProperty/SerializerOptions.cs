using System;

namespace Imlight.Common.ObjectProperty;

public class SerializerOptions {
    public enum Mode {
        Compact,
        Verbose
    }

    [Flags]
    public enum Behaviors {
        None,
        UseFlags      = 1 << 0, // States the serializer should use these flags for deserialization.
        CompactLength = 1 << 1, // Length prefixes are compacted into smaller data types whenever possible.
        StringEnums   = 1 << 2, // Some enums are made into strings.
        Compress      = 1 << 3, // Use ZLib compression.
        AlwaysEncode  = 1 << 4, // Always serialize properties with bitflag `8`.
    }

    [Flags]
    public enum PropertyFlags {
        Save              = 1 << 0,
        Copy              = 1 << 1,
        Public            = 1 << 2,
        Transmit          = 1 << 3,
        AuthorityTransmit = 1 << 4,
        Persistent        = 1 << 5,
        Deprecated        = 1 << 6,
        NoScript          = 1 << 7,
        Encode            = 1 << 8,
        Blob              = 1 << 9,

        Immutable         = 1 << 16,
        FileName          = 1 << 17,
        Color             = 1 << 18,

        Bits              = 1 << 20,
        Enum              = 1 << 21,
        Localized         = 1 << 22,
        StringKey         = 1 << 23,
        ObjectId          = 1 << 24,
        ReferenceId       = 1 << 25,

        ObjectName        = 1 << 27,
        HasBaseClass      = 1 << 28,
    }

    public Mode SerializerMode { get; set; }
    public Behaviors BehaviorFlags { get; set; }
    public PropertyFlags PropertyMask { get; set; }

    public SerializerOptions(Mode mode = Mode.Compact,
                           Behaviors flags = Behaviors.None,
                           PropertyFlags propertyFlags = PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit) {
        this.SerializerMode = mode;
        this.BehaviorFlags = flags;
        this.PropertyMask = propertyFlags;
    }

    public SerializerOptions OnMode(Mode mode) {
        this.SerializerMode = mode;
        return this;
    }

    public SerializerOptions OnBehaviors(Behaviors flags) {
        this.BehaviorFlags = flags;
        return this;
    }

    public SerializerOptions OnPropertyMask(PropertyFlags flags) {
        this.PropertyMask = flags;
        return this;
    }
}
