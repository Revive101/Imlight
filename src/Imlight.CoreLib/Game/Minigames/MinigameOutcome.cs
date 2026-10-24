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
 * MINIGAME OUTCOME
 * ========================================================================
 *
 * PURPOSE:
 * The single reward a minigame drop table rolled for one reward slot, resolved to concrete numbers
 * and template ids.
 *
 * USAGE EXAMPLE:
 * MinigameLootRoller.Roll returns one; MinigameRewardGranter grants it and shows it in the window.
 *
 * NOTE:
 * Amount is the mana points or the gold. TemplateId and Quantity are set for items, reagents and treasure cards.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

namespace Imlight.CoreLib.Game.Minigames;

internal enum MinigameOutcomeKind {

    Mana,
    Gold,
    Item,
    Reagent,
    TreasureCard,

}

internal sealed record MinigameOutcome(MinigameOutcomeKind Kind, int Amount, ulong TemplateId, int Quantity);
