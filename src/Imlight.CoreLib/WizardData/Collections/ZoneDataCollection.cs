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
*/

using System;
using System.Linq;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.World;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Collections;

public static class ZoneDataCollection {

    private const string CollectionName = "ZoneTransfer";

    private static readonly IDocumentStore s_store;

    static ZoneDataCollection() 
        => s_store = WorldDatabase.Instance.Store;

    /// <summary>
    /// Adds the zone data to the database.
    /// </summary>
    /// <param name="zoneData"></param>
    public static void AddZoneData(WizardZoneData zoneData) {
        using var session = s_store.OpenSession();

        // Store the zone data in the database.
        session.Store(zoneData);
        var metadata = session.Advanced.GetMetadataFor(zoneData);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }

    /// <summary>
    /// Updates the zone data in the database.
    /// </summary>
    /// <param name="zoneName"></param>
    /// <returns></returns>
    public static WizardZoneData GetZoneData(string zoneName) {
        using var session = s_store.OpenSession();

        // Retrieve the zone data from the database.
        var zoneData = session
            .Query<WizardZoneData>(collectionName: CollectionName)
            .FirstOrDefault(x => x.ZoneName == zoneName);

        return zoneData;
    }

    /// <summary>
    /// Retrieves a random zone data from the database.
    /// </summary>
    /// <returns> A random zone data. </returns>
    /// <remarks> This method is used for the April Fools event. </remarks>
    public static WizardZoneData GetAprilFoolsRandomZoneData() {
        using var session = s_store.OpenSession();

        // Retrieve a random zone data from the database.
        var zoneDatas = session
            .Query<WizardZoneData>(collectionName: CollectionName)
            .ToList();
        var random = new Random();
        var index = random.Next(0, zoneDatas.Count);
        var zoneData = zoneDatas[index];

        return zoneData;
    }

}
