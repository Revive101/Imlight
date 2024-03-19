/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Imlight.CoreLib.WizardData.Implementations;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerNPCBehavior : BehaviorInstance, IClientBehaviorProvider<NPCBehavior> {
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

    public NPCBehavior GetClientBehaviorInstance() {
        return new NPCBehavior {
            m_isMonster = IsMonster,
            m_wsNameOverride = NameOveride,
        };
    }
}
