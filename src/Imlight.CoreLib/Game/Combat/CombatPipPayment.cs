/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * COMBAT PIP PAYMENT
 * ========================================================================
 *
 * PURPOSE:
 * Pays a spell's pip cost from a participant's pips the way the client does, including school pips:
 * a school pip pays two for a spell of its school, and any school pip pays two for a mastered spell.
 *
 * USAGE EXAMPLE:
 * if (CombatPipPayment.CanPay(caster, spell)) { CombatPipPayment.Pay(caster, school, rank); }
 *
 * NOTE:
 * School pips are indexed in the client's order: Balance, Death, Fire, Ice, Life, Myth, Storm.
 * Shadow pips do not count toward the pip total or the pip cap.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Behaviors;
using System;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Reads, counts and spends a participant's school pips.
/// </summary>
internal static class CombatPipPayment {

    internal const int SCHOOL_PIP_SCHOOL_COUNT = 7;

    private static readonly uint[] SCHOOL_PIP_SCHOOLS_IN_CLIENT_ORDER = [
        (uint) MagicSchool.Balance,
        (uint) MagicSchool.Death,
        (uint) MagicSchool.Fire,
        (uint) MagicSchool.Ice,
        (uint) MagicSchool.Life,
        (uint) MagicSchool.Myth,
        (uint) MagicSchool.Storm,
    ];

    /// <summary>
    /// The client's index of a school that can hold school pips, or <see cref="SCHOOL_PIP_SCHOOL_COUNT"/>.
    /// </summary>
    internal static int SchoolIndexOf(uint school) {
        var index = Array.IndexOf(SCHOOL_PIP_SCHOOLS_IN_CLIENT_ORDER, school);

        return index < 0 ? SCHOOL_PIP_SCHOOL_COUNT : index;
    }

    internal static bool IsSchoolPipSchool(uint school) => SchoolIndexOf(school) < SCHOOL_PIP_SCHOOL_COUNT;

    internal static uint SchoolAt(int index) => SCHOOL_PIP_SCHOOLS_IN_CLIENT_ORDER[index];

    /// <summary>
    /// Generic, power and school pips; the count the seven-pip cap applies to.
    /// </summary>
    internal static int TotalPips(PipCount pips) {
        var total = pips.m_genericPips + pips.m_powerPips;
        for (var i = 0; i < SCHOOL_PIP_SCHOOL_COUNT; i++) {
            total += GetSchoolPips(pips, i);
        }

        return total;
    }

    internal static byte GetSchoolPips(PipCount pips, int index) => index switch {
        0 => pips.m_balancePips,
        1 => pips.m_deathPips,
        2 => pips.m_firePips,
        3 => pips.m_icePips,
        4 => pips.m_lifePips,
        5 => pips.m_mythPips,
        6 => pips.m_stormPips,
        _ => 0,
    };

    internal static void SetSchoolPips(PipCount pips, int index, byte value) {
        switch (index) {
            case 0: pips.m_balancePips = value; break;
            case 1: pips.m_deathPips = value; break;
            case 2: pips.m_firePips = value; break;
            case 3: pips.m_icePips = value; break;
            case 4: pips.m_lifePips = value; break;
            case 5: pips.m_mythPips = value; break;
            case 6: pips.m_stormPips = value; break;
        }
    }

    internal static byte GetSchoolPipCost(SpellRank rank, int index) => index switch {
        0 => rank.m_balancePips,
        1 => rank.m_deathPips,
        2 => rank.m_firePips,
        3 => rank.m_icePips,
        4 => rank.m_lifePips,
        5 => rank.m_mythPips,
        6 => rank.m_stormPips,
        _ => 0,
    };

    internal static void ClearSchoolPips(PipCount pips) {
        for (var i = 0; i < SCHOOL_PIP_SCHOOL_COUNT; i++) {
            SetSchoolPips(pips, i, 0);
        }
    }

    internal static PipCount Copy(PipCount pips) => new() {
        m_genericPips = pips.m_genericPips,
        m_powerPips = pips.m_powerPips,
        m_balancePips = pips.m_balancePips,
        m_deathPips = pips.m_deathPips,
        m_firePips = pips.m_firePips,
        m_icePips = pips.m_icePips,
        m_lifePips = pips.m_lifePips,
        m_mythPips = pips.m_mythPips,
        m_stormPips = pips.m_stormPips,
        m_shadowPips = pips.m_shadowPips,
    };

    /// <summary>
    /// Whether the caster's pips cover the spell's rank, and its school pip costs; CombatShadowPips checks the shadow pip cost.
    /// </summary>
    internal static bool CanPay(CombatDuelSubCircle caster, Spell spell)
        => TryPay(caster, spell.m_magicSchoolID, spell.m_pipCost, spell.m_pipCost?.m_spellRank ?? 0, commit: false);

    /// <summary>
    /// Spends pips for a generic cost of the given school, as far as the pips go.
    /// </summary>
    internal static void Pay(CombatDuelSubCircle caster, uint school, int genericCost)
        => TryPay(caster, school, null, genericCost, commit: true);

    /// <summary>
    /// Pays a pip cost in the client's order (CombatParticipant::DeductSpellPipCost): per-school
    /// costs first, then pairs from power pips and school pips, then single pips. Writes the pips back only
    /// when committing and the whole cost was paid.
    /// </summary>
    internal static bool TryPay(CombatDuelSubCircle caster, uint school, SpellRank rankCosts, int genericCost, bool commit) {
        var pips = caster.CombatParticipant?.m_pipCount;
        if (pips is null) {
            return false;
        }

        var generic = (int) pips.m_genericPips;
        var power = (int) pips.m_powerPips;
        var schoolPips = new int[SCHOOL_PIP_SCHOOL_COUNT];
        for (var i = 0; i < SCHOOL_PIP_SCHOOL_COUNT; i++) {
            schoolPips[i] = GetSchoolPips(pips, i);
        }

        if (rankCosts is not null && !TryTakeFixedCosts(rankCosts, schoolPips)) {
            return false;
        }

        var isMastered = school == (uint) caster.CombatParticipant.m_primaryMagicSchoolID
                         || caster.HasSchoolMastery(school);
        var schoolIndex = SchoolIndexOf(school);
        var cost = PayInPairs(genericCost, isMastered, schoolIndex, ref power, schoolPips);
        cost = PayInSingles(cost, schoolIndex, ref generic, ref power, schoolPips);
        if (cost != 0) {
            return false;
        }

        if (!commit) {
            return true;
        }

        pips.m_genericPips = (byte) generic;
        pips.m_powerPips = (byte) power;
        for (var i = 0; i < SCHOOL_PIP_SCHOOL_COUNT; i++) {
            SetSchoolPips(pips, i, (byte) schoolPips[i]);
        }

        return true;
    }

    private static bool TryTakeFixedCosts(SpellRank rankCosts, int[] schoolPips) {
        for (var i = 0; i < SCHOOL_PIP_SCHOOL_COUNT; i++) {
            var schoolCost = GetSchoolPipCost(rankCosts, i);
            if (schoolPips[i] < schoolCost) {
                return false;
            }

            schoolPips[i] -= schoolCost;
        }

        return true;
    }

    private static int PayInPairs(int cost, bool isMastered, int schoolIndex, ref int power, int[] schoolPips) {
        var hasSchoolIndex = schoolIndex < SCHOOL_PIP_SCHOOL_COUNT;
        if (cost > 1 && !isMastered && hasSchoolIndex) {
            cost -= 2 * Take(ref schoolPips[schoolIndex], cost / 2);
        }

        if (cost <= 1 || !isMastered) {
            return cost;
        }

        cost -= 2 * Take(ref power, cost / 2);
        if (hasSchoolIndex) {
            cost -= 2 * Take(ref schoolPips[schoolIndex], cost / 2);
        }

        for (var i = 0; i < SCHOOL_PIP_SCHOOL_COUNT && cost >= 2; i++) {
            cost -= 2 * Take(ref schoolPips[i], cost / 2);
        }

        return cost;
    }

    private static int PayInSingles(int cost, int schoolIndex, ref int generic, ref int power, int[] schoolPips) {
        cost -= Take(ref generic, cost);
        cost -= Take(ref power, cost);
        if (schoolIndex < SCHOOL_PIP_SCHOOL_COUNT) {
            cost -= Take(ref schoolPips[schoolIndex], cost);
        }

        for (var i = 0; i < SCHOOL_PIP_SCHOOL_COUNT && cost > 0; i++) {
            cost -= Take(ref schoolPips[i], cost);
        }

        return cost;
    }

    private static int Take(ref int pool, int wanted) {
        var taken = Math.Clamp(wanted, 0, pool);
        pool -= taken;

        return taken;
    }

}
