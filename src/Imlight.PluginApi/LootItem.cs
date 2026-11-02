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
/// An item, reagent, spell or treasure card in a player's loot.
/// </summary>
/// <param name="TemplateId">The ID of its template, shared by every copy of it.</param>
/// <param name="Quantity">How many to give. An entry with none is skipped.</param>
/// <param name="Name">Its template's internal name on rolled loot. Not needed when adding loot; it is ignored.</param>
public sealed record LootItem(ulong TemplateId, int Quantity = 1, string Name = "");
