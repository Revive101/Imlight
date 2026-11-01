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
 * Persists attics. The document id is derived from the character, so creating an attic is idempotent however many
 * sessions race.
 *
 * USAGE EXAMPLE:
 * var attic = AtticCollection.Load(charId);
 * if (attic is null) { AtticCollection.TryCreate(newAttic, out attic); }
 *
 * NOTE:
 * - Reads are direct loads by document id, never queries, because a query can be stale.
 * - An item that moves between an attic and a backpack is written with SaveWithBackpack: one transaction, so a crash
 *   between the two cannot lose or duplicate it.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Raven.Client.Documents;
using Raven.Client.Exceptions;

namespace Imlight.CoreLib.WizardData.Collections;

public static class AtticCollection {

    public const string COLLECTION_NAME = "Attics";
    private static readonly IDocumentStore s_store;

    static AtticCollection() => s_store = PlayerDatabase.Instance.Store;

    /// <summary>
    /// Loads a character's attic.
    /// </summary>
    /// <param name="charId">The character.</param>
    /// <returns>The attic, or <c>null</c> when the character has none.</returns>
    public static Attic Load(ulong charId) {
        using var session = s_store.OpenSession();

        return session.Load<Attic>(Attic.GetDocumentId(charId));
    }

    /// <summary>
    /// Stores a new attic unless one already exists under the same id.
    /// </summary>
    /// <param name="attic">The attic to store; its <see cref="Attic.Id"/> must be set.</param>
    /// <param name="winner"><paramref name="attic"/> when it was stored, otherwise the attic that already existed; <c>null</c> when the store failed.</param>
    /// <returns><c>true</c> when <paramref name="attic"/> was stored.</returns>
    public static bool TryCreate(Attic attic, out Attic winner) {
        try {
            using var session = s_store.OpenSession();

            session.Store(attic, changeVector: string.Empty, attic.Id);
            session.Advanced.GetMetadataFor(attic)[Raven.Client.Constants.Documents.Metadata.Collection] = COLLECTION_NAME;
            session.SaveChanges();
            winner = attic;

            return true;
        } catch (ConcurrencyException) {
            winner = Load(attic.CharId);

            return false;
        } catch (Exception ex) {
            Logger.Error("Failed to create attic {AtticId}: {Reason}", Logger.Args(attic.Id, ex.Message));
            winner = null;

            return false;
        }
    }

    /// <summary>
    /// Writes the changing parts of an attic: its revisions, next item id and items.
    /// </summary>
    /// <param name="attic">The attic to save.</param>
    /// <returns><c>true</c> when the stored attic now matches <paramref name="attic"/>.</returns>
    public static bool Save(Attic attic) {
        try {
            using var session = s_store.OpenSession();

            var stored = session.Load<Attic>(attic.Id);
            if (stored is null) {
                return false;
            }

            CopyChanging(attic, stored);
            session.SaveChanges();

            return true;
        } catch (Exception ex) {
            Logger.Error("Failed to save attic {AtticId}: {Reason}", Logger.Args(attic.Id, ex.Message));

            return false;
        }
    }

    /// <summary>
    /// Writes an attic together with its owner's backpack in one transaction, so an item that moves between them
    /// is either in the attic or in the backpack, never lost and never in both.
    /// </summary>
    /// <param name="attic">The attic to save.</param>
    /// <param name="owner">The owner, whose backpack is written as it is in memory.</param>
    /// <param name="addedItem">An item that entered the backpack and has no document yet, or <c>null</c>. An item that came out of an attic keeps its document.</param>
    /// <returns><c>true</c> when both are stored; <c>false</c> when neither changed.</returns>
    public static bool SaveWithBackpack(Attic attic, Wizard owner, WizClientObjectItem addedItem = null)
        => WizardItemCollection.SaveContainerWithBackpack<Attic>(attic.Id, owner, stored => CopyChanging(attic, stored), addedItem);

    /// <summary>
    /// Writes an attic and deletes the document of an item that left it for good, in one transaction.
    /// </summary>
    /// <param name="attic">The attic to save.</param>
    /// <param name="itemGid">The global id of the deleted item; 0 for an item that has no document.</param>
    /// <returns><c>true</c> when the attic is stored and the document is deleted; <c>false</c> when neither changed.</returns>
    public static bool SaveDeleting(Attic attic, ulong itemGid) {
        try {
            using var session = s_store.OpenSession();

            var stored = session.Load<Attic>(attic.Id);
            if (stored is null) {
                return false;
            }

            CopyChanging(attic, stored);
            if (itemGid != 0 && !WizardItemCollection.DeleteInSession(session, attic.CharId, itemGid)) {
                Logger.Warning("Item {ItemId} of character {CharId} has no stored document to delete.", Logger.Args(itemGid, attic.CharId));
            }

            session.SaveChanges();

            return true;
        } catch (Exception ex) {
            Logger.Error("Failed to save attic {AtticId} and delete item {ItemId}: {Reason}", Logger.Args(attic.Id, itemGid, ex.Message));

            return false;
        }
    }

    internal static void CopyChanging(Attic from, Attic to) {
        to.Revisions = new(from.Revisions);
        to.NextItemId = from.NextItemId;
        to.Items = [.. from.Items];
    }

}
