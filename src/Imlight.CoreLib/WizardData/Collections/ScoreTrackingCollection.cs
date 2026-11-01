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
    public const int LeaderboardSize = 10;
    private static readonly IDocumentStore s_store;

    static ScoreTrackingCollection() => s_store = PlayerDatabase.Instance.Store;

    /// <summary>
    /// Records a score for a gamer in a minigame. The collection keeps one document per gamer and minigame,
    /// holding the best score; a lower score leaves the stored one in place.
    /// </summary>
    /// <param name="highscore">The score to record.</param>
    /// <returns>The gamer's best entry for the minigame, which is what the leaderboard shows.</returns>
    public static ScoreTracking AddScoreTracking(ScoreTracking highscore) {
        using var session = s_store.OpenSession();

        // Waits for the index, so a score stored a moment ago is found too.
        var entries = session.Query<ScoreTracking>(collectionName: CollectionName)
            .Customize(query => query.WaitForNonStaleResults())
            .Where(x => x.MinigameName == highscore.MinigameName && x.GamerId == highscore.GamerId)
            .ToList();

        var best = entries.MaxBy(x => x.GameScore);
        if (best is null) {
            session.Store(highscore);
            var metadata = session.Advanced.GetMetadataFor(highscore);
            metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
            session.SaveChanges();

            return highscore;
        }

        // Older builds stored every finished game, so a gamer may still have several documents.
        foreach (var duplicate in entries.Where(x => !ReferenceEquals(x, best))) {
            session.Delete(duplicate);
        }

        if (highscore.GameScore > best.GameScore) {
            best.GameScore = highscore.GameScore;
        }

        best.GamerNameIndices = highscore.GamerNameIndices;
        best.GamerNameOverride = highscore.GamerNameOverride;
        best.GamerGender = highscore.GamerGender;
        session.SaveChanges();

        return best;
    }

    /// <summary>
    /// Gets the leaderboard for a specific minigame.
    /// </summary>
    /// <param name="minigameName">The name of the minigame to get the leaderboard for.</param>
    /// <returns>An array of the top 10 highscores for the minigame.</returns>
    public static ScoreTracking[] GetLeaderboard(string minigameName) {
        using var session = s_store.OpenSession();

        // Waits for the index, so the score stored by the last finished game is on the board.
        var highscores = session.Query<ScoreTracking>(collectionName: CollectionName)
            .Customize(query => query.WaitForNonStaleResults())
            .Where(x => x.MinigameName == minigameName)
            .OrderByDescending(x => x.GameScore)
            .Take(LeaderboardSize)
            .ToList();

        return [.. highscores];
    }

}

