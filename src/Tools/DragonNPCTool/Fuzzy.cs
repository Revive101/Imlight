/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using FuzzySharp;

namespace DragonNPCTool;

public class FuzzyFindMatchResult {
    public string Option { get; init; }
    public int Similarity { get; init; }
}

public static class Fuzzy {
    private const int FuzzyFindThreshold = 20;

    public static IEnumerable<FuzzyFindMatchResult> FindClosestMatches(string userInput, IEnumerable<string> options, int threshold = FuzzyFindThreshold) {
        var closestMatches = new List<FuzzyFindMatchResult>();

        foreach (var option in options) {
            var similarity = Fuzz.PartialRatio(userInput, option);
            if (similarity >= FuzzyFindThreshold) {
                closestMatches.Add(new FuzzyFindMatchResult { Option = option, Similarity = similarity });
            }
        }

        closestMatches.Sort((x, y) => y.Similarity.CompareTo(x.Similarity));
        return closestMatches.Take(200).ToList();
    }
}
