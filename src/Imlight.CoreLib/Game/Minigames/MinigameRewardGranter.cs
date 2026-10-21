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
 * MINIGAME REWARD GRANTER
 * ========================================================================
 *
 * PURPOSE:
 * Grants the rewards a finished kiosk minigame earned (one outcome per reward table of the reached
 * tier) and builds the loot list the rewards window shows.
 *
 * USAGE EXAMPLE:
 * var reward = MinigameRewardGranter.Grant(playerActor, wizard, minigameInfo, score);
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Minigames;

internal static class MinigameRewardGranter {

    internal static MinigameRewardResult Grant(IActorRef playerActor, Wizard wizard, MinigameInfo info, int score) {
        var thresholdIndex = MinigameRewardTiers.GetThresholdIndex(info.m_scoreThresholds, score);
        var tableNames = MinigameRewardTiers.GetLootTableNames(
            info.m_rewardTable.Select(x => (string) x).ToList(), thresholdIndex);

        var loot = new List<LootInfo>();
        var goldGranted = 0;
        foreach (var tableName in tableNames) {
            var outcome = MinigameLootRoller.Roll(tableName, wizard.GameStats.m_baseMana, Random.Shared);
            if (outcome is null) {
                continue;
            }

            if (outcome.Kind == MinigameOutcomeKind.Gold) {
                goldGranted += GrantGold(playerActor, wizard, outcome);

                continue;
            }

            var granted = GrantLoot(playerActor, wizard, outcome);
            if (granted is not null) {
                loot.Add(granted);
            }
        }

        var slots = loot.Count + (goldGranted > 0 ? 1 : 0);
        var lootList = new LootInfoList {
            m_loot = loot,
            m_goldInfo = new GoldLootInfo { m_lootType = LOOT_TYPE.LOOT_TYPE_GOLD, m_goldAmount = goldGranted },
            m_lootRarityList = new LootRarityList { m_loot = [] },
        };

        var finalPhase = slots == 0 ? 0 : Math.Min(thresholdIndex, MinigameRewardTiers.FinalPhaseForSlots(slots));

        return new MinigameRewardResult(lootList, finalPhase, slots > 0);
    }

    private static int GrantGold(IActorRef playerActor, Wizard wizard, MinigameOutcome outcome) {
        // The pouch holds only so much: the window shows what fit.
        var room = Math.Max(0, wizard.GameStats.m_baseGoldPouch - wizard.GameStats.m_currentGold);
        var gold = Math.Min(outcome.Amount, room);
        if (gold <= 0) {
            if (outcome.Amount > 0) {
                Logger.Information("Minigame gold reward of {Gold} dropped, the pouch of {Wizard} is full",
                    Logger.Args(outcome.Amount, wizard.PlayerNameBehavior.GetWizardName()));
            }

            return 0;
        }

        LootGranter.UpdateWizardGold(playerActor, wizard, gold);

        return gold;
    }

    private static LootInfo GrantLoot(IActorRef playerActor, Wizard wizard, MinigameOutcome outcome) {
        switch (outcome.Kind) {
            case MinigameOutcomeKind.Mana:
                LootGranter.GrantMana(playerActor, wizard, outcome.Amount);

                return new ManaLootInfo { m_lootType = LOOT_TYPE.LOOT_TYPE_MANA, m_manaAmount = outcome.Amount };
            case MinigameOutcomeKind.Item:
                var item = new DropItemResult { ItemId = outcome.TemplateId.ToString(), Quantity = 1 };
                if (!LootGranter.GrantItem(playerActor, wizard, item, outcome.TemplateId)) {
                    return null;
                }

                return new ItemLootInfo { m_lootType = LOOT_TYPE.LOOT_TYPE_ITEM, m_itemID = outcome.TemplateId, m_numItems = 1 };
            case MinigameOutcomeKind.Reagent:
                var reagentCount = LootGranter.GrantReagent(playerActor, wizard, outcome.TemplateId, outcome.Quantity);
                if (reagentCount == 0) {
                    Logger.Information("Minigame reagent reward {Reagent} dropped, the stack of {Wizard} is full",
                        Logger.Args(outcome.TemplateId, wizard.PlayerNameBehavior.GetWizardName()));

                    return null;
                }

                return new ItemLootInfo { m_lootType = LOOT_TYPE.LOOT_TYPE_ITEM, m_itemID = outcome.TemplateId, m_numItems = reagentCount };
            case MinigameOutcomeKind.TreasureCard:
                if (CoreObjectFactory.GetCoreTemplate(outcome.TemplateId) is not SpellTemplate card) {
                    return null;
                }

                var cardCount = LootGranter.GrantTreasureCards(playerActor, wizard, card, (uint) outcome.TemplateId, outcome.Quantity);

                return new TreasureCardLootInfo {
                    m_lootType = LOOT_TYPE.LOOT_TYPE_TREASURE_CARD,
                    m_spellID = (uint) outcome.TemplateId,
                    m_numItems = cardCount,
                };
            default:
                return null;
        }
    }

}

internal sealed record MinigameRewardResult(LootInfoList Loot, int FinalPhase, bool Success);
