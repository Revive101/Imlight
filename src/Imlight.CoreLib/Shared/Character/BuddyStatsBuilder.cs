/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * BUDDY STATS BUILDER
 * ========================================================================
 * 
 * PURPOSE:
 * Builds the MSG_BUDDYSTATS reply for the character window: stats, avatar, equipment,
 * effects, pet and wishlist blocks, each left out when the client's checksum shows it
 * already holds that block.
 * 
 * USAGE EXAMPLE:
 * SendToSocket(BuddyStatsBuilder.Build(buddy, request, targetCharacterGid: 0));
 * 
 * NOTE:
 * A reply block checksum of 1 means "block attached", 0 means "keep your cache". The pet
 * checksum instead carries the pet's item template ID when the pet block is attached, and
 * 1 when the client's cached pet is current.
 *
 * TODO:
 * - Sockets are not modeled yet, so PetJewelID and jewel info on public items are always empty.
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Character;

internal static class BuddyStatsBuilder {

    // SerializeFlags | CompactLength | StringEnums | Compress, as live sends and the client caches every block.
    internal const uint BlockSerializerFlags = 0x0F;

    private const uint BlockAttached = 1;
    private const uint EquipmentBehaviorTemplateNameId = 0x4867764C;
    private const uint DefaultShowPvpOption = 1;
    private const PropertyFlags TransmitMask = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;

    // Live names these two blocks after the server-side behaviors.
    private static readonly uint s_equipmentWireHash = StringHash.Compute("class WizEquipmentBehavior");
    private static readonly uint s_petWireHash = StringHash.Compute("class PetItemBehavior");

    // Magic, version, an unknown 16-bit 1, then a 16-bit entry count of 0.
    private static readonly byte[] s_emptyWishlist = [
        0xE7, 0xAC, 0xCE, 0xFA, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
    ];

    internal static GAME_5_PROTOCOL.MSG_BUDDYSTATS Build(Wizard buddy,
                                                         GAME_5_PROTOCOL.MSG_BUDDYSTATS request,
                                                         ulong targetCharacterGid)
        => Build(CollectSource(buddy), request, targetCharacterGid);

    internal static GAME_5_PROTOCOL.MSG_BUDDYSTATS Build(BuddyStatsSource source,
                                                         GAME_5_PROTOCOL.MSG_BUDDYSTATS request,
                                                         ulong targetCharacterGid) {
        var reply = new GAME_5_PROTOCOL.MSG_BUDDYSTATS {
            BuddyID = request.BuddyID,
            Level = source.Level,
            School = source.School,
            ShowPVPOption = source.Stats.m_showPVPOption,
            TargetCharacterGID = targetCharacterGid,
        };

        // The client's stats checksum covers padding bits it never initializes, so stats are always attached.
        if (TrySerializeBlock(source.Stats, TransmitMask, null, out var statBlock)) {
            reply.StatBlock = statBlock;
            reply.StatBlockCRC = BlockAttached;
        }

        AttachCharacterBlock(reply, source.Avatar, request);
        AttachEquipmentBlock(reply, source.Equipment, request);
        AttachEffectBlock(reply, source.Effects, request);
        AttachPetBlock(reply, source, request);
        AttachWishlistBlock(reply, request);

        return reply;
    }

    internal static BuddyStatsSource CollectSource(Wizard buddy) {
        var equipmentBehavior = buddy.EquipmentBehavior;
        var hasEquipment = equipmentBehavior?.SlotList is not null && equipmentBehavior.EquippedItems is not null;
        var equipment = new ClientWizEquipmentBehavior {
            m_behaviorTemplateNameID = EquipmentBehaviorTemplateNameId,
            m_publicItemList = hasEquipment ? CharacterHelper.GetEquipmentList(equipmentBehavior).m_infoList : [],
        };

        var petItem = hasEquipment ? equipmentBehavior.GetItemInSlot(EquipmentSlotType.Pet) : null;
        ClientPetItemBehavior pet = null;
        if (petItem is not null) {
            CoreObjectFactory.FindBehaviorInstance(petItem, out pet);
        }

        return new BuddyStatsSource {
            Stats = GetStatBlock(buddy),
            Avatar = buddy.WizardAvatar,
            Equipment = equipment,
            Effects = buddy.GameEffects ?? [],
            Pet = pet,
            PetTemplateId = (uint) (petItem?.m_templateID ?? 0),
            Level = (uint) buddy.MagicSchoolBehavior.Level,
            School = (uint) buddy.MagicSchoolBehavior.MagicSchool,
        };
    }

    internal static byte[] WrapBlock(byte[] objectBytes, uint serializerFlags, byte compressionFlagByte = 0) {
        // The client only sets bit 0 of the flag byte; the rest keeps whatever was in its buffer.
        var compressed = Deflate(objectBytes);
        var isCompressed = compressed.Length > 0 && compressed.Length < objectBytes.Length;

        using var block = new MemoryStream();
        Span<byte> word = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(word, serializerFlags);
        block.Write(word);
        block.WriteByte((byte) ((compressionFlagByte & ~1) | (isCompressed ? 1 : 0)));

        if (isCompressed) {
            BinaryPrimitives.WriteInt32LittleEndian(word, objectBytes.Length);
            block.Write(word);
            block.Write(compressed);
        } else {
            block.Write(objectBytes);
        }

        return block.ToArray();
    }

    private static void AttachCharacterBlock(GAME_5_PROTOCOL.MSG_BUDDYSTATS reply,
                                             WizardCharacterBehavior avatar,
                                             GAME_5_PROTOCOL.MSG_BUDDYSTATS request) {
        if (avatar is null) {
            return;
        }

        reply.Gender = (sbyte) avatar.m_eGender;
        if (!TrySerializeObject(avatar, TransmitMask, null, out var avatarObject)
            || BuddyStatsCrc.ForCharacter(avatarObject) == request.CharBlockCRC) {
            return;
        }

        reply.CharBlock = WrapBlock(avatarObject, BlockSerializerFlags);
        reply.CharBlockCRC = BlockAttached;
    }

    private static void AttachEquipmentBlock(GAME_5_PROTOCOL.MSG_BUDDYSTATS reply,
                                             ClientWizEquipmentBehavior equipment,
                                             GAME_5_PROTOCOL.MSG_BUDDYSTATS request) {
        if (BuddyStatsCrc.TryForEquipment(equipment.m_publicItemList, out var equipmentCrc)
            && equipmentCrc == request.EquipBlockCRC) {
            return;
        }

        if (TrySerializeBlock(equipment, PropertyFlags.Prop_Public, s_equipmentWireHash, out var equipBlock)) {
            reply.EquipBlock = equipBlock;
            reply.EquipBlockCRC = BlockAttached;
        }
    }

    private static void AttachEffectBlock(GAME_5_PROTOCOL.MSG_BUDDYSTATS reply,
                                          List<GameEffectBase> effects,
                                          GAME_5_PROTOCOL.MSG_BUDDYSTATS request) {
        // A zero checksum from the client can also mean it has no effect cache at all.
        var clientIsCurrent = BuddyStatsCrc.ForEffects(effects) == request.EffectBlockCRC
            && (request.EffectBlockCRC != 0 || effects.Count == 0);
        if (clientIsCurrent) {
            return;
        }

        var container = new GameEffectContainer {
            m_publicEffects = [.. effects],
            m_myEffects = [],
        };

        if (TrySerializeBlock(container, TransmitMask, null, out var effectBlock)) {
            reply.EffectBlock = effectBlock;
            reply.EffectBlockCRC = BlockAttached;
        }
    }

    private static void AttachPetBlock(GAME_5_PROTOCOL.MSG_BUDDYSTATS reply,
                                       BuddyStatsSource source,
                                       GAME_5_PROTOCOL.MSG_BUDDYSTATS request) {
        if (source.Pet is null) {
            return;
        }

        if (request.PetStatBlockCRC != 0 && BuddyStatsCrc.ForPet(source.Pet) == request.PetStatBlockCRC) {
            reply.PetStatBlockCRC = BlockAttached;
            return;
        }

        if (TrySerializeBlock(source.Pet, TransmitMask, s_petWireHash, out var petBlock)) {
            reply.PetStatBlock = petBlock;
            reply.PetStatBlockCRC = source.PetTemplateId;
        }
    }

    private static void AttachWishlistBlock(GAME_5_PROTOCOL.MSG_BUDDYSTATS reply,
                                            GAME_5_PROTOCOL.MSG_BUDDYSTATS request) {
        if (BuddyStatsCrc.ForWishlist(s_emptyWishlist) == request.WishlistBlockCRC) {
            return;
        }

        reply.WishlistBlock = s_emptyWishlist;
        reply.WishlistBlockCRC = BlockAttached;
    }

    private static WizGameStats GetStatBlock(Wizard buddy) {
        // The client-side copy only carries base stats; the combat copy adds what equipped items grant.
        var serverStats = buddy.GameStats;
        var combatStats = serverStats.GetCombatGameStats();

        // todo: live reports 1 for every player, and nothing sets this option yet. Is 0 a real value?
        var showPvpOption = serverStats.m_showPVPOption == 0 ? DefaultShowPvpOption : serverStats.m_showPVPOption;

        return serverStats.GetClientTypeAlternative() with {
            m_baseHitpoints = combatStats.m_baseHitpoints,
            m_baseMana = combatStats.m_baseMana,
            m_dmgBonusPercent = combatStats.m_dmgBonusPercent,
            m_dmgBonusFlat = combatStats.m_dmgBonusFlat,
            m_accBonusPercent = combatStats.m_accBonusPercent,
            m_apBonusPercent = combatStats.m_apBonusPercent,
            m_dmgReducePercent = combatStats.m_dmgReducePercent,
            m_dmgReduceFlat = combatStats.m_dmgReduceFlat,
            m_accReducePercent = combatStats.m_accReducePercent,
            m_healBonusPercent = combatStats.m_healBonusPercent,
            m_healIncBonusPercent = combatStats.m_healIncBonusPercent,
            m_spellChargeBonus = combatStats.m_spellChargeBonus,
            m_dmgBonusPercentAll = combatStats.m_dmgBonusPercentAll,
            m_dmgBonusFlatAll = combatStats.m_dmgBonusFlatAll,
            m_accBonusPercentAll = combatStats.m_accBonusPercentAll,
            m_apBonusPercentAll = combatStats.m_apBonusPercentAll,
            m_dmgReducePercentAll = combatStats.m_dmgReducePercentAll,
            m_dmgReduceFlatAll = combatStats.m_dmgReduceFlatAll,
            m_accReducePercentAll = combatStats.m_accReducePercentAll,
            m_healBonusPercentAll = combatStats.m_healBonusPercentAll,
            m_healIncBonusPercentAll = combatStats.m_healIncBonusPercentAll,
            m_spellChargeBonusAll = combatStats.m_spellChargeBonusAll,
            m_powerPipBase = combatStats.m_powerPipBase,
            m_powerPipBonusPercentAll = combatStats.m_powerPipBonusPercentAll,
            m_xpPercentIncrease = combatStats.m_xpPercentIncrease,
            m_criticalHitPercentBySchool = combatStats.m_criticalHitPercentBySchool,
            m_blockPercentBySchool = combatStats.m_blockPercentBySchool,
            m_criticalHitRatingBySchool = combatStats.m_criticalHitRatingBySchool,
            m_blockRatingBySchool = combatStats.m_blockRatingBySchool,
            m_balanceMastery = serverStats.m_balanceMastery,
            m_deathMastery = serverStats.m_deathMastery,
            m_fireMastery = serverStats.m_fireMastery,
            m_iceMastery = serverStats.m_iceMastery,
            m_lifeMastery = serverStats.m_lifeMastery,
            m_mythMastery = serverStats.m_mythMastery,
            m_stormMastery = serverStats.m_stormMastery,
            m_schoolID = (uint) buddy.MagicSchoolBehavior.MagicSchool,
            m_currentZoneName = buddy.Zone ?? string.Empty,
            m_showPVPOption = showPvpOption,
        };
    }

    private static bool TrySerializeBlock(PropertyClass block,
                                          PropertyFlags mask,
                                          uint? wireHash,
                                          out ByteString output) {
        output = default;
        if (!TrySerializeObject(block, mask, wireHash, out var objectBytes)) {
            return false;
        }

        output = WrapBlock(objectBytes, BlockSerializerFlags);

        return true;
    }

    private static bool TrySerializeObject(PropertyClass block,
                                           PropertyFlags mask,
                                           uint? wireHash,
                                           out byte[] objectBytes) {
        objectBytes = [];

        // A serializer instance keeps its mask between calls, so one is created per block.
        var serializer = new ObjectSerializer(Versionable: false,
                                              Behaviors: SerializerFlags.CompactLength | SerializerFlags.StringEnums);
        if (!serializer.Serialize(block, mask, out var serialized)) {
            Logger.Error("Could not serialize a {Block} buddy stats block.", Logger.Args(block.GetType().Name));

            return false;
        }

        objectBytes = serialized;
        if (wireHash is { } hash) {
            BinaryPrimitives.WriteUInt32LittleEndian(objectBytes, hash);
        }

        return true;
    }

    private static byte[] Deflate(byte[] data) {
        // Level 6 reproduces the client's zlib output byte for byte.
        using var compressed = new MemoryStream();
        using (var zlib = new Ionic.Zlib.ZlibStream(compressed,
                                                    Ionic.Zlib.CompressionMode.Compress,
                                                    Ionic.Zlib.CompressionLevel.Default,
                                                    leaveOpen: true)) {
            zlib.Write(data, 0, data.Length);
        }

        return compressed.ToArray();
    }

}

internal sealed class BuddyStatsSource {

    public WizGameStats Stats { get; init; }
    public WizardCharacterBehavior Avatar { get; init; }
    public ClientWizEquipmentBehavior Equipment { get; init; }
    public List<GameEffectBase> Effects { get; init; } = [];
    public ClientPetItemBehavior Pet { get; init; }
    public uint PetTemplateId { get; init; }
    public uint Level { get; init; }
    public uint School { get; init; }

}
