using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Requirements.Contexts;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// ReqTriggerObjectState: the named zone object is in the given state (the puzzle obelisks and levers).
/// An object nobody has changed is in the state it started in, which the zone seeds when it loads.
/// </summary>
internal sealed class ReqTriggerObjectStateHandler : BaseRequirementHandler<ReqTriggerObjectState> {

    public override bool Evaluate(IRequirementContext context) {
        if (context is not IZoneStateContext { ObjectStates: { } states }) {
            return false;
        }

        return states.IsIn(Requirement.m_triggerObjName.ToString(), Requirement.m_triggerObjState.ToString());
    }

}
