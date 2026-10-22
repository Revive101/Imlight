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
 */

using System.Collections.Generic;
using Imcodec.Math;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Statues;

internal sealed record StatueDefinition(
    string Id,
    bool Enabled,
    string Zone,
    Vector3 Location,
    Vector3 Orientation,
    uint BaseTemplateId,
    string Name,
    uint NameKeys,
    string PlaqueTitle,
    string PlaqueText,
    WizardCharacterBehavior Appearance,
    IReadOnlyList<WizardEquippedItemInfo> Equipment,
    ulong SourceCharacterId);
