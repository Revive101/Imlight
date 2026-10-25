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
 * PVP
 * ========================================================================
 * 
 * PURPOSE:
 * One row of the level scaling data: the stat values a wizard of one school has at one level.
 * 
 * USAGE EXAMPLE:
 * var health = row.Get("MaxHealth");
 * 
 * NOTE:
 * The keys are the property names of the data row without their prefix, such as MaxHealth or FireDamage. A key the row does not have reads as 0.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.Game.PvP.Rules;

internal sealed class LevelScaledRow(string school, int level, IReadOnlyDictionary<string, float> values) {

    public string School { get; } = school;
    public int Level { get; } = level;

    public float Get(string key)
        => values.GetValueOrDefault(key);

}
