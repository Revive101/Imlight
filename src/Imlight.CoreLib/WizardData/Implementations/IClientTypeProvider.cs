/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.ObjectProperty;

namespace Imlight.CoreLib.WizardData.Implementations;

public interface IClientTypeProvider<T> where T : PropertyClass {

    public T GetClientTypeAlternative();

}
