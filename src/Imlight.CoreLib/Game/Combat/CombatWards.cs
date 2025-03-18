/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Spells;

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
    internal static List<SpellEffect> FindAppliedWards(CombatDuelSubCircle target,
                                                       SpellEffect spellEffect,
                                                       kHangingDisposition disposition = kHangingDisposition.kBoth) {
        var appliedCharms = new List<SpellEffect>();

        // Choose the beneficial or harmful wards based on the disposition.
        var beneficialWards = GetBeneficialWards(target);
        var harmfulWards = GetHarmfulWards(target);
        var wards = disposition switch {
            kHangingDisposition.kBeneficial => beneficialWards,
            kHangingDisposition.kHarmful => harmfulWards,
            _ => [.. beneficialWards, .. harmfulWards]
        };

        return [.. wards.DistinctBy(x => x.m_spellTemplateID)];
    }

    /// <summary>
    /// Calculates the incoming damage from the given wards.
    /// </summary>
    /// <param name="wards">The array of wards.</param>
    /// <param name="initialDamage">The initial damage amount.</param>
    /// <returns>The modified incoming damage.</returns>
    internal static int GetIncomingDamageFromWards(List<SpellEffect> wards, int initialDamage = 0) {
        foreach (var ward in wards) {
            if (ward.m_effectType == kSpellEffects.kAbsorbDamage) {
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
    /// Retrieves a list of wards based on the specified magic school.
    /// </summary>
    /// <param name="wards">An array of spell effects representing the wards.</param>
    /// <param name="school">The magic school to filter the wards by.</param>
    /// <param name="finalSchool">The final magic school determined by the wards.</param>
    /// <returns>A list of spell effects that match the specified magic school.</returns>
    internal static List<SpellEffect> GetWardsBySchool(SpellEffect[] wards, string school, out string finalSchool) {
        var schoolWards = new List<SpellEffect>();
        finalSchool = school;

        foreach (var ward in wards) {
            if (ward.m_effectType == kSpellEffects.kModifyIncomingDamageType) {
                finalSchool = MagicSchools.GetMagicSchool(ward.m_effectParam)?.m_schoolName ?? school;
            }

            if (ward.m_sDamageType == finalSchool.ToString() || ward.m_sDamageType == "All") {
                schoolWards.Add(ward);
            }
        }

        return schoolWards;
    }

    private static List<SpellEffect> GetBeneficialWards(CombatDuelSubCircle target) => [.. target._hangingEffects
            .Where(x => x.m_effectType is kSpellEffects.kModifyIncomingDamage && x.m_effectParam > 0
                                       || x.m_effectType == kSpellEffects.kAbsorbDamage)
            .Reverse()];

    private static List<SpellEffect> GetHarmfulWards(CombatDuelSubCircle target) => [.. target._hangingEffects
            .Where(x => x.m_effectType is kSpellEffects.kModifyIncomingDamage && x.m_effectParam < 0 ||
                        x.m_effectType == kSpellEffects.kModifyIncomingDamageType)
            .Reverse()];

}
