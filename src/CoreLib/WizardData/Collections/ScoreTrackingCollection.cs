/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
    public static void AddHighscore(ScoreTracking highscore) {
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

        var highscores = session.Query<ScoreTracking>()
            .Where(x => x.MinigameName == minigameName)
            .OrderByDescending(x => x.GameScore)
            .Take(10)
            .ToList();

        return [.. highscores];
    }

}

