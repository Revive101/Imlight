/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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