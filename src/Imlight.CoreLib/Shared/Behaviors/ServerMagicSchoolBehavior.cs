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

public enum MagicSchool {

    None = 0,
    Ice = 72777,
    Life = 2330892,
    Fire = 2343174,
    Myth = 2448141,
    Death = 78318724,
    Storm = 83375795,
    Balance = 1027491821,
    Sun = 78483,
    Star = 2625203,
    Moon = 2504141,
    Shadow = 1429009101,

}

[Serializable]
public class ServerMagicSchoolBehavior : IClientBehaviorProvider<ClientMagicSchoolBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    public MagicSchool MagicSchool;
    public int ExperiencePoints;
    public int Level;
    public int TrainingPoints;
    public int OverflowXp;
    public int LevelIsLocked;
    public uint EquippedTeleportEffect;

    public ClientMagicSchoolBehavior GetClientBehaviorInstance() => new() {
        m_schoolOfFocus = (uint) MagicSchool,
        m_experiencePoints = ExperiencePoints,
        m_level = Level,
        m_trainingPoints = TrainingPoints,
        m_overflowXP = OverflowXp,
        m_levelLocked = LevelIsLocked,
        m_equippedTeleportEffect = EquippedTeleportEffect
    };
    
}
