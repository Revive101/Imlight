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
 * ZONE TRANSFER GATE
 * ========================================================================
 *
 * PURPOSE:
 * Tracks one zone transfer from the client request through the
 * server transfer, so each request produces at most one transfer.
 *
 * USAGE EXAMPLE:
 * ZoneService owns one gate per session. A transition method returns
 * true only when the caller may proceed with that step.
 *
 * NOTE:
 * Pure state, no I/O, so the transitions can be driven without a session.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/09/2026
 */

namespace Imlight.CoreLib.Game.Services;

internal enum ZoneTransferState {
    None,
    Requested,
    Acked,
    Sent,
}

internal sealed class ZoneTransferGate {

    public ZoneTransferState State { get; private set; } = ZoneTransferState.None;

    public bool IsQueued => State != ZoneTransferState.None;

    public bool TryRequest() {
        if (State is ZoneTransferState.Acked or ZoneTransferState.Sent) {
            return false;
        }

        State = ZoneTransferState.Requested;

        return true;
    }

    public bool TryAck() => TryAdvanceFromRequested();

    public bool TryRetry() => TryAdvanceFromRequested();

    public bool TryNack() {
        if (State != ZoneTransferState.Requested) {
            return false;
        }

        State = ZoneTransferState.None;

        return true;
    }

    public bool TrySend() {
        if (State != ZoneTransferState.Acked) {
            return false;
        }

        State = ZoneTransferState.Sent;

        return true;
    }

    private bool TryAdvanceFromRequested() {
        if (State != ZoneTransferState.Requested) {
            return false;
        }

        State = ZoneTransferState.Acked;

        return true;
    }
}
