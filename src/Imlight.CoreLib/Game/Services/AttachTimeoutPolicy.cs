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
 * ATTACH TIMEOUT POLICY
 * ========================================================================
 *
 * PURPOSE:
 * Decides what to do with a session whose MSG_ATTACH never arrived: close it so
 * the client resends, or redirect the client to its fallback zone.
 *
 * USAGE EXAMPLE:
 * AttachService calls Decide when the attach timer fires.
 *
 * NOTE:
 * Pure function, no I/O, so the cases can be driven without a session.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/09/2026
 */

namespace Imlight.CoreLib.Game.Services;

internal enum AttachTimeoutAction {
    Close,
    Redirect,
}

internal static class AttachTimeoutPolicy {

    public static AttachTimeoutAction Decide(bool sessionValid, int priorTimeoutsForIp, bool hasFallback) {
        if (!hasFallback) {
            return AttachTimeoutAction.Close;
        }

        // The client only resends a lost ATTACH when the socket closes after its handshake completed.
        return sessionValid && priorTimeoutsForIp == 0
            ? AttachTimeoutAction.Close
            : AttachTimeoutAction.Redirect;
    }

}
