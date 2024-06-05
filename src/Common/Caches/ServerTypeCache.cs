/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Diagnostics;
using Akka.Util;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
using static Imlight.Common.Caches.TypeCache;
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
    public static PropertyClass? Dispatch(uint hash) => hash switch {
        0x06DAAC43 => new WizZoneTriggers(),
        0x068C265B => new Trigger(),
        0x1B6EF770 => new WizZoneVolumes(),
        0x1B7B55F6 => new Volume(),
        0x774C0B33 => new ResDisplayText(),
        0x3C626744 => new ResPlaySound(),
        0xDa51FA8 => new ZoneRouter(),
        478486736 => new CombatSigil(),
        16312488 => new ResPlayCinematic(),
        338444955 => new ResActorDialog(),
        703672453 => new ResAddGold(),
        1320311385 => new ResAddMagicXP(),
        475964190 => new ResLoot(),
        1936095675 => new ResPostQuestEvent(),
        1774023420 => new ResAddSpell(),
        1627552040 => new ResAddTrainingPoints(),
        185712126 => new ResModifyEntry(),
        461317711 => new ResIncrementEntry(),
        1202940500 => new ResAddMissionDoor(),
        1989908347 => new ResItemLoot(),
        1875038822 => new ResAddMaxGold(),
        822278902 => new ResDeleteItem(),
        794463279 => new ResAddHealth(),
        412163800 => new ResPostEvent(),
        552459800 => new ResRemoveDynaMod(),
        3938346 => new ResAddDynaMod(),
        526762782 => new ResWait(),
        475497368 => new ResEmote(),
        798482874 => new ResAddEncounterXP(),
        1534247075 => new ResMarkZoneNoWarn(),
        807468757 => new ResDownloadPackage(),
        1787561491 => new ResAddTreasureSpell(),
        1263108441 => new ResModifyTriggerObject(),
        723600258 => new ResSpawn(),
        682171658 => new ResReInteract(),
        101365432 => new ResDownloadElement(),
        803366521 => new ResRemoveTriggerObject(),
        1279893472 => new ResCompleteQuestGoal(),
        2041850521 => new ResAddBadge(),
        798724538 => new ResAddEncounter(),
        1383450208 => new ResDespawn(),
        1874483934 => new ResRemoveEntry(),
        1962253934 => new ResFallthrough(),
        1471095109 => new ResultOption(),
        309059205 => new ResMaxPotions(),
        1714192944 => new ResRemoveMissionDoor(),
        633964307 => new ResAddCraftingSlot(),
        1826857186 => new ResTimeStampEntry(),
        1383058250 => new ResToggleQuestEffect(),
        577427349 => new ResDespawnLeashedObject(),
        837967761 => new ResRemoveEffect(),
        1505576005 => new ResSpawnLeashedObject(),
        53623319 => new ResAddEffect(),
        1893729846 => new ResAddTriggerObject(),
        228794493 => new ResTeleport(),
        1375195018 => new ResSetPips(),
        1100956089 => new ResClearHand(),
        151650171 => new ResGiveSpell(),
        266932982 => new ResUpdatePips(),
        919520188 => new ResSyncScript(),
        43418311 => new ResAddEnergy(),
        66638275 => new ResAddElixir(),
        1254124953 => new ResGivePowerPip(),
        875336888 => new ResSetGardeningLevel(),
        2128108307 => new ResDownloadBrowser(),
        638582544 => new ResSetFishingLevel(),
        1980688359 => new ResAddMana(),
        1887868329 => new ResClearSpellbook(),
        1952853362 => new ResClearExperience(),
        742393364 => new ResShowGUI(),
        82637767 => new ResCinematic(),
        1895914322 => new ResAddRecipe(),
        1144211986 => new ResControlBackgroundMusic(),
        513283589 => new ResUnlockShadowMagic(),
        145615551 => new ResStartStagedCinematic(),
        74783451 => new ResReduceMana(),
        1486342711 => new ResInitiateCombat(),
        _ => null
    };

    public class WizZoneTriggers : PropertyClass {
        public override uint GetHash() => 0x06DAAC43;

        [Property(0x3F1DB764, 31)] public List<Trigger>? m_triggers;
    }

    [DebuggerDisplay("{m_triggerName}")]
    public class Trigger : PropertyClass {
        public override uint GetHash() => 0x068C265B;

        [Property(0xB8C90C10, 31)] public ByteString m_triggerName;
        [Property(0x3933D634, 31)] public uint m_triggerMax;
        [Property(0x767AAC3C, 31)] public uint m_cooldown;
        [Property(0x2E8B9981, 31)] public uint m_cooldownRand;
        [Property(0x3282D78A, 31)] public bool m_pulsar;
        [Property(0x7DB09CC1, 31)] public List<ByteString>? m_activateEvents;
        [Property(0xA7BEADF6, 31)] public List<ByteString>? m_fireEvents;
        [Property(0x62A2160A, 31)] public List<ByteString>? m_deactivateEvents;
        [Property(0x5C548D5F, 31)] public List<ByteString>? m_unknown;
        [Property(0xA955FFA6, 31)] public RequirementList? m_requirements;
        [Property(0xE11C8ADA, 31)] public ResultList? m_results;
        [Property(0x794EA0DF, 31)] public uint unknown_uint_3;      // ??
        [Property(0x88B9D287, 31)] public ByteString unknown_str_3; // this is probably a list or string
        [Property(0x8177DA98, 31)] public TriggerObjectInfo m_triggerObjInfo;
    }

    // Don't look at me, this is what the game uses.
    public class TriggerObjectBase : CoreObjectInfo {
        // todo: need actual hashes here
        public override uint GetHash() => 0x068C265B;

        // Why would a trigger have a location? Isn't this what volumes are for?
        [Property(0x7DB3F828, 31)] public float m_locationX;
        [Property(0x7DB3F829, 31)] public float m_locationY;
        [Property(0x7DB3F82A, 31)] public float m_locationZ;
    }

    public class TriggerObjectInfo : TriggerObjectBase {
        // todo: need actual hashes here
        public override uint GetHash() => 0x068C265B;
    }

    public class WizZoneVolumes : PropertyClass {
        public override uint GetHash() => 0x1B6EF770;

        [Property(0x884BFB48, 31)] public List<Volume>? m_volumes;
    }

    [DebuggerDisplay("{m_volumeName}")]
    public class Volume : CoreObjectInfo {
        public override uint GetHash() => 0x1B7B55F6;

        // CoreObjectInfo properties end here.
        [Property(0xC6E6048B, 31)] public ByteString m_volumeName;
        [Property(0x7DB3F828, 31)] public float m_locationX;
        [Property(0x7DB3F829, 31)] public float m_locationY;
        [Property(0x7DB3F82A, 31)] public float m_locationZ;
        [Property(0x40183401, 31)] public new ulong m_templateID; // Yes, this is a duplicate property.
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
        [Property(0x3B9498D7, 31)] public bool m_blocking;
        [Property(0x2C2BC314, 31)] public float m_reinteractTime;

        // Live server says MSG_PLAYSOUND with ID `89062548015657`
        // Client data says this trigger should play "WC_ShopB_Bell_01"
        // How are we supposed to know what sound to play?

        [Property(0x1D70805C, 31)] public bool m_unknown_bool_1;  // Size: 65 bits
        [Property(0x3B657FD7, 31)] public ulong m_unknown_bool_2; // Size: 103 bits (~13 bytes)
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
            ROUTING_ZONE,
            ROUTING_PROXIMITY,
        }
    }

    public class ResPlayCinematic : TypeCache.Result {
        public override uint GetHash() => 16312488;

        [Property(2611527497, 134217735)] public ByteString m_cinematicName;
        [Property(0x444373FA, 31)] public ZoneRouter? m_router;
        [Property(0x1D70805C, 31)] public bool m_unknown_bool_1;
        [Property(0x3AAF6E2F, 31)] public bool m_unknown_bool_2;
        [Property(0x61436E16, 31)] public bool m_unknown_bool_3;
        [Property(0x78B7B1EE, 31)] public ByteString m_unknown_string_1;
        [Property(0x3C1B4C58, 31)] public bool m_unknown_bool_4;
        [Property(0x5BB196FF, 31)] public bool m_unknown_bool_5;
        [Property(0x7B00E397, 31)] public ByteString m_unknown_string_2;
        [Property(0x197BBD69, 31)] public bool m_unknown_bool_6;
        [Property(0x4FA58BBA, 31)] public float m_unknown_float_1;
        [Property(0x3DAC4C0A, 31)] public bool m_unknown_bool_7;
        [Property(0x66ECE9B3, 31)] public ByteString m_unknown_string_3;
        [Property(0xA4092DFC, 31)] public bool m_unknown_bool_8;
        [Property(0x61437E16, 31)] public bool m_unknown_bool_9;
    }

    public class ResActorDialog : TypeCache.Result {
        public override uint GetHash() => 338444955;

        // Property: m_dialogPrefix (unknown)

        [Property(1972573231, 7)] public ByteString m_activePersona;
        [Property(3056380390, 7)] public ByteString m_registryEntry;
        [Property(1310859212, 7)] public ActorDialog m_dialog;
        [Property(2305471533, 31)] public ByteString m_quest;
        [Property(2057644325, 7)] public bool m_broadcastToZone;
        [Property(482239118, 7)] public bool m_displayInQuestList;
        [Property(707400068, 7)] public bool m_oneShot;
    }

    public class ResAddGold : TypeCache.Result {
        public override uint GetHash() => 703672453;

        [Property(219423808, 7)] public int m_gold;
        [Property(2305508654, 7)] public ByteString m_sourceType;
    }

    public class ResAddMagicXP : TypeCache.Result {
        public override uint GetHash() => 1320311385;

        // Property: m_experience (int)
        // Property: m_magicSchool (string)
        [Property(2305508654, 7)] public ByteString m_sourceType;
    }

    public class ResLoot : TypeCache.Result {
        public override uint GetHash() => 475964190;

        // Property: m_lootTable (string)
    }

    public class ResPostQuestEvent : TypeCache.Result {
        public override uint GetHash() => 1936095675;

        [Property(3493260286, 7)] public ByteString m_eventName;
        // Property: m_subEventName (unknown)
    }

    public class ResAddSpell : TypeCache.Result {
        public override uint GetHash() => 1774023420;

        // Property: m_spellName (string)
    }

    public class ResAddTrainingPoints : TypeCache.Result {
        public override uint GetHash() => 1627552040;

        // Property: m_sourceType (string)
        // Property: m_trainingPoints (int)
    }

    public class ResModifyEntry : TypeCache.Result {
        public override uint GetHash() => 185712126;

        // Property: m_questName (unknown)

        [Property(2055270734, 7)] public ByteString m_entryName;
        [Property(1388902362, 7)] public bool m_isQuestRegistry;
        [Property(812990455, 7)] public int m_value;
        [Property(1702112846, 7)] public ByteString m_questName;
    }

    public class ResIncrementEntry : TypeCache.Result {
        public override uint GetHash() => 461317711;

        // Property: m_entryName (string)
        // Property: m_isQuestRegistry (int)
        // Property: m_questName (unknown)
    }

    public class ResAddMissionDoor : TypeCache.Result {
        public override uint GetHash() => 1202940500;

        // Property: m_advanced (int)
        // Property: m_clientTag (unknown)
        // Property: m_missionDoorLoc (unknown)
        // Property: m_missionDoorTag (string)
        // Property: m_missionDoorZone (unknown)
        // Property: m_useQuestAsOriginator (int)
    }

    public class ResItemLoot : TypeCache.Result {
        public override uint GetHash() => 1989908347;

        // Property: m_itemTemplateID (int)
        // Property: m_lootOptions (string)
        // Property: m_sendLootMessage (int)
        // Property: m_sourceType (string)
    }

    public class ResAddMaxGold : TypeCache.Result {
        public override uint GetHash() => 1875038822;

        // Property: m_maxGoldToAdd (int)
    }

    public class ResDeleteItem : TypeCache.Result {
        public override uint GetHash() => 822278902;

        // Property: m_itemTemplateID (int)
        // Property: m_quantity (int)
        // Property: m_sourceType (string)
    }

    public class ResAddHealth : TypeCache.Result {
        public override uint GetHash() => 794463279;

        // Property: m_healthFlat (int)
        // Property: m_healthPercent (float)
        // Property: m_useFlat (int)
    }

    public class ResPostEvent : TypeCache.Result {
        public override uint GetHash() => 412163800;

        // Property: m_eventName (string)
        // Property: m_subEventName (unknown) // Deserialization fails if this is made a string. 72 bits

        [Property(3493260286, 31)] public string m_eventName;
    }

    public class ResRemoveDynaMod : TypeCache.Result {
        public override uint GetHash() => 552459800;

        [Property(1601665858, 31)] public ByteString m_dynaModClientTag;
        [Property(1110452292, 31)] public bool m_useQuestAsOriginator;
    }

    public class ResAddDynaMod : TypeCache.Result {
        public override uint GetHash() => 3938346;

        [Property(1601665858, 31)] public ByteString m_dynaModClientTag;
        [Property(1110452292, 31)] public bool m_dynaModRemove;
        [Property(1494302749, 31)] public bool m_useQuestAsOriginator;
        [Property(2072855560, 31)] public ByteString m_dynaModState;
        [Property(2171167736, 8388615)] public ByteString m_zoneName;
    }

    public class ResWait : TypeCache.Result {
        public override uint GetHash() => 526762782;

        [Property(2403101108, 31)] public uint m_secondsToWait; // 128 bits
    }

    public class ResEmote : TypeCache.Result {
        public override uint GetHash() => 475497368;

        // Property: m_emoteName (string)
        // Property: m_emoteState (string)
        // Property: m_loop (int)
        // Property: m_particleAsset (unknown)
        // Property: m_particleNode (unknown)
        // Property: m_personaName (unknown)
        // Property: m_soundAsset (unknown)
        // Property: m_usePersona (int)
        // Property: m_useTarget (int)
    }

    public class ResAddEncounterXP : TypeCache.Result {
        public override uint GetHash() => 798482874;

        // Property: m_experience (int)
    }

    public class ResMarkZoneNoWarn : TypeCache.Result {
        public override uint GetHash() => 1534247075;

    }

    public class ResDownloadPackage : TypeCache.Result {
        public override uint GetHash() => 807468757;

        [Property(2325737891, 31)] public List<ByteString> m_packageList;
    }

    public class ResAddTreasureSpell : TypeCache.Result {
        public override uint GetHash() => 1787561491;

        // Property: m_sourceType (string)
        // Property: m_spellName (string)
    }

    public class ResModifyTriggerObject : TypeCache.Result {
        public override uint GetHash() => 1263108441;

        [Property(3336963211, 31)] public ByteString m_triggerObjName;
        [Property(2067625387, 31)] public bool m_triggerObjState;
    }

    public class ResSpawn : TypeCache.Result {
        public override uint GetHash() => 723600258;

        // Not confident about these types
        [Property(1481718190, 31)] public ulong m_spawnID;  // 128 bits
        [Property(142527940, 31)] public bool m_activate; // 65 bits
    }

    public class ResReInteract : TypeCache.Result {
        public override uint GetHash() => 682171658;

        // Property: m_actorType (string)
        // Property: m_delay (int)
        // Property: m_personaName (string)
        // Property: m_source (int)
    }

    public class ResDownloadElement : TypeCache.Result {
        public override uint GetHash() => 101365432;

        // Property: m_elementPackageList (string)
    }

    public class ResRemoveTriggerObject : TypeCache.Result {
        public override uint GetHash() => 803366521;

        // Property: m_triggerObjName (string)
    }

    public class ResCompleteQuestGoal : TypeCache.Result {
        public override uint GetHash() => 1279893472;

        // Property: m_goalName (string)
        // Property: m_questName (string)
    }

    public class ResAddBadge : TypeCache.Result {
        public override uint GetHash() => 2041850521;

        // Property: m_badgeName (string)
    }

    public class ResAddEncounter : TypeCache.Result {
        public override uint GetHash() => 798724538;

        // Property: m_experience (int)
    }

    public class ResDespawn : TypeCache.Result {
        public override uint GetHash() => 1383450208;

        // Property: m_despawnEffect (unknown) // uint perhaps?
        // Property: m_spawnID (int)
        // Property: m_templateID (int)
    }

    public class ResRemoveEntry : TypeCache.Result {
        public override uint GetHash() => 1874483934;

        // Property: m_entryName (string)
        // Property: m_isQuestRegistry (int)
        // Property: m_questName (unknown)
    }

    public class ResCinematicActor : TypeCache.Result {
        public override uint GetHash() => 16312488;

        // Property: m_blocking (int)
        // Property: m_cinematicName (string)
        // Property: m_endAtActor (int)
        // Property: m_endAtTargetActor (int)
        // Property: m_endLoc (string)
        // Property: m_objectTemplateID (int)
        // Property: m_router (string)
        // Property: m_routing (string)
        // Property: m_startAtActor (int)
        // Property: m_startAtTargetActor (int)
        // Property: m_startLoc (string)
        // Property: m_unique (int)
        // Property: m_uniqueBusyMsg (unknown)
        // Property: m_uniqueName (unknown)
    }

    public class ResFallthrough : TypeCache.Result {
        public override uint GetHash() => 1962253934;

        // Property: m_options (string)
    }

    public class ResultOption : TypeCache.Result {
        public override uint GetHash() => 1471095109;

        // Property: m_requirements (string)
        // Property: m_results (string)
    }

    public class ResMaxPotions : TypeCache.Result {
        public override uint GetHash() => 309059205;

        // Property: m_potionsToAdd (int)
        // Property: m_sourceType (string)
    }

    public class ResRemoveMissionDoor : TypeCache.Result {
        public override uint GetHash() => 1714192944;

        // Property: m_missionDoorTag (string)
        // Property: m_useQuestAsOriginator (int)
    }

    public class ResAddCraftingSlot : TypeCache.Result {
        public override uint GetHash() => 633964307;

        // Property: m_slotDelta (int)
        // Property: m_sourceType (string)
    }

    public class ResTimeStampEntry : TypeCache.Result {
        public override uint GetHash() => 1826857186;

        // Property: m_coolDownTime (int)
        // Property: m_registryEntry (string)
    }

    public class ResToggleQuestEffect : TypeCache.Result {
        public override uint GetHash() => 1383058250;

        // Property: m_addEffect (int)
        // Property: m_effectName (string)
    }

    public class ResDespawnLeashedObject : TypeCache.Result {
        public override uint GetHash() => 577427349;

        // Property: m_followerTemplateID (int)
    }

    public class ResRemoveEffect : TypeCache.Result {
        public override uint GetHash() => 837967761;

        // Property: m_effectName (string)
        // Property: m_useOriginatorID (int)
    }

    public class ResSpawnLeashedObject : TypeCache.Result {
        public override uint GetHash() => 1505576005;

        // Property: m_followerTemplateID (int)
    }

    public class ResAddEffect : TypeCache.Result {
        public override uint GetHash() => 53623319;

        // Property: m_effectName (unknown)
        // Property: m_playerOnly (int)
        // Property: m_spEffectInfo (string)
    }

    public class ResAddTriggerObject : TypeCache.Result {
        public override uint GetHash() => 1893729846;

        // Property: m_triggerObjName (string)
        // Property: m_triggerObjState (string)
    }

    public class ResSetPips : TypeCache.Result {
        public override uint GetHash() => 1375195018;

        // Property: m_numPips (int)
        // Property: m_subCircle (int)
    }

    public class ResClearHand : TypeCache.Result {
        public override uint GetHash() => 1100956089;

        // Property: m_subCircle (int)
    }

    public class ResGiveSpell : TypeCache.Result {
        public override uint GetHash() => 151650171;

        // Property: m_spellName (string)
        // Property: m_subCircle (int)
    }

    public class ResUpdatePips : TypeCache.Result {
        public override uint GetHash() => 266932982;

    }

    public class ResSyncScript : TypeCache.Result {
        public override uint GetHash() => 919520188;

        // Property: m_function (string)
        // Property: m_script (string)
    }

    public class ResAddEnergy : TypeCache.Result {
        public override uint GetHash() => 43418311;

        // Property: m_energyFlat (int)
        // Property: m_energyPercent (int)
        // Property: m_sourceType (string)
        // Property: m_useFlat (int)
    }

    public class ResAddElixir : TypeCache.Result {
        public override uint GetHash() => 66638275;

        // Property: m_sourceType (string)
        // Property: m_templateID (int)
    }

    public class ResGivePowerPip : TypeCache.Result {
        public override uint GetHash() => 1254124953;

        // Property: m_subCircle (int)
    }

    public class ResSetGardeningLevel : TypeCache.Result {
        public override uint GetHash() => 875336888;

        // Property: m_level (int)
    }

    public class ResDownloadBrowser : TypeCache.Result {
        public override uint GetHash() => 2128108307;

    }

    public class ResSetFishingLevel : TypeCache.Result {
        public override uint GetHash() => 638582544;

        // Property: m_level (int)
    }

    public class ResAddMana : TypeCache.Result {
        public override uint GetHash() => 1980688359;

        [Property(2305508654, 7)] public ByteString m_sourceType;
        [Property(1926272286, 7)] public int m_manaFlat;
        [Property(1293619909, 7)] public float m_manaPercent;
        [Property(138922614, 7)] public int m_overfill;
        [Property(2040055687, 7)] public bool m_useFlat;
    }

    public class ResClearSpellbook : TypeCache.Result {
        public override uint GetHash() => 1887868329;

    }

    public class ResClearExperience : TypeCache.Result {
        public override uint GetHash() => 1952853362;

    }

    public class ResShowGUI : TypeCache.Result {
        public override uint GetHash() => 742393364;

        [Property(2274463339, 31)] public ByteString m_guiDisplay;
        [Property(2717603296, 31)] public ByteString m_guiFile;
    }

    public class ResCinematic : TypeCache.Result {
        public override uint GetHash() => 82637767;

        // Property: m_blocking (int)
        // Property: m_cinematicName (string)
        // Property: m_endAtActor (int)
        // Property: m_endAtTargetActor (int)
        // Property: m_endLoc (string)
        // Property: m_objectTemplateID (int)
        // Property: m_router (string)
        // Property: m_routing (string)
        // Property: m_startAtActor (int)
        // Property: m_startAtTargetActor (int)
        // Property: m_startLoc (string)
        // Property: m_unique (int)
        // Property: m_uniqueBusyMsg (unknown)
        // Property: m_uniqueName (unknown)
    }

    public class ResAddRecipe : TypeCache.Result {
        public override uint GetHash() => 1895914322;

        // Property: m_recipeName (string)
        // Property: m_sourceType (string)
    }

    public class ResClientNotifyText : TypeCache.Result {
        public override uint GetHash() => 2001472307;

        // Property: m_allInZone (int)
        // Property: m_text (string)
        // Property: m_type (int)
    }

    public class ResControlBackgroundMusic : TypeCache.Result {
        public override uint GetHash() => 1144211986;

        // Property: m_action (string)
        // Property: m_fadeTime (int)
        // Property: m_router (string)

        [Property(1734553689, 31)] public ByteString m_action;
    }

    public class ResUnlockShadowMagic : TypeCache.Result {
        public override uint GetHash() => 513283589;

    }

    public class ResStartStagedCinematic : TypeCache.Result {
        public override uint GetHash() => 145615551;

        // Property: m_bIncludeAllPlayersInZone (int)
        // Property: m_cinematicName (string)
        // Property: m_stageName (string)
    }

    public class ResReduceMana : TypeCache.Result {
        public override uint GetHash() => 74783451;

        // Property: m_manaPercent (float)
    }

    public class ResInitiateCombat : TypeCache.Result {
        public override uint GetHash() => 1486342711;

        // Property: m_aggroActor (int)
        // Property: m_aggroRadius (int)
        // Property: m_aggroTarget (int)
        // Property: m_allPlayers (bool)
        // Property: m_sigilLabel (string)
    }

    public class CombatSigil : CoreObjectInfo {
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
