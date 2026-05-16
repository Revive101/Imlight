/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal sealed class ReqGlobalRegistryHandler : BaseRequirementHandler<ReqGlobalRegistryValue> {

    public override bool Evaluate(IRequirementContext context) {
        return GlobalRegistryCollection.CheckGlobalRegistryRequirements(
            context.GetFullRequirementList()
        );
    }

}