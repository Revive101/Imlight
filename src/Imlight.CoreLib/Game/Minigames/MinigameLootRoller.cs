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
 * MINIGAME LOOT ROLLER
 * ========================================================================
 *
 * PURPOSE:
 * Rolls one minigame drop table for exactly one outcome (mana, gold, item, reagent or treasure card),
 * picked by the weights of the table's Outcomes list.
 *
 * USAGE EXAMPLE:
 * var outcome = MinigameLootRoller.Roll("Minigame-Table05", wizard.GameStats.m_baseMana, Random.Shared);
 *
 * NOTE:
 * A table without outcomes is not configured yet and rolls nothing. A table with an invalid outcome rolls
 * nothing either, so a data mistake never changes the odds of the other outcomes. Each table name is
 * reported once.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Minigames;

/// <summary>
/// Rolls a minigame drop table for the one reward its slot shows.
/// </summary>
internal static class MinigameLootRoller {

    private static readonly ConcurrentDictionary<string, byte> s_reportedTables = new(StringComparer.OrdinalIgnoreCase);

    internal static MinigameOutcome Roll(string tableName, int maxMana, Random random) {
        var table = DropTableCollection.GetDropTable(tableName);
        if (table is null) {
            if (FirstReport("missing", tableName)) {
                Logger.Error("Minigame table {Table} is not in the drop tables, its slot is skipped",
                    Logger.Args(tableName));
            }

            return null;
        }

        return Roll(table, maxMana, random);
    }

    internal static MinigameOutcome Roll(DropTable table, int maxMana, Random random) {
        if (table.Outcomes is null || table.Outcomes.Count == 0) {
            if (FirstReport("empty", table.Name)) {
                Logger.Information("Minigame table {Table} has no outcomes yet, it grants nothing",
                    Logger.Args(table.Name));
            }

            return null;
        }

        foreach (var candidate in table.Outcomes) {
            var problem = FindProblem(candidate);
            if (problem is not null) {
                ReportInvalid(table.Name, problem);

                return null;
            }
        }

        var chosen = PickByWeight(table.Outcomes, random);
        var outcome = Resolve(chosen, maxMana, random);
        if (outcome is null) {
            ReportInvalid(table.Name, $"the {chosen.Kind} outcome resolved to nothing for max mana {maxMana}");
        }

        return outcome;
    }

    private static DropOutcome PickByWeight(List<DropOutcome> outcomes, Random random) {
        var total = outcomes.Sum(x => x.DropRate);
        var roll = random.NextDouble() * total;
        foreach (var outcome in outcomes) {
            roll -= outcome.DropRate;
            if (roll < 0) {
                return outcome;
            }
        }

        return outcomes[^1];
    }

    private static MinigameOutcome Resolve(DropOutcome outcome, int maxMana, Random random) {
        switch (ParseKind(outcome.Kind)) {
            case MinigameOutcomeKind.Mana:
                var mana = outcome.ManaFlat > 0 ? outcome.ManaFlat : (int) Math.Ceiling(outcome.ManaPercent * maxMana);

                return mana < 1 ? null : new MinigameOutcome(MinigameOutcomeKind.Mana, mana, 0, 1);
            case MinigameOutcomeKind.Gold:
                var gold = (int) random.NextInt64(outcome.MinGold, (long) outcome.MaxGold + 1);

                return new MinigameOutcome(MinigameOutcomeKind.Gold, gold, 0, 1);
            case MinigameOutcomeKind.Item:
                DropTableConverter.TryParseItemId(outcome.ItemId, out var itemGid);
                var itemId = itemGid.Full;
                var kind = CoreObjectFactory.GetCoreTemplate(itemId) is ReagentItemTemplate
                    ? MinigameOutcomeKind.Reagent
                    : MinigameOutcomeKind.Item;

                return new MinigameOutcome(kind, 0, itemId, outcome.Quantity);
            case MinigameOutcomeKind.TreasureCard:
                DropTableConverter.TryParseItemId(outcome.ItemId, out var cardGid);

                return new MinigameOutcome(MinigameOutcomeKind.TreasureCard, 0, cardGid.Full, outcome.Quantity);
            default:
                return null;
        }
    }

    private static string FindProblem(DropOutcome outcome) {
        if (outcome is null) {
            return "an outcome entry is empty";
        }

        if (!double.IsFinite(outcome.DropRate) || outcome.DropRate <= 0) {
            return string.Create(CultureInfo.InvariantCulture,
                $"the {outcome.Kind} outcome has the drop rate {outcome.DropRate}, it needs a weight above 0");
        }

        return ParseKind(outcome.Kind) switch {
            MinigameOutcomeKind.Mana => FindManaProblem(outcome),
            MinigameOutcomeKind.Gold => FindGoldProblem(outcome),
            MinigameOutcomeKind.Item => FindItemProblem(outcome),
            MinigameOutcomeKind.TreasureCard => FindTreasureCardProblem(outcome),
            _ => $"the outcome kind '{outcome.Kind}' is not Mana, Gold, Item or TreasureCard",
        };
    }

    private static string FindManaProblem(DropOutcome outcome) {
        var usesPercent = outcome.ManaPercent != 0;
        var usesFlat = outcome.ManaFlat != 0;
        if (usesPercent == usesFlat) {
            return "a Mana outcome needs exactly one of ManaPercent and ManaFlat";
        }

        if (usesFlat && outcome.ManaFlat < 1) {
            return $"a Mana outcome has ManaFlat {outcome.ManaFlat}, it needs at least 1";
        }

        if (usesPercent && (!(outcome.ManaPercent > 0) || outcome.ManaPercent > 1)) {
            return string.Create(CultureInfo.InvariantCulture,
                $"a Mana outcome has ManaPercent {outcome.ManaPercent}, it is a fraction of max mana above 0 up to 1");
        }

        return null;
    }

    private static string FindGoldProblem(DropOutcome outcome)
        => outcome.MinGold < 0 || outcome.MaxGold < Math.Max(1, outcome.MinGold)
            ? $"a Gold outcome has the range {outcome.MinGold} to {outcome.MaxGold}, it needs 0 <= MinGold <= MaxGold and MaxGold >= 1"
            : null;

    private static string FindItemProblem(DropOutcome outcome) {
        if (!DropTableConverter.TryParseItemId(outcome.ItemId, out var gid)) {
            return $"an Item outcome has the unreadable ItemId '{outcome.ItemId}'";
        }

        var template = CoreObjectFactory.GetCoreTemplate(gid.Full);
        if (template is not ItemTemplate) {
            return $"the Item outcome {outcome.ItemId} is not an item template";
        }

        if (outcome.Quantity < 1) {
            return $"the Item outcome {outcome.ItemId} has Quantity {outcome.Quantity}, it needs at least 1";
        }

        // Plain items are granted one at a time and the window shows no count, so only reagents may stack.
        if (outcome.Quantity > 1 && template is not ReagentItemTemplate) {
            return $"the Item outcome {outcome.ItemId} has Quantity {outcome.Quantity}, only reagents may be more than 1";
        }

        return null;
    }

    private static string FindTreasureCardProblem(DropOutcome outcome) {
        if (!DropTableConverter.TryParseItemId(outcome.ItemId, out var gid) || gid.Full > uint.MaxValue) {
            return $"a TreasureCard outcome has the unreadable ItemId '{outcome.ItemId}'";
        }

        if (CoreObjectFactory.GetCoreTemplate(gid.Full) is not SpellTemplate { m_Treasure: true }) {
            return $"the TreasureCard outcome {outcome.ItemId} is not a treasure card spell template";
        }

        return outcome.Quantity < 1
            ? $"the TreasureCard outcome {outcome.ItemId} has Quantity {outcome.Quantity}, it needs at least 1"
            : null;
    }

    private static MinigameOutcomeKind? ParseKind(string kind)
        => kind?.Trim().ToUpperInvariant() switch {
            "MANA" => MinigameOutcomeKind.Mana,
            "GOLD" => MinigameOutcomeKind.Gold,
            "ITEM" => MinigameOutcomeKind.Item,
            "TREASURECARD" => MinigameOutcomeKind.TreasureCard,
            _ => null,
        };

    private static void ReportInvalid(string tableName, string problem) {
        if (FirstReport(problem, tableName)) {
            Logger.Error("Minigame table {Table} is invalid and rolls nothing: {Problem}",
                Logger.Args(tableName, problem));
        }
    }

    private static bool FirstReport(string reason, string tableName)
        => s_reportedTables.TryAdd($"{reason}|{tableName}", 0);

}
