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
 * ATTIC SERVICE
 * ========================================================================
 *
 * PURPOSE:
 * Keeps a character's attic: housing items put away from the backpack, listed in the decorating window and taken back
 * out. The client shows the attic from blobs, and later changes arrive as small versioned patches.
 *
 * USAGE EXAMPLE:
 * Internal service handling attic messages within the game server session.
 *
 * NOTE:
 * - The attic is a stored list of items in Game/Housing, in one Attic blob. AtticMessages builds the
 *   blobs and the patches from the stored items with the generated Imcodec types; HousingService sends them too when
 *   "Pick Up All Items" fills the attic.
 * - The attic document is created the first time the character needs it, and the client is told its container id then.
 * - The blobs are sent when the window is opened and whenever a client that cannot apply a patch asks for one.
 * - An item keeps its global id and its item document in the attic, so it comes back as the same item, a pet with its stats,
 *   name and dyes included, and its entry carries the look the client shows. Only furniture the server supports goes in. Deleting an item from the attic deletes its document.
 *
 * TODO:
 * - Does the live server list the attic blobs when the player enters the game, or only when the window opens?
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Attics;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.Game.Housing.Items;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class AtticService(SessionActor sessionActor) : MessageService(sessionActor) {

    private readonly AtticViewer _atticViewer = new();

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new AtticService(parentActor));

    protected override void OnPreDispose() {
        _atticViewer.Release();
        base.OnPreDispose();
    }

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTATTIC))]
    private void ReceiveRequestAttic(WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTATTIC message) {
        var wizard = GetActiveWizard();
        if (message.GlobalID != wizard.GameObjectID) {
            HandleForgedRequest($"open the attic of object {message.GlobalID}");

            return;
        }

        if (!_atticViewer.TryOpen(wizard, SendToSocket, text => InformGameClient(text), out var attic)) {
            return;
        }

        // The window lists what the blobs hold, so they go first.
        SendAllBlobs(attic);

        SendCount(wizard, attic);
        SendToSocket(new WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTATTIC { GlobalID = message.GlobalID });
    }

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_REQUEST_BLOBS))]
    private void ReceiveRequestBlobs(WIZARDHOUSING_50_PROTOCOL.MSG_REQUEST_BLOBS message) {
        var wizard = GetActiveWizard();

        // Only requests that name the attic are ours; the house answers the others.
        if (wizard.AtticContainerGid == 0 || !HousingBlobs.TryParseRequest(message.Data, out var request)
            || request.m_associatedGID != wizard.AtticContainerGid) {
            return;
        }

        if (request.m_type != HousingBlobs.BLOB_TYPE || !_atticViewer.TryOpen(wizard, SendToSocket, text => InformGameClient(text), out var attic)) {
            Logger.Warning("Character {CharId} sent a blob request for the attic that is not a housing request.", Logger.Args(wizard.CharId));

            return;
        }

        // The version, count and user data in the request are the client's cache state, or 0 in a resync. Neither
        // decides what is sent.
        var subTypes = (request.m_blobRequestObjectList ?? [])
            .Where(entry => entry is not null && entry.m_packageNumber == 0 && IsAtticSubType(attic, entry.m_subType))
            .Select(entry => (string) entry.m_subType)
            .Distinct();
        foreach (var subType in subTypes) {
            SendBlob(attic, subType);
        }
    }

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_MOVETOATTIC))]
    private void ReceiveMoveToAttic(WIZARDHOUSING_50_PROTOCOL.MSG_MOVETOATTIC message) {
        var wizard = GetActiveWizard();
        if (message.GlobalID != wizard.GameObjectID) {
            HandleForgedRequest($"move item {message.ItemID} to the attic of object {message.GlobalID}");

            return;
        }

        // The item and its template come from the server's own inventory, never from the client.
        var item = wizard.InventoryBehavior.GetItem(message.ItemID);
        if (item is null) {
            HandleForgedRequest($"move item {message.ItemID}, which is not in the backpack, to the attic");

            return;
        }

        var templateId = ItemHelper.GetTemplateId(item);
        var verdict = HousingItems.Check(templateId, out _);
        if (verdict.Placement == HousingPlacement.Refused) {
            HandleForgedRequest($"move item {message.ItemID}, template {templateId}, which is not housing furniture, to the attic");

            return;
        }

        if (verdict.Placement == HousingPlacement.NotEnabled) {
            Logger.Information("Character {CharId} tried to put item {ItemId}, template {TemplateId}, in the attic, which is not supported.",
                Logger.Args(wizard.CharId, message.ItemID, templateId));
            InformGameClient("That kind of object cannot be put in the attic yet.");

            return;
        }

        if (!_atticViewer.TryOpen(wizard, SendToSocket, text => InformGameClient(text), out var attic)) {
            return;
        }

        if (attic.LiveCount >= attic.Limit) {
            InformGameClient("Your attic is full.");

            return;
        }

        var itemId = item.m_globalID;
        AtticChange change = default;
        var stored = new AtticItem { ItemGid = itemId, TemplateId = templateId, Custom = HousingItems.CustomDataOf(verdict.SubType, item, itemId) };
        var transfer = BackpackTransfer.Store(wizard, item,
            () => (change = attic.TryAdd(stored, HousingItems.ATTIC_SUB_TYPE, document => AtticCollection.SaveWithBackpack(document, wizard))).Done,
            "go in the attic");
        if (transfer == BackpackTransferResult.NotInBackpack) {
            return;
        }

        if (!change.Done) {
            if (change.Result == HouseEdit.NeedsResync) {
                SendBlob(attic, HousingItems.ATTIC_SUB_TYPE);
            }

            InformGameClient(change.Result == HouseEdit.LimitReached ? "Your attic is full." : "The object could not be put in the attic.");

            return;
        }

        ItemSync.SendRemoved(SessionActor.ActorRef, wizard, itemId);
        SendToSocket(AtticMessages.AddPatch(attic, change));
        SendCount(wizard, attic);
    }

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_MOVEFROMATTIC))]
    private void ReceiveMoveFromAttic(WIZARDHOUSING_50_PROTOCOL.MSG_MOVEFROMATTIC message) {
        var wizard = GetActiveWizard();
        if (!TryFindAtticItem(wizard, message.GlobalID, message.ItemID, "move", out var attic, out var stored)) {
            return;
        }

        // The slot was emptied already: the client acted on a blob that is a step behind.
        if (stored is null) {
            return;
        }

        // The backpack and the emptied slot are written in one transaction, so a crash between them cannot lose the item.
        // A stored item keeps its document, and only a new item is stored with it.
        AtticChange change = default;
        var transfer = BackpackTransfer.Take(wizard, stored.TemplateId, stored.ItemGid,
            added => (change = attic.TryRemove(stored.Id, document => AtticCollection.SaveWithBackpack(document, wizard, added))).Done, out var item);
        if (transfer != BackpackTransferResult.Done) {
            InformGameClient(transfer == BackpackTransferResult.BackpackCannotHold
                ? "Your backpack cannot hold that object."
                : "The object could not be taken out of the attic.");

            return;
        }

        if (!ItemSync.SendAdded(SessionActor.ActorRef, wizard, item)) {
            Logger.Error("Could not tell the client about item {ItemId} taken out of the attic of character {CharId}.",
                Logger.Args(item.m_globalID, wizard.CharId));
        }

        SendDeletePatch(attic, change);
        SendCount(wizard, attic);
    }

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_DELETEFROMATTIC))]
    private void ReceiveDeleteFromAttic(WIZARDHOUSING_50_PROTOCOL.MSG_DELETEFROMATTIC message) {
        var wizard = GetActiveWizard();
        if (!TryFindAtticItem(wizard, message.GlobalID, message.ItemID, "delete", out var attic, out var stored)) {
            return;
        }

        if (stored is null) {
            return;
        }

        // The item's document goes with the slot, in one write.
        var change = attic.TryRemove(stored.Id, document => AtticCollection.SaveDeleting(document, stored.ItemGid));
        if (!change.Done) {
            InformGameClient("The object could not be deleted from the attic.");

            return;
        }

        SendDeletePatch(attic, change);
        SendCount(wizard, attic);
    }

    private bool TryFindAtticItem(Wizard wizard, ulong globalId, ulong itemId, string what, out AtticState attic, out AtticItem stored) {
        stored = null;
        attic = null;
        if (globalId != wizard.GameObjectID) {
            HandleForgedRequest($"{what} attic item {itemId} for object {globalId}");

            return false;
        }

        if (!_atticViewer.TryOpen(wizard, SendToSocket, text => InformGameClient(text), out attic)) {
            return false;
        }

        if (!attic.TryFindByGid(itemId, out stored, out _)) {
            // A client that has not applied a renumbered blob yet sends exactly this, so it gets the real state.
            Logger.Debug("Character {CharId} tried to {What} attic item {ItemId}, which is not in the current blobs.",
                Logger.Args(wizard.CharId, what, itemId));
            SendAllBlobs(attic);

            return false;
        }

        return true;
    }

    private void SendDeletePatch(AtticState attic, AtticChange change)
        => SendToSocket(AtticMessages.DeletePatch(attic, change));

    private void SendCount(Wizard wizard, AtticState attic) {
        AtticRegistry.SetSummary(wizard, attic);
        SendToSocket(AtticMessages.Count(wizard, attic));
    }

    private void SendAllBlobs(AtticState attic) {
        foreach (var subType in attic.SubTypes().Append(HousingItems.ATTIC_SUB_TYPE).Distinct()) {
            SendBlob(attic, subType);
        }
    }

    private void SendBlob(AtticState attic, string subType) {
        if (AtticMessages.BlobMessage(attic, subType) is { } blob) {
            SendToSocket(blob);
        }
    }

    private static bool IsAtticSubType(AtticState attic, string subType)
        => subType == HousingItems.ATTIC_SUB_TYPE || attic.SubTypes().Contains((string) subType);

}
