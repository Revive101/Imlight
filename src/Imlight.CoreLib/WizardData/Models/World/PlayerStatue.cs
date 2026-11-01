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
 * PLAYER STATUE
 * ========================================================================
 *
 * PURPOSE:
 * One SpiralDB statue record: where it stands, which template it is built on, its look, name and plaque.
 *
 * USAGE EXAMPLE:
 * SpiralDB.TryGetPlayerStatue(id, out var statue);
 *
 * NOTE:
 * Id is the file name key: lowercase a-z, 0-9 and '-', 1 to 32 characters. The base template decides the kind of statue.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Collections.Generic;
using Imcodec.Math;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.World;

public class PlayerStatue {

    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Zone { get; set; } = string.Empty;
    public Vector3 Location { get; set; }
    public Vector3 Orientation { get; set; }
    public uint BaseTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint NameKeys { get; set; }
    public string PlaqueTitle { get; set; } = string.Empty;
    public string PlaqueText { get; set; } = string.Empty;
    public WizardCharacterBehavior Appearance { get; set; } = new();
    public List<WizardEquippedItemInfo> Equipment { get; set; } = [];
    public ulong SourceCharacterId { get; set; }

}
