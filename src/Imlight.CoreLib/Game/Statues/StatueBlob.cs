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
 * STATUE BLOB
 * ========================================================================
 *
 * PURPOSE:
 * Builds the visual blob the client's own pedestal statue draws a wizard from.
 *
 * USAGE EXAMPLE:
 * StatueLook.Apply stores StatueBlob.TryBuild's result in the pedestal behavior's blob list.
 *
 * NOTE:
 * The blob is a compressed stitch blob whose avatar and equipment buffers are raw object streams.
 * The name travels as packed name keys, so a pedestal can never show free text.
 *
 * TODO:
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Buffers.Binary;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Statues;

internal static class StatueBlob {

    private const PropertyFlags AVATAR_MASK = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
    private const PropertyFlags EQUIPMENT_MASK = PropertyFlags.Prop_Public;
    private const PropertyFlags BLOB_MASK = PropertyFlags.Prop_Public | AVATAR_MASK;
    private const uint NO_EMOTE_ID = 0;

    private static readonly uint s_equipmentWireHash = StringHash.Compute("class WizEquipmentBehavior");

    internal static bool TryBuild(PlayerStatue statue, out ByteString blob) {
        blob = default;
        if (!TrySerialize(statue.Appearance, AVATAR_MASK, null, out var avatarBuffer)
            || !TrySerialize(CreateEquipment(statue), EQUIPMENT_MASK, s_equipmentWireHash, out var equipmentBuffer)) {
            Logger.Error("Could not serialize the look of statue {Id}.", Logger.Args(statue.Id));

            return false;
        }

        var stitchBlob = new PlayerStatueStitchBlob {
            m_packedName = CreatePackedName(statue),
            m_characterBuffer = avatarBuffer,
            m_equipmentBuffer = equipmentBuffer,
            m_emoteID = NO_EMOTE_ID,
        };
        if (!TrySerialize(stitchBlob, BLOB_MASK, null, out var blobBytes)) {
            Logger.Error("Could not serialize the blob of statue {Id}.", Logger.Args(statue.Id));

            return false;
        }

        blob = BuddyStatsBuilder.WrapCompressed(blobBytes);

        return true;
    }

    private static ClientWizEquipmentBehavior CreateEquipment(PlayerStatue statue)
        => new() {
            m_behaviorTemplateNameID = BuddyStatsBuilder.EquipmentBehaviorTemplateNameId,
            m_publicItemList = [.. statue.Equipment],
        };

    private static byte[] CreatePackedName(PlayerStatue statue) {
        var name = new ServerWizPlayerNameBehavior {
            NameIndices = statue.NameKeys,
            Gender = statue.Appearance.m_eGender,
            NameOverride = statue.NameKeys == 0 ? statue.Name : default,
        };

        return name.GetPackedOverrideBytes()
            ?? DataManipulation.SpacedHexStringToBytes(name.GetWizardNameAsByteHexString());
    }

    private static bool TrySerialize(PropertyClass source, PropertyFlags mask, uint? wireHash, out byte[] bytes) {
        bytes = [];

        // A serializer instance keeps its mask between calls, so one is created per object.
        var serializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);
        if (!serializer.Serialize(source, mask, out var serialized)) {
            return false;
        }

        bytes = serialized;
        if (wireHash is { } hash) {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, hash);
        }

        return true;
    }

}
