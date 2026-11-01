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
 * STRIPED LOCKS
 * ========================================================================
 *
 * PURPOSE:
 * A fixed set of locks that serialises work per key without one lock object per key.
 *
 * USAGE EXAMPLE:
 * private static readonly StripedLocks s_locks = new();
 *
 * lock (s_locks.For(charId)) {
 *     // work on one character's data
 * }
 *
 * NOTE:
 * - Two keys can share a lock, so never take the locks of two keys at once.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System.Linq;
using System.Threading;

namespace Imlight.CoreLib.Shared.Utilities;

internal sealed class StripedLocks {

    private const int LOCK_COUNT = 32;

    private readonly Lock[] _locks = [.. Enumerable.Range(0, LOCK_COUNT).Select(_ => new Lock())];

    /// <summary>
    /// Gets the lock that covers a key.
    /// </summary>
    /// <param name="key">The key to serialise on.</param>
    /// <returns>The same lock for the same key.</returns>
    internal Lock For(ulong key) => _locks[(int) (key % LOCK_COUNT)];

}
