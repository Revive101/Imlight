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
 * HOUSING SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Builds what the client is told about an attic: its blobs, the patches that follow a change, the item count and the
 * container id. Every service that changes an attic sends the same messages, so they are built in one place.
 *
 * USAGE EXAMPLE:
 * SendToSocket(AtticMessages.AddPatch(attic, change));
 * SendToSocket(AtticMessages.Count(wizard, attic));
 *
 * NOTE:
 * - The messages are built from the stored items with the generated Imcodec types and HousingBlobs.
 * - Nothing here sends: the caller sends what it gets, in the order the client needs it.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Items;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Attics;

internal static class AtticMessages {

    /// <summary>
    /// Builds the patch that adds an item to its blob.
    /// </summary>
    /// <param name="attic">The attic.</param>
    /// <param name="change">The change that stored the item.</param>
    /// <returns>The patch, with the blob's next version.</returns>
    internal static IMessage AddPatch(AtticState attic, AtticChange change)
        => new WIZARDHOUSING_50_PROTOCOL.MSG_PATCHADDATTIC {
            BlobGID = attic.ContainerGid,
            ObjectID = change.Item.TemplateId,
            SubType = change.SubType,
            PackageNumber = 0,
            VersionNumber = change.Revision,
            GIDID = change.GidId,
            Data = change.Item.Custom ?? [],
            PrimaryColorIndex = (sbyte) change.Item.Tint,
        };

    /// <summary>
    /// Builds the patch that removes an item from its blob.
    /// </summary>
    /// <param name="attic">The attic.</param>
    /// <param name="change">The change that removed the item.</param>
    /// <returns>The patch, with the blob's next version.</returns>
    internal static IMessage DeletePatch(AtticState attic, AtticChange change)
        => new WIZARDHOUSING_50_PROTOCOL.MSG_PATCHDELETEATTIC {
            BlobGID = attic.ContainerGid,
            ObjectID = change.Item.TemplateId,
            SubType = change.SubType,
            PackageNumber = 0,
            VersionNumber = change.Revision,
            GIDID = change.GidId,
        };

    /// <summary>
    /// Builds the message that tells the client how many items the attic holds.
    /// </summary>
    /// <param name="wizard">The attic's owner.</param>
    /// <param name="attic">The attic.</param>
    /// <returns>The count message.</returns>
    internal static IMessage Count(Wizard wizard, AtticState attic)
        => new WIZARDHOUSING_50_PROTOCOL.MSG_UPDATEATTICCOUNT { GlobalID = wizard.GameObjectID, ItemCount = attic.LiveCount };

    /// <summary>
    /// Builds one blob of the attic.
    /// </summary>
    /// <param name="attic">The attic.</param>
    /// <param name="subType">The blob's subtype.</param>
    /// <returns>The blob message, or <c>null</c> when the blob cannot be built.</returns>
    internal static IMessage BlobMessage(AtticState attic, string subType) {
        var snapshot = attic.Snapshot(subType);
        try {
            return HousingBlobs.CreateBlobMessage(subType, snapshot.Revision, attic.ContainerGid, snapshot.GidBase,
                snapshot.Slots.Select(item => item?.ToBlobObject() ?? new HousingBlobObject()));
        } catch (InvalidOperationException exception) {
            Logger.Error("Could not build the {SubType} attic blob of character {CharId}: {Reason}",
                Logger.Args(subType, attic.CharId, exception.Message));

            return null;
        }
    }

    /// <summary>
    /// Builds what the client is told when a character's attic becomes live: the container id when the attic was just created,
    /// and the result of the audit when it dropped items.
    /// </summary>
    /// <param name="wizard">The attic's owner.</param>
    /// <param name="attic">The attic.</param>
    /// <param name="created">Whether the attic document was just created.</param>
    /// <param name="audit">What the audit at the creation of the state found.</param>
    /// <returns>The messages to send, in order.</returns>
    internal static IReadOnlyList<IMessage> Opening(Wizard wizard, AtticState attic, bool created, AtticAudit audit) {
        var messages = new List<IMessage>();
        if (created) {
            messages.Add(new WIZARDHOUSING_50_PROTOCOL.MSG_SETATTICID { GlobalID = wizard.GameObjectID, AtticID = attic.ContainerGid });
        }

        if (audit.Removed > 0) {
            Logger.Warning("The attic of character {CharId} lost {Removed} of {Audited} items whose template is gone.",
                Logger.Args(wizard.CharId, audit.Removed, audit.Audited));
            messages.Add(new WIZARDHOUSING_50_PROTOCOL.MSG_AUDITATTICRESULTS { ItemsAudited = (uint) audit.Audited, ItemsRemoved = (uint) audit.Removed });
        }

        return messages;
    }

}
