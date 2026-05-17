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

using Newtonsoft.Json;
using Imcodec.IO;
using Imcodec.Types;
using Imlight.CoreLib.Shared.Behaviors;

namespace Imlight.CoreLib.WizardData.Models.Misc;

public sealed class ScoreTracking 
    : IClientBehaviorProvider<Imcodec.ObjectProperty.TypeCache.ScoreTracking> {


    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    public string MinigameName { get; set; }
    public float GameScore { get; set; }
    public GID GamerId { get; set; }
    public uint GamerNameIndices { get; set; }
    public WideByteString GamerNameOverride { get; set; }
    public ByteString GamerGender { get; set; }

    public Imcodec.ObjectProperty.TypeCache.ScoreTracking GetClientBehaviorInstance() 
        => new() {
            m_gamerID = GamerId,
            m_gamerName = GamerNameOverride,
            m_gamerNameCode = GamerNameIndices,
            m_gameScore = GameScore,
            m_gamerGender = GamerGender,
            m_gamerRace = "Human"
        };

}