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
 * Last Updated: 10/24/2026
 */

namespace Imlight.PluginApi;

/// <summary>
/// A snapshot of an item a wizard owns. It does not change when the item does.
/// </summary>
/// <param name="ItemId">The item's unique ID. Pass it to <see cref="IPlayer.RemoveItem"/> to remove this exact item.</param>
/// <param name="TemplateId">The ID of the item's template, shared by every copy of the item.</param>
/// <param name="Name">The item's internal name.</param>
public sealed record PlayerItem(ulong ItemId, ulong TemplateId, string Name);
