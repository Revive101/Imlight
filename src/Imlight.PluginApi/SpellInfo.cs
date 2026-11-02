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
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/01/2026
 */

namespace Imlight.PluginApi;

/// <summary>
/// A spell as its template defines it.
/// </summary>
/// <param name="TemplateId">The ID of the spell's template.</param>
/// <param name="Name">Its internal name, the same name spell events give.</param>
/// <param name="DisplayName">Its English name, or its internal name if it has none.</param>
/// <param name="School">The school it belongs to, or <see cref="School.None"/> if it has none.</param>
/// <param name="PipCost">Its rank in pips.</param>
/// <param name="Accuracy">Its base chance to hit, as a percentage.</param>
/// <param name="IsTreasureCard">True if the template is a treasure card.</param>
public sealed record SpellInfo(uint TemplateId, string Name, string DisplayName, School School, int PipCost,
    int Accuracy, bool IsTreasureCard);
