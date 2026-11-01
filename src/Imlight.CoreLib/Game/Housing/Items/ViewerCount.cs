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
 * Counts the players who are looking at a live state and retires the state when the last one leaves.
 *
 * USAGE EXAMPLE:
 * if (viewers.Enter()) {
 *     // ...
 *     var lastOut = viewers.Leave();
 * }
 *
 * NOTE:
 * - A retired state takes no new viewers; whoever wants it gets a new state from the registry.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System.Threading;

namespace Imlight.CoreLib.Game.Housing.Items;

internal sealed class ViewerCount {

    private readonly Lock _lock = new();
    private int _viewers;
    private bool _retired;

    internal bool IsRetired {
        get {
            lock (_lock) {
                return _retired;
            }
        }
    }

    /// <summary>
    /// Counts a viewer in.
    /// </summary>
    /// <returns><c>false</c> when the state was retired by its last viewer leaving.</returns>
    internal bool Enter() {
        lock (_lock) {
            if (_retired) {
                return false;
            }

            _viewers++;

            return true;
        }
    }

    /// <summary>
    /// Counts a viewer out. The state is retired when the last viewer leaves.
    /// </summary>
    /// <returns><c>true</c> when this was the last viewer and the state is now retired.</returns>
    internal bool Leave() {
        lock (_lock) {
            if (_viewers > 0) {
                _viewers--;
            }

            if (_viewers > 0 || _retired) {
                return false;
            }

            _retired = true;

            return true;
        }
    }

}
