/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.CoreLib.Shared.Behaviors;

public interface IClientBehaviorProvider<out T> {

    public abstract bool NoTransfer { get; set; }
    public abstract T GetClientBehaviorInstance();
    
}
