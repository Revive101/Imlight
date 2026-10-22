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
 * The wait for a client to confirm that a house zone is ready, between the request for the zone and the transfer: it
 * remembers which entry was asked for, reads the client's echo of it, and ends the wait by starting the transfer or by
 * aborting it.
 *
 * USAGE EXAMPLE:
 * _houseZone.Track(entry);
 * _houseZone.SendRequest(wizard, entry, location);
 * // later, from the handler of the client's echo
 * _houseZone.OnEcho(pending => pending.Read(message), "REQUESTDEEDZONE");
 *
 * NOTE:
 * - A house transfer has no zone transfer request: the client's echo of the house zone request is its answer.
 * - Only the echo of the request that was sent counts. Anything else the client sends under the same names is ignored.
 * - The host owns the transfer gate, the timers and the socket; this class owns the pending entry and the decisions.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System;
using Imcodec.MessageLayer;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Access;

/// <summary>
/// What the handshake needs from the session service that runs the zone transfer.
/// </summary>
internal interface IHouseZoneHost {

    /// <summary>
    /// Gets the gate of the transfer that the handshake belongs to.
    /// </summary>
    ZoneTransferGate TransferGate { get; }

    /// <summary>
    /// Gets the character who is transferred.
    /// </summary>
    Wizard Character { get; }

    void Send(IMessage message);

    void SendNewItem(ulong itemGid);

    void StartTimeout();

    void CancelTimeout();

    void Inform(string text);

    void LogStep(string step, ZoneTransferState previous, bool accepted);

    void BeginTransfer();

}

internal sealed class HouseZoneHandshake(IHouseZoneHost host) {

    private HouseEntry _pending;

    /// <summary>
    /// Gets a value indicating whether a request for a house zone is waiting for the client's answer.
    /// </summary>
    internal bool IsWaiting => _pending is not null && host.TransferGate.State == ZoneTransferState.Requested;

    /// <summary>
    /// Checks that a house zone is only entered with an entry, and that the entry is for the zone it is sent with.
    /// </summary>
    /// <param name="entry">The entry, or <c>null</c> for a transfer that has none.</param>
    /// <param name="zone">The destination zone.</param>
    /// <returns><c>true</c> when the entry and the zone belong together.</returns>
    internal static bool IsValidTransfer(HouseEntry entry, string zone) {
        if (entry is null) {
            return !HouseZoneIndex.IsHouseZone(zone);
        }

        return entry.LotGid != 0 && string.Equals(entry.Zone, zone, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets the entry that the next echo is read against.
    /// </summary>
    /// <param name="entry">The entry of the transfer that was requested, or <c>null</c> when it has none.</param>
    internal void Track(HouseEntry entry) => _pending = entry;

    /// <summary>
    /// Sends the new items of an entry and the request for the house zone, and starts the wait.
    /// </summary>
    /// <param name="entry">The entry to request the zone for.</param>
    /// <param name="location">The location in the zone the character is sent to.</param>
    internal void SendRequest(HouseEntry entry, string location) {
        foreach (var itemGid in entry.NewItemGids) {
            host.SendNewItem(itemGid);
        }

        host.Send(entry.CreateRequest(location));
        host.StartTimeout();
    }

    /// <summary>
    /// Handles the client's echo of a house zone request.
    /// </summary>
    /// <param name="read">Reads the echoed message against the pending entry.</param>
    /// <param name="messageName">The message's name, for the log.</param>
    internal void OnEcho(Func<HouseEntry, HouseEchoStatus> read, string messageName) {
        var status = _pending is null ? HouseEchoStatus.NotForThisEntry : read(_pending);
        if (status == HouseEchoStatus.NotForThisEntry || host.TransferGate.State != ZoneTransferState.Requested) {
            return;
        }

        switch (status) {
            case HouseEchoStatus.Ready:
                Accept(messageName);

                break;
            case HouseEchoStatus.Patching:
                // The client is still patching the house zone archive.
                host.StartTimeout();

                break;
            default:
                Abort($"{messageName} refused by the client");

                break;
        }
    }

    /// <summary>
    /// Ends the wait when the client has not answered in time.
    /// </summary>
    internal void OnTimeout() {
        if (IsWaiting) {
            Abort("house zone request timed out");
        }
    }

    private void Accept(string messageName) {
        host.CancelTimeout();
        var previous = host.TransferGate.State;
        var accepted = host.TransferGate.TryAck();
        host.LogStep($"{messageName} echo received", previous, accepted);
        if (!accepted) {
            return;
        }

        _pending = null;
        host.BeginTransfer();
    }

    private void Abort(string step) {
        host.CancelTimeout();
        var previous = host.TransferGate.State;
        var accepted = host.TransferGate.TryNack();
        host.LogStep(step, previous, accepted);

        _pending = null;
        host.Character.QueuedLotGid = 0;
        host.Inform("The house could not be prepared. Please try again.");
    }

}
