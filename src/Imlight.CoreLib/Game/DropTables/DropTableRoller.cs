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
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.DropTables;

public static class DropTableRoller {

    /// <summary>
    /// Rolls multiple drop tables with requirement validation against the provided player context
    /// </summary>
    /// <param name="dropTableNames">Array of drop table names to roll</param>
    /// <param name="playerRef">Player actor reference for requirements context</param>
    /// <param name="playerObj">Player core object for requirements context</param>
    /// <param name="wizard">Player wizard data for requirements context</param>
    /// <returns>Combined DropTableResult containing all rolled loot that passed requirements</returns>
    public static DropTableResult Roll(
        string[] dropTableNames,
        IActorRef playerRef,
        CoreObject playerObj,
        Wizard wizard) {

        if (dropTableNames == null || dropTableNames.Length == 0) {
            return CreateEmptyResult("no_tables_specified");
        }

        var random = new Random();

        // Create a new type which represents the combined result
        // of rolling the specified drop table(s).
        var combinedResult = new DropTableResult {
            DropTableId = string.Join(",", dropTableNames),
            GoldAmount = 0,
            ExperienceAmount = 0,
            TrainingPoints = 0,
            GrantsPotionSlot = false,
            Items = []
        };

        // Roll each drop table.
        foreach (var tableName in dropTableNames) {
            if (string.IsNullOrWhiteSpace(tableName)) {
                continue;
            }

            // Load drop table from database.
            var dropTable = DropTableCollection.GetDropTable(tableName);
            if (dropTable == null) {
                Logger.Warning("Roller encountered missing drop table {0} that was not present in the database.",
                    Logger.Args(tableName));

                continue;
            }

            // Roll the table with context for item requirements.
            var tableResult = RollSingleTable(dropTable, playerRef, playerObj, wizard, random);

            // Accumulate rewards.
            combinedResult.GoldAmount += tableResult.GoldAmount;
            combinedResult.ExperienceAmount += tableResult.ExperienceAmount;
            combinedResult.TrainingPoints += tableResult.TrainingPoints;
            combinedResult.Items.AddRange(tableResult.Items);
            combinedResult.GrantsPotionSlot = tableResult.GrantsPotionSlot;
            
        }

        return combinedResult;
    }
    
    private static DropTableResult RollSingleTable(DropTable dropTable,
                                                             IActorRef playerRef,
                                                             CoreObject playerObj,
                                                             Wizard wizard,
                                                             Random random) {

        var result = new DropTableResult {
            DropTableId = dropTable.Id
        };

        // Items don't have drop chance, only the table itself does.
        // Roll that chance to see if we get any rewards at all.
        var rollValue = random.NextDouble();
        if (rollValue > dropTable.RollChance) {
            // Failed to enter table, return empty result
            return result;
        }

        // We entered the table, roll all rewards.
        result.GoldAmount = RollGoldAmount(dropTable, random);
        result.ExperienceAmount = dropTable.ExperienceAmount;
        result.MagicSchool = "All"; 
        result.TrainingPoints = dropTable.TrainingPoints;
        
        // Roll items with requirements validation.
        result.Items = RollTableItems(dropTable, playerRef, playerObj, wizard, random);
            
        return result;
    }

    private static int RollGoldAmount(DropTable dropTable, Random random) {
        if (dropTable.MinGold <= 0 && dropTable.MaxGold <= 0) {
            return 0;
        }

        // If min equals max, return fixed amount (typical for quest rewards).
        if (dropTable.MinGold == dropTable.MaxGold) {
            return dropTable.MinGold;
        }

        // If only max is set, treat min as 0.
        var minGold = Math.Max(0, dropTable.MinGold);
        var maxGold = Math.Max(minGold, dropTable.MaxGold);

        // Generate random amount in range.
        return random.Next(minGold, maxGold + 1);
    }

    private static List<DropItemResult> RollTableItems(DropTable dropTable,
                                                                  IActorRef playerRef,
                                                                  CoreObject playerObj,
                                                                  Wizard wizard,
                                                                  Random random) {
        
        var results = new List<DropItemResult>();

        if (dropTable.Items == null || dropTable.Items.Count == 0) {
            return results;
        }

        // Create a working copy of items to roll from.
        var availableItems = dropTable.Items
            .Where(item => !string.IsNullOrEmpty(item.ItemId))
            .ToList();

        // Keep rolling until we find an item that meets requirements or run out of items.
        while (availableItems.Count > 0) {
            // Randomly select an item from the remaining pool.
            var randomIndex = random.Next(availableItems.Count);
            var selectedItem = availableItems[randomIndex];

            // Check if this item meets requirements.
            bool meetsRequirements = true;
            if (selectedItem.Requirements != null) {
                var context = new GenericRequirementContext(
                    requirements: selectedItem.Requirements,
                    playerRef: playerRef,
                    playerObj: playerObj,
                    wizard: wizard
                );

                meetsRequirements = RequirementDispatcher.EvaluateRequirements(selectedItem.Requirements, context);
            }

            if (meetsRequirements) {
                // Found a valid item, add it to results and stop rolling.
                results.Add(new DropItemResult {
                    ItemId = selectedItem.ItemId,
                    ItemName = selectedItem.ItemName,
                    Quantity = 1
                });

                break;
            } else {
                // Item doesn't meet requirements, remove it from the pool and try again.
                availableItems.RemoveAt(randomIndex);
            }
        }

        return results;
    }

    private static DropTableResult CreateEmptyResult(string dropTableId = "") 
        => new() {
            DropTableId = dropTableId,
            GoldAmount = 0,
            ExperienceAmount = 0,
            TrainingPoints = 0,
            Items = []
        };

}