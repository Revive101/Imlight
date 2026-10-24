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
 * CHEST REWARD GRANTER
 * ========================================================================
 *
 * PURPOSE:
 * Rolls and grants the loot of a silver chest the player won, and builds the loot list the minigame
 * rewards window shows for it.
 *
 * USAGE EXAMPLE:
 * var reward = ChestRewardGranter.Grant(playerActor, character, chestTemplateId, lootTables);
 *
 * NOTE:
 * The window draws at most 4 slots, and the gold always takes the second one. Only items and treasure
 * cards besides gold are drawn, so other granted loot is left out of the window's list. The gold shown
 * is what fit into the pouch.
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
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Minigames;

internal static class ChestRewardGranter {

    private const int WINDOW_SLOTS = 4;

    internal static MinigameRewardResult NoReward()
        => new(DropTableConverter.CreateEmpty(), 0, false);

    internal static MinigameRewardResult Grant(IActorRef playerActor,
                                               CHARACTER_103_PROTOCOL.MSG_CHARACTER character,
                                               ulong chestTemplateId,
                                               IReadOnlyList<string> lootTables) {
        var wizard = character.Wizard;
        var rolled = ChestLootRoller.Roll(lootTables, playerActor, character.WizardGameObject, wizard);
        FitGoldToPouch(rolled, wizard);

        var granted = LootGranter.Grant(playerActor, wizard, rolled);
        var lootList = DropTableConverter.ToLootInfoList(granted);

        // The window draws items and treasure cards only (and the gold), so it is given only those.
        var drawn = lootList.m_loot.Where(x => x is ItemLootInfo or TreasureCardLootInfo).ToList();
        if (drawn.Count < lootList.m_loot.Count) {
            Logger.Information("Chest {Template} granted loot that the rewards window cannot show to {Wizard}",
                Logger.Args(chestTemplateId, wizard.PlayerNameBehavior.GetWizardName()));
        }

        lootList.m_loot = drawn;

        var slots = drawn.Count + (lootList.m_goldInfo is { m_goldAmount: > 0 } ? 1 : 0);
        if (slots > WINDOW_SLOTS) {
            Logger.Warning("Chest {Template} granted {Slots} rewards, the rewards window shows {WindowSlots}",
                Logger.Args(chestTemplateId, slots, WINDOW_SLOTS));
        }

        var finalPhase = MinigameRewardTiers.FinalPhaseForSlots(Math.Min(slots, WINDOW_SLOTS));

        return new MinigameRewardResult(lootList, finalPhase, slots > 0);
    }

    private static void FitGoldToPouch(DropTableResult rolled, Wizard wizard) {
        var room = Math.Max(0, wizard.GameStats.m_baseGoldPouch - wizard.GameStats.m_currentGold);
        if (rolled.GoldAmount <= room) {
            return;
        }

        if (room == 0) {
            Logger.Information("Chest gold of {Gold} dropped, the pouch of {Wizard} is full",
                Logger.Args(rolled.GoldAmount, wizard.PlayerNameBehavior.GetWizardName()));
        }

        rolled.GoldAmount = room;
    }

}
