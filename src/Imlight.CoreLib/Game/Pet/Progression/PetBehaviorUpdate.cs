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
 * PET BEHAVIOR UPDATE
 * ========================================================================
 *
 * PURPOSE:
 * Builds the message that tells the client the new state of a pet item's behavior.
 *
 * USAGE EXAMPLE:
 * if (PetBehaviorUpdate.TryCreate(pet, out var update)) { SendToSocket(update); }
 *
 * NOTE:
 * The message is keyed by the pet item's global id. The blob starts with the class hash the client
 * writes for the behavior, which is not the hash the serializer derives from the type.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Buffers.Binary;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.ObjectProperty;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Pet.Progression;

/// <summary>
/// Creates MSG_PETUPDATEBEHAVIOR for a pet item.
/// </summary>
internal static class PetBehaviorUpdate {

    private const PropertyFlags TRANSMIT_MASK = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;

    internal static uint WireClassHash { get; } = StringHash.Compute("class PetItemBehavior");

    /// <summary>
    /// Creates the update for a pet item, sent as the item's current state.
    /// </summary>
    /// <param name="pet">The pet item. Its behavior should already be initialized.</param>
    /// <param name="update">The message, if the item has a pet behavior that serializes.</param>
    /// <returns>True if the message was created.</returns>
    internal static bool TryCreate(WizClientObjectItem pet, out PET_9_PROTOCOL.MSG_PETUPDATEBEHAVIOR update) {
        update = null;
        if (pet?.m_inactiveBehaviors is null || !CoreObjectFactory.FindBehaviorInstance<ClientPetItemBehavior>(pet, out var behavior)) {
            return false;
        }

        var serializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);
        if (!serializer.Serialize(behavior, TRANSMIT_MASK, out var blob)) {
            Logger.Error("Failed to serialize the pet item behavior of pet {PetId}.", Logger.Args(pet.m_globalID.Full));

            return false;
        }

        var bytes = (byte[]) blob;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, WireClassHash);
        update = new PET_9_PROTOCOL.MSG_PETUPDATEBEHAVIOR {
            GlobalID = pet.m_globalID,
            Data = bytes
        };

        return true;
    }

}
