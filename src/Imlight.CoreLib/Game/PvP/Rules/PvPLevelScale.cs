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
 * The level a league scales wizards to, and whether wizards below that level are scaled up.
 * 
 * USAGE EXAMPLE:
 * var scale = new PvPLevelScale(effectName, scaleToLevel, scaleUp);
 * 
 * NOTE:
 * 
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

namespace Imlight.CoreLib.Game.PvP.Rules;

internal readonly record struct PvPLevelScale(string EffectName, int ScaleToLevel, bool ScaleUp);
