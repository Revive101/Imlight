/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Provides utility methods for handling combat wards.
/// </summary>
internal static class CombatWards {
    /// <summary>
    /// Finds all applied wards on the target that match the given spell effect.
    /// </summary>
    /// <param name="target">The target combat actor.</param>
    /// <param name="spellEffect">The spell effect to match.</param>
    /// <returns>A list of applied wards.</returns>
    internal static List<SpellEffect> FindAppliedWards(CombatDuelActorSubCircle target, SpellEffect spellEffect) {
        var appliedCharms = new List<SpellEffect>();

        // Get all wards that are currently applied to the target.
        var wards = target._hangingEffects
            .Where(x => x.m_effectType is SpellEffect.kSpellEffects.kModifyIncomingDamage
                                       or SpellEffect.kSpellEffects.kModifyIncomingDamageType
                                       or SpellEffect.kSpellEffects.kAbsorbDamage)
            .Reverse()
            .ToList();

        var seen = new HashSet<SpellEffect>();
        var currentSchool = spellEffect.m_sDamageType;
        foreach (var ward in wards) {
            // Check if this ward has already been applied.
            if (!seen.Add(ward)) {
                continue;
            }
            if (ward.m_sDamageType != currentSchool && ward.m_sDamageType != "All") {
                continue;
            }

            // If this is a prism, we need to change the damage type.
            if (ward.m_effectType == SpellEffect.kSpellEffects.kModifyIncomingDamageType) {
                currentSchool = ((MagicSchool) ward.m_effectParam).ToString();
                continue;
            }

            appliedCharms.Add(ward);
        }

        return appliedCharms;
    }

    /// <summary>
    /// Calculates the incoming damage from the given wards.
    /// </summary>
    /// <param name="wards">The array of wards.</param>
    /// <param name="initialDamage">The initial damage amount.</param>
    /// <returns>The modified incoming damage.</returns>
    internal static int GetIncomingDamageFromWards(SpellEffect[] wards, int initialDamage = 0) {
        foreach (var ward in wards) {
            if (ward.m_effectType == SpellEffect.kSpellEffects.kAbsorbDamage) {
                // Absorbs will absorb the flat damage, up to the effect param.
                var absorbAmount = ward.m_paramPerRound;
                var absorbedDamage = Math.Min(initialDamage, absorbAmount);

                ward.m_paramPerRound -= absorbedDamage;

                initialDamage -= absorbedDamage;
                continue;
            }

            var damageChange = ward.m_effectParam / 100.0f;
            initialDamage = (int) Math.Floor(initialDamage * (1 + damageChange));
        }

        return initialDamage;
    }

    /// <summary>
    /// Gets the last school from the given wards.
    /// </summary>
    /// <param name="wards">The array of wards.</param>
    /// <returns>The last school from the wards.</returns>
    internal static MagicSchool GetLastSchoolFromWards(SpellEffect[] wards, MagicSchool startingSchool) {
        foreach (var ward in wards) {
            if (ward.m_effectType == SpellEffect.kSpellEffects.kModifyIncomingDamageType) {
                return (MagicSchool) ward.m_effectParam;
            }
        }

        return startingSchool;
    }
}
