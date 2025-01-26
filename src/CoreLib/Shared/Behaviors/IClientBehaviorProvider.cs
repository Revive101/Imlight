/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Implementations;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

public interface IClientBehaviorProvider<out T> {

    public abstract bool NoTransfer { get; set; }
    public abstract T GetClientBehaviorInstance();
    
}
