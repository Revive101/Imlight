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

using System;
using Imcodec.ObjectProperty.TypeCache;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerPetItemBehavior : IClientBehaviorProvider<ClientPetItemBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    public eGender Gender;
    public eRace Race;
    public byte Level;
    public uint XP;
    public uint RequiredXP;
    public uint HatchedTimeInSeconds;

    public ClientPetItemBehavior GetClientBehaviorInstance() => new() {
        m_level = Level,
        m_XP = XP,
        m_hatchedTimeSecs = HatchedTimeInSeconds,
        m_requiredXP = RequiredXP,
    };

}