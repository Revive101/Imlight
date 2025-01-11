/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Imlight.CoreLib.WizardData.Implementations;
using Newtonsoft.Json;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerNPCBehavior : IClientBehaviorProvider<NPCBehavior> {
    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    public bool BossMob { get; set; }
    public float Intelligence { get; set; }
    public float SelfishFactor { get; set; }
    public int AggressiveFactor { get; set; }
    public int StartingHealth { get; set; }
    public MagicSchool School { get; set; }
    public int Level { get; set; }
    public bool TurnTowardsPlayer { get; set; }
    public bool IsMonster { get; set; }
    public string NameOveride { get; set; }

    public NPCBehavior GetClientBehaviorInstance() => new() {
        m_isMonster = IsMonster,
        m_wsNameOverride = NameOveride,
    };
}
