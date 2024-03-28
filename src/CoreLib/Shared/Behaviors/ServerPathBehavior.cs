/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.WizardData.Implementations;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerPathBehavior : ServerBehaviorInstance {
    public override bool NoTransfer { get; set; } = false;

    public PathBehaviorTemplate.PathType PathType { get; set; }
    public GID PathId { get; set; }
    public int PathDirection { get; set; }
    public List<PathBehaviorTemplate.Action> Actions { get; set; }
    public uint PauseChance { get; set; }
    public float PauseTime { get; set; }
    public float MovementSpeed { get; set; }
    public float MovementMultiplier { get; set; }
    public bool IsMovingCreature => MovementSpeed > 0.0f;

    public override PathBehaviorClient GetClientBehaviorInstance() => new() {
        // Nothing here for client.
    };
}
