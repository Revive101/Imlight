using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.Shared.Items;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// Compares a value with a ReqNumeric's number, the way the client's numeric requirements do.
/// </summary>
internal static class NumericRequirement {

    public static bool Compare(ReqNumeric requirement, float actual) {
        var required = requirement.m_numericValue;

        return requirement.m_operatorType switch {
            OPERATOR_TYPE.OPERATOR_EQUALS => actual == required,
            OPERATOR_TYPE.OPERATOR_GREATER_THAN => actual > required,
            OPERATOR_TYPE.OPERATOR_LESS_THAN => actual < required,
            OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ => actual >= required,
            OPERATOR_TYPE.OPERATOR_LESS_THAN_EQ => actual <= required,
            _ => false
        };
    }

    public static float Amount(int current, int max, bool isPercent)
        => isPercent ? (max > 0 ? current * 100f / max : 0f) : current;

}

/// <summary>
/// ReqHealth: the player's current health (or its percentage of the maximum) against a number.
/// </summary>
internal sealed class ReqHealthHandler : BaseRequirementHandler<ReqHealth> {

    public override bool Evaluate(IRequirementContext context) {
        var stats = context.GetWizard()?.GameStats;
        if (stats is null) {
            return false;
        }

        return NumericRequirement.Compare(Requirement,
            NumericRequirement.Amount(stats.m_currentHitpoints, stats.m_baseHitpoints, Requirement.m_isPercent));
    }

}

/// <summary>
/// ReqMana: the player's current mana (or its percentage of the maximum) against a number.
/// </summary>
internal sealed class ReqManaHandler : BaseRequirementHandler<ReqMana> {

    public override bool Evaluate(IRequirementContext context) {
        var stats = context.GetWizard()?.GameStats;
        if (stats is null) {
            return false;
        }

        return NumericRequirement.Compare(Requirement,
            NumericRequirement.Amount(stats.m_currentMana, stats.m_baseMana, Requirement.m_isPercent));
    }

}

/// <summary>
/// ReqHasGold: the player carries at least the gold.
/// </summary>
internal sealed class ReqHasGoldHandler : BaseRequirementHandler<ReqHasGold> {

    public override bool Evaluate(IRequirementContext context) {
        var stats = context.GetWizard()?.GameStats;

        return stats is not null && stats.m_currentGold >= Requirement.m_gold;
    }

}

/// <summary>
/// ReqHasItems: the player carries at least the quantity (one when unset) of items, matched by template id
/// or, when there is none, by the adjective the item template lists.
/// </summary>
internal sealed class ReqHasItemsHandler : BaseRequirementHandler<ReqHasItems> {

    public override bool Evaluate(IRequirementContext context) {
        var items = context.GetWizard()?.InventoryBehavior?.Items;
        if (items is null) {
            return false;
        }

        var wantedId = Requirement.m_templateID;
        var adjective = Requirement.m_adjective;
        if (wantedId == 0 && string.IsNullOrEmpty(adjective)) {
            return false;
        }

        var found = 0;
        foreach (var item in items) {
            if (item is null) {
                continue;
            }

            if (wantedId != 0) {
                if (item.m_templateID == wantedId) {
                    found++;
                }

                continue;
            }

            var template = ItemHelper.GetItemTemplate(item);
            if (template?.m_adjectiveList?.Exists(a => string.Equals(a, adjective, System.StringComparison.OrdinalIgnoreCase)) == true) {
                found++;
            }
        }

        return found >= System.Math.Max(1, Requirement.m_quantity);
    }

}

/// <summary>
/// ReqHasRegistryEntry: the player has the (global) registry entry.
/// </summary>
internal sealed class ReqHasRegistryEntryHandler : BaseRequirementHandler<ReqHasRegistryEntry> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();

        return wizard is not null
            && !string.IsNullOrEmpty(Requirement.m_registryEntry)
            && wizard.HasRegistryValue(Requirement.m_registryEntry);
    }

}

/// <summary>
/// ReqIsInParty: the player is in a group with someone else.
/// </summary>
internal sealed class ReqIsInPartyHandler : BaseRequirementHandler<ReqIsInParty> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();

        return wizard is not null
            && GroupRegistry.TryGetGroup(wizard.CharId, out var group)
            && group.Members.Count > 1;
    }

}

/// <summary>
/// ReqHasExpansion: every player owns every expansion.
/// </summary>
internal sealed class ReqHasExpansionHandler : BaseRequirementHandler<ReqHasExpansion> {

    public override bool Evaluate(IRequirementContext context) => true;

}

/// <summary>
/// ReqIsCSR: nobody here is a customer service representative.
/// </summary>
internal sealed class ReqIsCSRHandler : BaseRequirementHandler<ReqIsCSR> {

    public override bool Evaluate(IRequirementContext context) => false;

}
