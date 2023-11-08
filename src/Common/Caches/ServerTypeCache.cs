using System.Collections.Generic;
using System.Diagnostics;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
// ReSharper disable InconsistentNaming

namespace Imlight.Common.Caches;

/*
   Reading through server type byte blobs:

   65 or 72 bits -- Boolean
   12 bytes      -- Floating point.
   8 bytes       -- (U)Int32, empty List<T>, Enum

   A list of ByteString can end up at odd bit counts.
*/

public static class ServerTypeCache {
    public static PropertyClass? Dispatch(uint hash) {
        return hash switch {
            0x06DAAC43 => new WizZoneTriggers(),
            0x068C265B => new Trigger(),
            0x1B6EF770 => new WizZoneVolumes(),
            0x1B7B55F6 => new Volume(),
            0x774C0B33 => new ResDisplayText(),
            0x3C626744 => new ResPlaySound(),
            0xDa51FA8 => new ZoneRouter(),
            478486736 => new CombatSigil(),
            _ => null
        };
    }

    public class WizZoneTriggers : PropertyClass {
        public override uint GetHash() => 0x06DAAC43;

        [Property(0x3F1DB764, 31)] public List<Trigger>? m_triggers;
    }

    [DebuggerDisplay("{m_triggerName}")]
    public class Trigger : PropertyClass {
        public override uint GetHash() => 0x068C265B;

        [Property(0xB8C90C10, 31)] public ByteString m_triggerName;
        [Property(0x3933D634, 31)] public uint unknown_1;
        [Property(0x767AAC3C, 31)] public uint unknown_2;
        [Property(0x2E8B9981, 31)] public uint unknown_3;
        [Property(0x3282D78A, 31)] public bool unknown_bool;
        [Property(0x7DB09CC1, 31)] public List<ByteString>? unknown_5;
        [Property(0xA7BEADF6, 31)] public List<ByteString>? m_volumes;
        [Property(0x62A2160A, 31)] public byte unknown_byte_1;
        [Property(0x5C548D5F, 31)] public byte unknown_byte_2;
        [Property(0xA955FFA6, 31)] public TypeCache.RequirementList? m_requirements;
        [Property(0xE11C8ADA, 31)] public TypeCache.ResultList? m_results;
        [Property(0x794EA0DF, 31)] public uint unknown_uint_3;
        [Property(0x88B9D287, 31)] public byte unknown_byte_3;
        [Property(0x8177DA98, 31)] public uint unknown_int;
    }

    public class WizZoneVolumes : PropertyClass {
        public override uint GetHash() => 0x1B6EF770;

        [Property(0x884BFB48, 31)] public List<Volume>? m_volumes;
    }

    [DebuggerDisplay("{m_volumeName}")]
    public class Volume : TypeCache.CoreObjectInfo {
        public override uint GetHash() => 0x1B7B55F6;

        // CoreObjectInfo properties end here.
        [Property(0xC6E6048B, 31)] public ByteString m_volumeName;
        [Property(0x7DB3F828, 31)] public float m_locationX;
        [Property(0x7DB3F829, 31)] public float m_locationY;
        [Property(0x7DB3F82A, 31)] public float m_locationZ;
        // Yes, this is a duplicate property. KI making another certified whoopsie daisy moment.
        [Property(0x40183401, 31)] public new ulong m_templateID;
        [Property(0x8987B2CC, 31)] public ByteString m_primitiveType; // @todo: convert to enum
        [Property(0x3AF933DF, 31)] public float m_radius;
        [Property(0x2D481539, 31)] public float m_length;
        [Property(0x35EBF597, 31)] public float m_width;
        [Property(0x3492258C, 31)] public int unknown_int;
        [Property(0x3B3CD5DA, 31)] public bool unknown_1;
        [Property(0x71FCB022, 31)] public byte unknown_2;
        [Property(0x8576192E, 31)] public List<ByteString>? m_enterEvents;
        [Property(0xAB57CF4A, 31)] public List<ByteString>? m_exitEvents;
    }

    public class ResTeleport : TypeCache.Result {
        public override uint GetHash() => 228794493;

        public string? m_destinationLoc { get; set; }
        public string? m_destinationZone { get; set; }
        [Property(0x2, 31)] public byte m_exitTeleporter;
        [Property(0x3, 31)] public byte m_teleporterTag;
        [Property(0x4, 31)] public TeleportType m_teleportType;
        [Property(0x5, 31)] public byte m_transitionID;

        public enum TeleportType {
            TELEPORT_STATIC,
        }
    }

    public class ResDisplayText : TypeCache.Result {
        public override uint GetHash() => 0x774C0B33;

        [Property(0x66603160, 31)] public ByteString m_text;
        [Property(0x0D1B703C, 31)] public int m_type;
        [Property(0x3AF933DF, 31)] public float m_radius;
        [Property(0x431157E7, 31)] public float m_locationX;
        [Property(0x431157E8, 31)] public float m_locationY;
        [Property(0x431157E9, 31)] public float m_locationZ;

        [Property(0x2EB6A55F, 31)] public bool m_unknown_bool;
        [Property(0x7E84339F, 31)] public bool m_unknown_bool_2;
        [Property(0x57EDA63C, 31)] public bool m_unknown_bool_3;
    }

    public class ResPlaySound : TypeCache.Result {
        public override uint GetHash() => 0x3C626744;

        [Property(0x444373FA, 31)] public ZoneRouter? m_router;
        [Property(0x87BA8BE5, 31)] public ByteString m_soundName;

        // 0x1D70805C | Size: 65 bits
        // 0x3B657FD7 | Size: 103 bits (~13 bytes)
        // 0x3B9498D7 | Size: 65 bits
        // 0x2C2BC314 | Size: 12 bytes
    }

    public class ZoneRouter : PropertyClass {
        public override uint GetHash() => 0xDA51FA8;

        [Property(0x12773D2D, 31)] public float m_locX;
        [Property(0x12773D2E, 31)] public float m_locY;
        [Property(0x12773D2F, 31)] public float m_locZ;
        [Property(0xC7FCACAC, 31)] public RoutingType m_routingType;
        [Property(0xE36CE99, 31)] public bool m_useLocation;
        [Property(0x148E0B6D, 31)] public bool m_useTriggerLocation;

        public enum RoutingType {
            ROUTING_ACTOR,
        }
    }

    public class CombatSigil : TypeCache.CoreObjectInfo {
        public override uint GetHash() => 478486736;

        // Properties here are listed in order of their understanding.
        // The template for this object is a DynamicTriggerTemplate.

        [Property(0x7B91Df78, 31)] public ByteString m_sigilType;
        [Property(0xADC3A56F, 31)] public ByteString m_zoneTag2;
        [Property(0x3AF933DF, 31)] public float m_radius;
        [Property(0x595FC144, 31)] public int m_firstTeamToAct;
        [Property(0x4AFCF400, 2097183)] public TypeCache.Duel.SigilInitiativeSwitchMode m_initiativeSwitchMode;
        [Property(0x203340FD, 31)] public int m_initiativeSwitchRounds;
        [Property(0x975DE361, 268435463)] public List<ByteString>? m_lootTable;
        [Property(0x5DB0B6E8, 31)] public bool m_disableTimer;

        // HASH : 0x7DB09CC1
        // SIZE : 191 bits
        // EXAM : StartZone
        // Very confident about the type.
        // This property is shared with the Trigger class.
        [Property(0x7DB09CC1, 31)] public List<ByteString>? unknown_5;

        // HASH : 0x71FCB022
        // SIZE : 65 bits
        // Very confident about the type.
        [Property(0x71FCB022, 31)] public bool m_unknown_boolean_1;

        // HASH : 0x3BF5B2D
        // SIZE : 65 bits
        // Very confident about the type.
        [Property(0x3BF5B2D, 31)] public bool m_unknown_boolean_2;

        // HASH : 0x3C345132
        // SIZE : 72 bits
        [Property(0x3C345132, 31)] public bool m_unknown_boolean_3;

        // HASH : 0x61B4E11E
        // SIZE : 72 bits
        [Property(0x61B4E11E, 31)] public bool m_unknown_boolean_4;

        // HASH : 0x62A2160A
        // SIZE : 8
        // This property is shared with the Trigger class.
        [Property(0x62A2160A, 31)] public uint m_unknown_uint_1;

        // HASH : 0x37BEB1CF
        // SIZE : 8
        [Property(0x37BEB1CF, 31)] public int m_unknown_uint_2;

        // HASH : 0x62794D39
        // SIZE : 8
        [Property(0x62794D39, 31)] public uint m_unknown_uint_3;

        // HASH : 0x6FA14D24
        // SIZE : 8
        [Property(0x6FA14D24, 31)] public uint m_unknown_uint_4;
    }
}
