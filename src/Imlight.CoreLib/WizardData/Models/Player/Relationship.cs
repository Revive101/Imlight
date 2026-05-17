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
using Imlight.CoreLib.Shared.Utilities;
using Newtonsoft.Json;

namespace Imlight.CoreLib.WizardData.Models.Player;

public class Relationship {

    public ulong RelationshipId { get; init; }
    public ulong FirstPlayerId { get; set; }
    public ulong SecondPlayerId { get; set; }
    public bool AddedViaTrueFriend { get; set; }
    public bool BestFriends { get; set; }
    public bool Blocked { get; set; }
    public uint RelationshipEpochInSeconds { get; set; }
    public bool IsBrokenUp { get; set; }

    // ctor
    public Relationship(ulong firstPlayerId,
                        ulong secondPlayerId,
                        bool addedViaTrueFriend,
                        bool bestFriends,
                        bool blocked,
                        bool isBrokenUp) {
        this.RelationshipId = RandomGen.GenerateGUID();
        this.FirstPlayerId = firstPlayerId;
        this.SecondPlayerId = secondPlayerId;
        this.AddedViaTrueFriend = addedViaTrueFriend;
        this.BestFriends = bestFriends;
        this.Blocked = blocked;
        this.RelationshipEpochInSeconds = (uint)DateTimeOffset.Now.ToUnixTimeSeconds();
        this.IsBrokenUp = isBrokenUp;
    }

    // Empty ctor for database deserialization; assume all values are set
    // after deserialization.
    [JsonConstructor]
    public Relationship() { }
    
}