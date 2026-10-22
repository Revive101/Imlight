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
 * Persists houses. The document id is derived from the lot, so creating a house is idempotent however
 * many logins race, and the owner's lot pointer is written in the same transaction.
 * 
 * USAGE EXAMPLE:
 * var house = HouseCollection.Load(lotGid);
 * if (house is null) { HouseCollection.TryCreate(newHouse, owner, out house); }
 * 
 * NOTE:
 * - Reads are direct loads by document id, never queries, because a query can be stale.
 * - An item that moves between a house and a backpack is written with SaveWithBackpack, and objects that move between
 *   a house and an attic with SaveWithAttic: one transaction, so a crash between the two cannot lose or duplicate them.
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
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions;

namespace Imlight.CoreLib.WizardData.Collections;

public static class HouseCollection {

    public const string COLLECTION_NAME = "Houses";
    private static readonly IDocumentStore s_store;

    static HouseCollection() => s_store = PlayerDatabase.Instance.Store;

    /// <summary>
    /// Loads a house.
    /// </summary>
    /// <param name="lotGid">The lot of the house.</param>
    /// <returns>The house, or <c>null</c> when none is stored.</returns>
    public static House Load(ulong lotGid) {
        using var session = s_store.OpenSession();

        return session.Load<House>(House.GetDocumentId(lotGid));
    }

    /// <summary>
    /// Stores a new house unless one already exists under the same id.
    /// </summary>
    /// <param name="house">The house to store; its <see cref="House.Id"/> must be set.</param>
    /// <param name="winner">
    /// <paramref name="house"/> when it was stored, otherwise the house that already existed.
    /// <c>null</c> when the store failed for another reason.
    /// </param>
    /// <returns><c>true</c> when <paramref name="house"/> was stored.</returns>
    public static bool TryCreate(House house, out House winner) {
        try {
            using var session = s_store.OpenSession();

            StoreNew(session, house);
            session.SaveChanges();
            winner = house;

            return true;
        } catch (ConcurrencyException) {
            winner = Load(house.LotGid);

            return false;
        } catch (Exception ex) {
            Logger.Error("Failed to create house {HouseId}: {Reason}", Logger.Args(house.Id, ex.Message));
            winner = null;

            return false;
        }
    }

    /// <summary>
    /// Stores a new house together with its owner's pointer to the lot, in one transaction. When the owner
    /// already points at another lot for the template, <paramref name="house"/> takes that lot instead, so
    /// racing creators end up with one lot.
    /// </summary>
    /// <param name="house">The house to store; its <see cref="House.Id"/> and lot may be replaced.</param>
    /// <param name="owner">The owner, whose in-memory lot pointer follows the stored one.</param>
    /// <param name="winner">
    /// <paramref name="house"/> when it was stored, otherwise the house that already existed under the lot.
    /// <c>null</c> when the store failed for another reason.
    /// </param>
    /// <returns><c>true</c> when <paramref name="house"/> was stored.</returns>
    public static bool TryCreate(House house, Wizard owner, out House winner) {
        try {
            var stored = WizardCollection.UpdateCharacterWithSession(owner.CharId, (session, storedOwner) => {
                storedOwner.HouseLots ??= [];
                if (storedOwner.HouseLots.TryGetValue(house.TemplateId, out var pointedLot)
                    && pointedLot != house.LotGid) {
                    house.LotGid = pointedLot;
                    house.Id = House.GetDocumentId(pointedLot);
                }

                storedOwner.HouseLots[house.TemplateId] = house.LotGid;
                StoreNew(session, house);

                return true;
            });
            if (!stored) {
                winner = null;

                return false;
            }

            owner.HouseLots[house.TemplateId] = house.LotGid;
            winner = house;

            return true;
        } catch (ConcurrencyException) {
            winner = Load(house.LotGid);
            if (winner is not null) {
                owner.HouseLots[house.TemplateId] = house.LotGid;
            }

            return false;
        } catch (Exception ex) {
            Logger.Error("Failed to create house {HouseId}: {Reason}", Logger.Args(house.Id, ex.Message));
            winner = null;

            return false;
        }
    }

    /// <summary>
    /// Writes the changing parts of a house: its revisions, bonus slots, next item id and items.
    /// </summary>
    /// <param name="house">The house to save.</param>
    /// <returns><c>true</c> when the stored house now matches <paramref name="house"/>.</returns>
    public static bool Save(House house) {
        try {
            using var session = s_store.OpenSession();

            var stored = session.Load<House>(house.Id);
            if (stored is null) {
                return false;
            }

            CopyChanging(house, stored);
            session.SaveChanges();

            return true;
        } catch (Exception ex) {
            Logger.Error("Failed to save house {HouseId}: {Reason}", Logger.Args(house.Id, ex.Message));

            return false;
        }
    }

    /// <summary>
    /// Writes a house together with its owner's backpack in one transaction, so an item that moves between
    /// them is either in the house or in the backpack, never lost and never in both.
    /// </summary>
    /// <param name="house">The house to save.</param>
    /// <param name="owner">The owner, whose backpack is written as it is in memory.</param>
    /// <param name="addedItem">An item that entered the backpack and has no document yet, or <c>null</c>. An item that came out of a house keeps its document.</param>
    /// <returns><c>true</c> when both are stored; <c>false</c> when neither changed.</returns>
    public static bool SaveWithBackpack(House house, Wizard owner, WizClientObjectItem addedItem = null)
        => WizardItemCollection.SaveContainerWithBackpack<House>(house.Id, owner, stored => CopyChanging(house, stored), addedItem);

    /// <summary>
    /// Writes a house together with its owner's attic in one transaction, so objects that move between them are
    /// either in the house or in the attic, never lost and never in both.
    /// </summary>
    /// <param name="house">The house to save.</param>
    /// <param name="attic">The owner's attic to save.</param>
    /// <returns><c>true</c> when both are stored; <c>false</c> when neither changed.</returns>
    public static bool SaveWithAttic(House house, Attic attic) {
        try {
            using var session = s_store.OpenSession();

            var storedHouse = session.Load<House>(house.Id);
            var storedAttic = session.Load<Attic>(attic.Id);
            if (storedHouse is null || storedAttic is null) {
                return false;
            }

            CopyChanging(house, storedHouse);
            AtticCollection.CopyChanging(attic, storedAttic);
            session.SaveChanges();

            return true;
        } catch (Exception ex) {
            Logger.Error("Failed to save house {HouseId} with attic {AtticId}: {Reason}",
                Logger.Args(house.Id, attic.Id, ex.Message));

            return false;
        }
    }

    private static void StoreNew(IDocumentSession session, House house) {
        session.Store(house, changeVector: string.Empty, house.Id);
        var metadata = session.Advanced.GetMetadataFor(house);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = COLLECTION_NAME;
    }

    private static void CopyChanging(House from, House to) {
        to.Revisions = new(from.Revisions);
        to.BonusSlots = from.BonusSlots;
        to.NextItemId = from.NextItemId;
        to.Items = [.. from.Items];
    }

}
