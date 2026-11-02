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
/// What every copy of an item has in common, from its template.
/// </summary>
/// <param name="TemplateId">The ID of the item's template.</param>
/// <param name="Name">Its English name, or its internal name if it has none.</param>
/// <param name="InternalName">The name of its template in the game data.</param>
/// <param name="School">The school it belongs to, or <see cref="School.None"/> if it has none.</param>
public sealed record ItemInfo(ulong TemplateId, string Name, string InternalName, School School);
