using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal static class ZoneScriptRequirement {

    /// <summary>
    /// The player's game object id, which per-player tokens are keyed by (0 when there is none).
    /// </summary>
    public static ulong PlayerId(IRequirementContext context) => context.GetPlayerObj()?.m_globalID.Full ?? 0;

}

/// <summary>
/// ReqZoneToken: the zone token is enabled (for the zone, or for this player).
/// </summary>
internal sealed class ReqZoneTokenHandler : BaseRequirementHandler<ReqZoneToken> {

    public override bool Evaluate(IRequirementContext context)
        => context is IZoneStateContext { ScriptState: { } state }
        && state.IsTokenEnabled(Requirement.m_tokenName, ZoneScriptRequirement.PlayerId(context));

}

/// <summary>
/// ReqZoneTokenValue: the zone token's value lies between the minimum and the maximum, inclusive.
/// </summary>
internal sealed class ReqZoneTokenValueHandler : BaseRequirementHandler<ReqZoneTokenValue> {

    public override bool Evaluate(IRequirementContext context) {
        if (context is not IZoneStateContext { ScriptState: { } state }) {
            return false;
        }

        var value = state.GetTokenValue(Requirement.m_tokenName, ZoneScriptRequirement.PlayerId(context));

        return value >= Requirement.m_minValue && value <= Requirement.m_maxValue;
    }

}

/// <summary>
/// ReqZoneCounter: the zone counter compares with the number.
/// </summary>
internal sealed class ReqZoneCounterHandler : BaseRequirementHandler<ReqZoneCounter> {

    public override bool Evaluate(IRequirementContext context)
        => context is IZoneStateContext { ScriptState: { } state }
        && NumericRequirement.Compare(Requirement, state.GetCounter(Requirement.m_counterName));

}

/// <summary>
/// ReqGetEncounterVariable: a puzzle variable a trigger set is on.
/// </summary>
internal sealed class ReqGetEncounterVariableHandler : BaseRequirementHandler<ReqGetEncounterVariable> {

    public override bool Evaluate(IRequirementContext context)
        => context is IZoneStateContext { ScriptState: { } state }
        && state.GetVariable(Requirement.m_questName, Requirement.m_varName);

}

/// <summary>
/// ReqEncounterComplete: the encounter name is a dungeon quest's name (KatzLab "MB-YARD3-C01-002"), and the
/// requirement holds once the player has completed that quest.
/// </summary>
internal sealed class ReqEncounterCompleteHandler : BaseRequirementHandler<ReqEncounterComplete> {

    private const string QUEST_COMPLETED_ENTRY = "Complete";

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        var name = Requirement.m_encounterName;

        if (context is IZoneStateContext { InstanceQuestCompleted: { } instanceCompleted }
            && instanceCompleted(name) is { } completed) {
            return completed;
        }

        return wizard is not null
            && !string.IsNullOrEmpty(name)
            && wizard.HasQuestRegistryValue(name, QUEST_COMPLETED_ENTRY);
    }

}

/// <summary>
/// ReqTriggerState: a trigger of the zone is enabled or disabled. INACTIVE is a disabled trigger; ACTIVE and READY
/// both mean an enabled one (the data uses READY for triggers a puzzle step enables and ACTIVE for those the zone
/// starts with, and asks about both only to tell "enabled" from "disabled").
/// </summary>
internal sealed class ReqTriggerStateHandler : BaseRequirementHandler<ReqTriggerState> {

    private const string STATE_INACTIVE = "TRIGGER_STATE_INACTIVE";
    private const string STATE_ACTIVE = "TRIGGER_STATE_ACTIVE";
    private const string STATE_READY = "TRIGGER_STATE_READY";

    public override bool Evaluate(IRequirementContext context) {
        if (context is not IZoneStateContext { ScriptState: { } state }) {
            return false;
        }

        var enabled = state.IsTriggerEnabled(Requirement.m_triggerName);

        return Requirement.m_triggerState switch {
            STATE_INACTIVE => !enabled,
            STATE_ACTIVE or STATE_READY => enabled,
            _ => false,
        };
    }

}
