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

using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Misc;
using Raven.Client.Documents;
using System.Linq;

namespace Imlight.CoreLib.WizardData.Collections;

public static class ScoreTrackingCollection {

    public const string CollectionName = "ScoreTracking";
    private static readonly IDocumentStore s_store;

    static ScoreTrackingCollection() => s_store = PlayerDatabase.Instance.Store;

    /// <summary>
    /// Adds a highscore to the collection.
    /// </summary>
    /// <param name="highscore">The highscore to add.</param>
    public static void AddScoreTracking(ScoreTracking highscore) {
        using var session = s_store.OpenSession();

        session.Store(highscore);
        var metadata = session.Advanced.GetMetadataFor(highscore);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }

    /// <summary>
    /// Gets the leaderboard for a specific minigame.
    /// </summary>
    /// <param name="minigameName">The name of the minigame to get the leaderboard for.</param>
    /// <returns>An array of the top 10 highscores for the minigame.</returns>
    public static ScoreTracking[] GetLeaderboard(string minigameName) {
        using var session = s_store.OpenSession();

        var highscores = session.Query<ScoreTracking>(collectionName: CollectionName)
            .Where(x => x.MinigameName == minigameName)
            .OrderByDescending(x => x.GameScore)
            .Take(10)
            .ToList();

        return [.. highscores];
    }

}

