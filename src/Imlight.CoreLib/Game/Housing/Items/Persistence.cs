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
 * The blob revisions and the way a failed write takes a change back, shared by the house and the attic.
 *
 * USAGE EXAMPLE:
 * var undo = Persistence.BumpRevision(document.Revisions, subType, out var revision);
 * var saved = Persistence.Persist(apply, undo, write, "house 1");
 *
 * NOTE:
 * - A blob without a stored revision is at 1.
 * - Persist runs a change, writes it, and runs the undo when the write fails or throws.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System;
using System.Collections.Generic;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Housing.Items;

internal static class Persistence {

    /// <summary>
    /// Gets the stored revision of a blob.
    /// </summary>
    /// <param name="revisions">The revisions of a document, by subtype.</param>
    /// <param name="subType">The blob's subtype.</param>
    /// <returns>The revision; 1 when none is stored.</returns>
    internal static uint RevisionOf(IReadOnlyDictionary<string, uint> revisions, string subType)
        => revisions.TryGetValue(subType, out var revision) ? revision : 1;

    /// <summary>
    /// Moves the revision of a blob up by one.
    /// </summary>
    /// <param name="revisions">The revisions of a document, by subtype.</param>
    /// <param name="subType">The blob's subtype.</param>
    /// <param name="revision">The new revision.</param>
    /// <returns>An action that puts the revision back as it was, stored or not.</returns>
    internal static Action BumpRevision(IDictionary<string, uint> revisions, string subType, out uint revision) {
        var hadRevision = revisions.TryGetValue(subType, out var stored);
        revision = (hadRevision ? stored : 1) + 1;
        revisions[subType] = revision;

        return () => {
            if (hadRevision) {
                revisions[subType] = stored;
            } else {
                revisions.Remove(subType);
            }
        };
    }

    /// <summary>
    /// Applies a change, writes it, and undoes it when the write fails or throws.
    /// </summary>
    /// <param name="apply">Changes the state in memory.</param>
    /// <param name="undo">Puts the state back as it was before <paramref name="apply"/>.</param>
    /// <param name="write">Writes the changed document.</param>
    /// <param name="what">What is written, for the log.</param>
    /// <returns><c>true</c> when the write succeeded.</returns>
    internal static bool Persist(Action apply, Action undo, Func<bool> write, string what) {
        apply();
        if (TryWrite(write, what)) {
            return true;
        }

        undo();

        return false;
    }

    private static bool TryWrite(Func<bool> write, string what) {
        try {
            return write();
        } catch (Exception ex) {
            Logger.Error("Failed to write {What}: {Reason}", Logger.Args(what, ex.Message));

            return false;
        }
    }

}
