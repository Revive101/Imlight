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
using System.Linq;
using Imcodec.Math;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Statues;

internal static class StatueRegistry {

    private static readonly StatueDefinition[] s_definitions = [
        CreateMemorial()
    ];

    internal static IReadOnlyList<StatueDefinition> All => s_definitions;

    internal static IEnumerable<StatueDefinition> InZone(string zone)
        => s_definitions.Where(x => x.Enabled && x.Zone == zone);

    private static StatueDefinition CreateMemorial()
        => new(
            Id: "memorial-1",
            Enabled: false, // todo: owner fills in: set true once the values below are real
            Zone: "", // todo: owner fills in
            Location: new Vector3(), // todo: owner fills in
            Orientation: new Vector3(), // todo: owner fills in
            BaseTemplateId: 0, // todo: owner fills in
            Name: "", // todo: owner fills in
            PlaqueTitle: "", // todo: owner fills in
            PlaqueText: "", // todo: owner fills in
            Appearance: new WizardCharacterBehavior(), // todo: owner fills in
            Equipment: [], // todo: owner fills in
            SourceCharacterId: 0);

}
