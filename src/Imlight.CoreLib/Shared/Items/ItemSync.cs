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
 * ITEM SYNC
 * ========================================================================
 *
 * PURPOSE:
 * Tells a player's client that an item entered or left its inventory behavior,
 * with the one property mask the client's inventory reader expects.
 *
 * USAGE EXAMPLE:
 * if (!ItemSync.SendAdded(SessionActor.ActorRef, wizard, item)) { ... }
 * ItemSync.SendRemoved(SessionActor.ActorRef, wizard, itemId);
 *
 * NOTE:
 * The session is a SessionActor reference: an IMessage told to it goes to the
 * client socket. The client reads the item blob at mask 0x18 (Prop_Transmit |
 * Prop_AuthorityTransmit) and a blob written at another mask is misaligned.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Items;

internal static class ItemSync {

    private const uint ItemSerializationFlags =
        (uint) (PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit);
    private const int TEMPLATE_GID_TYPE_BIT_SHIFT = 40;
    private const ulong ITEM_TEMPLATE_GID_TYPE = 9;
    private static readonly CoreObjectSerializer s_itemSerializer = new(behaviors: SerializerFlags.None);

    /// <summary>
    /// Serializes an item the way the client's inventory add message reads it.
    /// </summary>
    /// <param name="item">The item to serialize.</param>
    /// <param name="serialized">The item blob.</param>
    /// <returns>False if the item could not be serialized.</returns>
    internal static bool TrySerialize(WizClientObjectItem item, out ByteString serialized) {
        // The client compares the item's template id as a full 64-bit id with the item type set, for example to
        // match a loot toast to the item it offers to equip. A copy carries that id, so the stored item keeps
        // the plain template id the server looks templates up with.
        var clientItem = item with {
            m_templateID = item.m_templateID.Full | (ITEM_TEMPLATE_GID_TYPE << TEMPLATE_GID_TYPE_BIT_SHIFT)
        };

        return s_itemSerializer.Serialize(clientItem, ItemSerializationFlags, out serialized);
    }

    /// <summary>
    /// Tells the client that an item entered the wizard's inventory.
    /// </summary>
    /// <param name="session">The SessionActor reference of the wizard's session.</param>
    /// <param name="wizard">The wizard that received the item.</param>
    /// <param name="item">The item that was added.</param>
    /// <returns>False if the item could not be serialized, in which case nothing was sent.</returns>
    internal static bool SendAdded(IActorRef session, Wizard wizard, WizClientObjectItem item) {
        if (!TrySerialize(item, out var serializedItem)) {
            Logger.Error("Failed to serialize item {0} for the inventory add message.",
                Logger.Args(item.m_globalID.Full));

            return false;
        }

        session.Tell(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = wizard.GameObjectID,
            SerializedItem = serializedItem,
        });

        return true;
    }

    /// <summary>
    /// Tells the client that an item left the wizard's inventory.
    /// </summary>
    /// <param name="session">The SessionActor reference of the wizard's session.</param>
    /// <param name="wizard">The wizard that lost the item.</param>
    /// <param name="itemId">The global id of the removed item.</param>
    internal static void SendRemoved(IActorRef session, Wizard wizard, ulong itemId)
        => session.Tell(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_REMOVEITEM {
            GlobalID = wizard.GameObjectID,
            ItemID = itemId,
        });

}
