/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Implementations;

public interface IClientBehaviorProvider<T> where T : BehaviorInstance {
    internal T GetClientBehaviorInstance();
}
