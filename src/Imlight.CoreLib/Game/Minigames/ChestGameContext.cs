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
 * CHEST GAME CONTEXT
 * ========================================================================
 *
 * PURPOSE:
 * Carries one silver chest game from the chest through the player's session to the minigame process:
 * which chest, which player, which game, and where the player returns to afterwards.
 *
 * USAGE EXAMPLE:
 * var context = new ChestGameContext(Self, templateId, tables, 1, charId, gameName, zone, location, false);
 *
 * NOTE:
 * ReturnLocation is a compact location string as Util.GetCompactStringFromVector writes it.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using Akka.Actor;

namespace Imlight.CoreLib.Game.Minigames;

/// <summary>
/// One silver chest game: the chest that offered it, the player, the game and the return point.
/// </summary>
/// <param name="ChestRef">The chest component that is told how the game went, null when the result ran without a chest.</param>
/// <param name="ChestTemplateId">The template id of the chest, 0 without one.</param>
/// <param name="LootTables">The resolved drop table names the loot is rolled from.</param>
/// <param name="SuccessScore">The score from which the game counts as won.</param>
/// <param name="CharId">The character playing the game.</param>
/// <param name="MinigameName">The name of the minigame in the minigame configuration.</param>
/// <param name="ReturnZone">The zone the player returns to when the game ends.</param>
/// <param name="ReturnLocation">The compact location in that zone.</param>
/// <param name="ReturnPrivate">Whether the player returns to a private copy of the zone.</param>
internal sealed record ChestGameContext(
    IActorRef ChestRef,
    ulong ChestTemplateId,
    IReadOnlyList<string> LootTables,
    int SuccessScore,
    ulong CharId,
    string MinigameName,
    string ReturnZone,
    string ReturnLocation,
    bool ReturnPrivate);
