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
 * HOUSING SERVICE
 * ========================================================================
 *
 * PURPOSE:
 * Sends a house's furniture to the client when the player enters it, and keeps the client's copy in step
 * with the house while the player decorates.
 *
 * USAGE EXAMPLE:
 * Internal service handling housing messages within the game server session.
 *
 * NOTE:
 * - Furniture is not a zone entity. The client builds every placed object from a blob the server sends, and
 *   later edits arrive as small versioned patches. The blob and the patches are built here, from the stored
 *   items, with the generated Imcodec types; the house is in Game/Housing.
 * - The service counts the player into the house at MSG_PRELOGIN and out when the session ends.
 * - A blob request that names the attic is left to AtticService. The house has a blob for plain objects, one for pets and one for
 *   mounts; a pet or a mount is placed as a custom entry that carries its look.
 * - Only the owner decorates. A visitor sees the same blob and patches, and every edit they send is refused as forged.
 * - An object that is placed keeps the item document of its backpack item, and the house holds the id, so a pick up gives back
 *   the same item.
 * - Picking up every object moves them into the owner's attic, so the service is a viewer of the attic as well. The house
 *   and the attic are written in one transaction by Game/Housing/HousePickUpAll.
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
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Attics;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.Game.Housing.Items;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class HousingService(SessionActor sessionActor) : MessageService(sessionActor) {

    private HouseState _house;
    private readonly AtticViewer _atticViewer = new();

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new HousingService(parentActor));

    protected override void OnPreDispose() {
        LeaveHouse();
        _atticViewer.Release();
        base.OnPreDispose();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PRELOGIN))]
    private void ReceivePreLogin(ZONE_102_PROTOCOL.MSG_PRELOGIN message) {
        LeaveHouse();

        var wizard = GetActiveWizard();
        if (wizard.AttachedLotGid == 0) {
            return;
        }

        if (!HouseRegistry.TryEnter(wizard.AttachedLotGid, out var house)) {
            Logger.Warning("Character {CharId} attached to lot {LotGid}, which is not a house.",
                Logger.Args(wizard.CharId, wizard.AttachedLotGid));

            return;
        }

        _house = house;

        // The client compares this with its cached copy and asks for the blob only when they differ.
        var request = new BlobRequest {
            m_type = HousingBlobs.BLOB_TYPE,
            m_associatedGID = house.LotGid,
            m_blobRequestObjectList = [.. HousingItems.LiveSubTypes.Select(subType => BlobRequestOf(house.Snapshot(subType)))],
        };
        SendToSocket(new WIZARDHOUSING_50_PROTOCOL.MSG_REQUEST_BLOBS { Data = HousingBlobs.Serialize(request) });
    }

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_REQUEST_BLOBS))]
    private void ReceiveRequestBlobs(WIZARDHOUSING_50_PROTOCOL.MSG_REQUEST_BLOBS message) {
        var wizard = GetActiveWizard();
        if (!HousingBlobs.TryParseRequest(message.Data, out var request)) {
            Logger.Warning("Character {CharId} sent a malformed blob request.", Logger.Args(wizard.CharId));

            return;
        }

        // The attic has blobs of its own, which AtticService answers.
        if (wizard.AtticContainerGid != 0 && request.m_associatedGID == wizard.AtticContainerGid) {
            return;
        }

        var house = _house;
        if (house is null) {
            Logger.Warning("Character {CharId} requested house blobs outside a house.", Logger.Args(wizard.CharId));

            return;
        }

        // The version, count and user data in the request are the client's cache state, or 0 in a resync. Neither
        // decides what is sent.
        var subTypes = RequestedSubTypes(request, house.LotGid);
        if (subTypes.Count == 0) {
            Logger.Warning("Character {CharId} requested blobs that are not blobs of lot {LotGid}.",
                Logger.Args(wizard.CharId, house.LotGid));

            return;
        }

        foreach (var subType in subTypes) {
            SendBlob(house, subType);
        }
    }

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_PLACEHOUSINGOBJECT))]
    private void ReceivePlaceHousingObject(WIZARDHOUSING_50_PROTOCOL.MSG_PLACEHOUSINGOBJECT message) {
        if (!TryGetHouse("place an object", out var house)) {
            return;
        }

        // The item and its template come from the server's own inventory, never from the client.
        var wizard = GetActiveWizard();
        var item = wizard.InventoryBehavior.GetItem(message.ObjectID);
        if (item is null) {
            HandleForgedRequest($"place item {message.ObjectID} that is not in the backpack");

            return;
        }

        var templateId = ItemHelper.GetTemplateId(item);
        var verdict = HousingItems.Check(templateId, out var template);
        if (verdict.Placement == HousingPlacement.Refused) {
            HandleForgedRequest($"place item {message.ObjectID}, template {templateId}, which is not placeable furniture");

            return;
        }

        if (verdict.Placement == HousingPlacement.NotEnabled || message.SwitchCastleBlock != 0) {
            Logger.Information("Character {CharId} tried to place item {ItemId}, template {TemplateId}, which the server cannot place yet.",
                Logger.Args(wizard.CharId, message.ObjectID, templateId));
            InformGameClient("That kind of object cannot be placed in a house yet.");

            return;
        }

        var placed = new PlacedItem {
            ItemGid = item.m_globalID,
            TemplateId = templateId,
            X = message.LocX,
            Y = message.LocY,
            Z = message.LocZ,
            Yaw = message.Yaw,
            Custom = HousingItems.CustomDataOf(verdict.SubType, item, item.m_globalID),
        }.Normalize();
        if (placed is null) {
            HandleForgedRequest($"place item {message.ObjectID} at a position that is not valid");

            return;
        }

        if (house.LiveCount >= house.ItemLimit
            || HousingItems.TemplateCopyLimitReached(template, house.CountTemplate(templateId))) {
            InformGameClient("This house cannot hold any more of that.");

            return;
        }

        var itemId = item.m_globalID;
        HouseChange change = default;
        var transfer = BackpackTransfer.Store(wizard, item,
            () => (change = house.TryAdd(placed, verdict.SubType, document => HouseCollection.SaveWithBackpack(document, wizard))).Done, "be placed");
        if (transfer == BackpackTransferResult.NotInBackpack) {
            return;
        }

        if (!change.Done) {
            if (change.Result == HouseEdit.NeedsResync) {
                SendBlobs(house);
            }

            InformGameClient("The object could not be placed.");

            return;
        }

        ItemSync.SendRemoved(SessionActor.ActorRef, wizard, itemId);
        ZoneBroadcast(new WIZARDHOUSING_50_PROTOCOL.MSG_PATCHADDHOUSINGOBJECT {
            ObjectID = templateId,
            LocX = change.Item.X,
            LocY = change.Item.Y,
            LocZ = change.Item.Z,
            Yaw = change.Item.Yaw,
            SubType = change.SubType,
            PackageNumber = 0,
            VersionNumber = change.Revision,
            GIDID = change.GidId,
            Data = change.Item.Custom ?? [],
            ColorBits = (sbyte) change.Item.Tint,
        }, isSelfless: false, ZoneBroadcastTarget.Players);
    }

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_UPDATEHOUSINGOBJECT))]
    private void ReceiveUpdateHousingObject(WIZARDHOUSING_50_PROTOCOL.MSG_UPDATEHOUSINGOBJECT message) {
        if (!TryGetHouse("move an object", out var house)) {
            return;
        }

        if (message.UseExtendedYaw is not (0 or 1)) {
            HandleForgedRequest("move an object with an invalid yaw flag");

            return;
        }

        if (!house.TryFindByGid(message.ObjectGID, out var current, out _)) {
            // A client that has not applied a renumbered blob yet sends exactly this, so it gets the real state.
            Logger.Debug("Move of object {ObjectGid}, which is not in the current blob of lot {LotGid}.", Logger.Args(message.ObjectGID, house.LotGid));
            SendBlobs(house);

            return;
        }

        if (current is null) {
            // The object is gone. The client moved it already, so give it the real state.
            SendBlobs(house);

            return;
        }

        if (message.SwitchCastleBlock != 0) {
            InformGameClient("That kind of object cannot be changed in a house yet.");
            SendBlobs(house);

            return;
        }

        var invalid = false;
        var change = house.TryUpdate(current.Id, stored => {
            var replacement = (stored with {
                X = message.LocX,
                Y = message.LocY,
                Z = message.LocZ,
                Yaw = message.Yaw,
                ExactYaw = message.UseExtendedYaw == 1,
                Scale = message.Scale,
                Brightness = message.Brightness,
            }).Normalize();
            invalid = replacement is null;

            return replacement;
        });
        if (invalid) {
            HandleForgedRequest($"move object {message.ObjectGID} to a position, scale or brightness that is not valid");

            return;
        }

        if (!change.Done) {
            // The object is gone or the write failed. The client moved it already, so give it the real state.
            SendBlobs(house);

            return;
        }

        ZoneBroadcast(new WIZARDHOUSING_50_PROTOCOL.MSG_PATCHUPDATEHOUSINGOBJECT {
            LocX = change.Item.X,
            LocY = change.Item.Y,
            LocZ = change.Item.Z,
            Yaw = change.Item.Yaw,
            SubType = change.SubType,
            PackageNumber = 0,
            VersionNumber = change.Revision,
            GIDID = change.GidId,
            ObjectID = change.Item.TemplateId,
            SwitchTemplateID = 0,
            UseExtendedYaw = (sbyte) (change.Item.ExactYaw ? 1 : 0),
            Scale = change.Item.Scale,
            Brightness = (sbyte) change.Item.Brightness,
        }, isSelfless: false, ZoneBroadcastTarget.Players);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_PICKUPOBJECT))]
    private void ReceivePickUpObject(GAME_5_PROTOCOL.MSG_PICKUPOBJECT message) {
        // Other features use this message too, so a GID that is not one of this house's objects is not ours.
        var house = _house;
        if (house is null || !house.TryFindByGid(message.GameObjectID, out var placedItem, out _)) {
            return;
        }

        var wizard = GetActiveWizard();
        if (house.OwnerCharId != wizard.CharId) {
            HandleForgedRequest("pick up an object in a house that is not theirs");

            return;
        }

        if (placedItem is null) {
            RefusePickUp(house);

            return;
        }

        // The backpack and the emptied slot are written in one transaction, so a crash between them cannot lose the item.
        // A stored item keeps its document, and only a new item is stored with it.
        HouseChange change = default;
        var transfer = BackpackTransfer.Take(wizard, placedItem.TemplateId, placedItem.ItemGid,
            added => (change = house.TryRemove(placedItem.Id, document => HouseCollection.SaveWithBackpack(document, wizard, added))).Done, out var item);
        if (transfer != BackpackTransferResult.Done) {
            RefusePickUp(house);

            return;
        }

        if (!ItemSync.SendAdded(SessionActor.ActorRef, wizard, item)) {
            Logger.Error("Could not tell the client about picked up item {ItemId} of character {CharId}.",
                Logger.Args(item.m_globalID, wizard.CharId));
        }

        BroadcastDelete(change);
    }

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_PICKUPALL))]
    private void ReceivePickUpAll(WIZARDHOUSING_50_PROTOCOL.MSG_PICKUPALL message) {
        if (!TryGetHouse("pick up every object", out var house)) {
            return;
        }

        var wizard = GetActiveWizard();
        if (!HousingBlobs.TryParseItemList(message.Exceptions, out var kept)) {
            // Which objects stay is not known, so none is moved.
            Logger.Warning("Character {CharId} sent a list of objects to keep that could not be read ({Length} bytes).",
                Logger.Args(wizard.CharId, ((byte[]) message.Exceptions)?.Length ?? 0));
            InformGameClient("The objects could not be picked up.");

            return;
        }

        if (!_atticViewer.TryOpen(wizard, SendToSocket, text => InformGameClient(text), out var attic)) {
            return;
        }

        var result = HousePickUpAll.Run(house, attic, kept);
        if (result.Edit == HouseEdit.NeedsResync) {
            // The attic blobs were numbered again. The client needs them before any patch, and the run goes on with the room they made.
            foreach (var subType in result.Renumbered) {
                if (AtticMessages.BlobMessage(attic, subType) is { } blob) {
                    SendToSocket(blob);
                }
            }

            result = HousePickUpAll.Run(house, attic, kept);
        }

        if (result.Edit is not (HouseEdit.Done or HouseEdit.LimitReached)) {
            Logger.Warning("Character {CharId} could not pick up every object of lot {LotGid}: {Edit}.",
                Logger.Args(wizard.CharId, house.LotGid, result.Edit));
            InformGameClient("The objects could not be picked up.");
            SendBlobs(house);

            return;
        }

        foreach (var change in result.Added) {
            SendToSocket(AtticMessages.AddPatch(attic, change));
        }

        foreach (var change in result.Removed) {
            BroadcastDelete(change);
        }

        if (result.Added.Count > 0) {
            AtticRegistry.SetSummary(wizard, attic);
            SendToSocket(AtticMessages.Count(wizard, attic));
        }

        // Any error code makes the client say that the attic is too full, which is the one reason objects stay.
        SendToSocket(new WIZARDHOUSING_50_PROTOCOL.MSG_PICKUPALL {
            ItemCount = (uint) result.Removed.Count,
            ErrorCode = (sbyte) (result.Edit == HouseEdit.LimitReached || result.Left > 0 ? 1 : 0),
            Exceptions = string.Empty,
        });
    }

    private bool TryGetHouse(string request, out HouseState house) {
        house = _house;
        if (house is null) {
            HandleForgedRequest($"{request} outside a house");

            return false;
        }

        // Only the owner decorates. The client hides the controls from a visitor, so a request is forged.
        if (house.OwnerCharId != GetActiveWizard().CharId) {
            house = null;
            HandleForgedRequest($"{request} in a house that is not theirs");

            return false;
        }

        return true;
    }

    private void BroadcastDelete(HouseChange change) {
        ZoneBroadcast(new WIZARDHOUSING_50_PROTOCOL.MSG_PATCHDELETEHOUSINGOBJECT {
            SubType = change.SubType,
            PackageNumber = 0,
            VersionNumber = change.Revision,
            GIDID = change.GidId,
        }, isSelfless: false, ZoneBroadcastTarget.Players);
    }

    private void RefusePickUp(HouseState house) {
        // The client has faded the object already and the refusal does not bring it back, so the blob rebuilds it.
        SendToSocket(new WIZARDHOUSING_50_PROTOCOL.MSG_HOUSINGOBJECTNOPICKUP());
        SendBlobs(house);
    }

    private void LeaveHouse() {
        if (_house is null) {
            return;
        }

        HouseRegistry.Release(_house);
        _house = null;
    }

    private void SendBlobs(HouseState house) {
        foreach (var subType in HousingItems.LiveSubTypes) {
            SendBlob(house, subType);
        }
    }

    private void SendBlob(HouseState house, string subType) {
        var snapshot = house.Snapshot(subType);
        try {
            SendToSocket(HousingBlobs.CreateBlobMessage(subType, snapshot.Revision, house.LotGid, snapshot.GidBase,
                snapshot.Slots.Select(item => item?.ToBlobObject() ?? new HousingBlobObject())));
        } catch (InvalidOperationException exception) {
            Logger.Error("Could not build the {SubType} blob of lot {LotGid}: {Reason}", Logger.Args(subType, house.LotGid, exception.Message));
        }
    }

    private static BlobRequestObject BlobRequestOf(HouseSnapshot snapshot)
        => new() {
            m_subType = snapshot.SubType,
            m_versionNumber = snapshot.Revision,
            m_packageNumber = 0,
            m_userData = snapshot.GidBase,
            m_objectCount = (uint) snapshot.LiveCount,
        };

    private static List<string> RequestedSubTypes(BlobRequest request, ulong lotGid) {
        if (request.m_type != HousingBlobs.BLOB_TYPE || request.m_associatedGID != lotGid) {
            return [];
        }

        return [.. (request.m_blobRequestObjectList ?? [])
            .Where(entry => entry is not null && entry.m_packageNumber == 0)
            .Select(entry => (string) entry.m_subType)
            .Where(subType => HousingItems.LiveSubTypes.Contains(subType))
            .Distinct()];
    }

}
