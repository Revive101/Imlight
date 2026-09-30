using System;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Requirements.Contexts;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// ReqMonsterKilled: a Monster_Killed event whose monster carries one of the listed adjectives (the boss's ".AdjRef").
/// </summary>
internal sealed class ReqMonsterKilledHandler : BaseRequirementHandler<ReqMonsterKilled> {

    public override bool Evaluate(IRequirementContext context) {
        if (context is not ZoneRequirementContext { EventAdjectives: { } adjectives }) {
            return false;
        }

        // Any listed adjective matches, not only the first.
        var wanted = Requirement.m_monsterAdjectives?.Where(w => !string.IsNullOrEmpty(w)).ToList();

        return wanted is { Count: > 0 }
            && wanted.Any(w => adjectives.Any(a => string.Equals(a, w, StringComparison.OrdinalIgnoreCase)));
    }

}
