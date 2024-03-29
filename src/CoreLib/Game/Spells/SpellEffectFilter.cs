/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common;
using Imlight.Common.Cryptography;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Spells;

public static class SpellEffectFilter {
    public static List<Spell> FilterSpellsByOutgoingDamage(List<Spell> spells) {
        var filteredSpells = new List<Spell>();
        foreach (var spell in spells) {
            // The spell type doesn't carry spell effects. That instead comes from the spell template.
            var spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(spell.m_templateID);
            if (spellTemplate == null) {
                continue;
            }

            // Filter spells that deal damage to the enemy team.
            foreach (var effect in spellTemplate.m_effects) {
                if (!IsEnemyDamageEffect(effect)) {
                    continue;
                }
                if (!IsDamageEffect(effect)) {
                    continue;
                }

                filteredSpells.Add(spell);
            }
        }

        return filteredSpells;
    }

    public static List<Spell> FilterSpellsByHealing(List<Spell> spells) {
        var filteredSpells = new List<Spell>();
        foreach (var spell in spells) {
            // The spell type doesn't carry spell effects. That instead comes from the spell template.
            var spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(spell.m_templateID);
            if (spellTemplate == null) {
                continue;
            }

            // Filter spells that heal the caster or their team.
            foreach (var effect in spellTemplate.m_effects) {
                if (!IsHealingEffect(effect)) {
                    continue;
                }

                filteredSpells.Add(spell);
            }
        }

        return filteredSpells;
    }

    private static bool IsEnemyDamageEffect(SpellEffect effect) {
        var t = effect.m_effectTarget;
        return t is SpellEffect.kEffectTarget.kAtLeastOneEnemy
                 or SpellEffect.kEffectTarget.kEnemyTeam
                 or SpellEffect.kEffectTarget.kEnemyTeamAllAtOnce
                 or SpellEffect.kEffectTarget.kEnemySingle
                 or SpellEffect.kEffectTarget.kMultiTargetEnemy
                 or SpellEffect.kEffectTarget.kAtLeastOneEnemy
                 or SpellEffect.kEffectTarget.kPreselectedEnemySingle;
    }

    private static bool IsDamageEffect(SpellEffect effect) {
        var t = effect.m_effectType;
        return t is SpellEffect.kSpellEffects.kDamage
                 or SpellEffect.kSpellEffects.kDamageNoCrit
                 or SpellEffect.kSpellEffects.kDamageOverTime
                 or SpellEffect.kSpellEffects.kDamagePerTotalPipPower;
    }

    private static bool IsHealingEffect(SpellEffect effect) {
        var t = effect.m_effectType;
        return t is SpellEffect.kSpellEffects.kHeal
                 or SpellEffect.kSpellEffects.kHealOverTime
                 or SpellEffect.kSpellEffects.kHealByWard
                 or SpellEffect.kSpellEffects.kHealPercent
                 or SpellEffect.kSpellEffects.kMaxHealthHeal
                 or SpellEffect.kSpellEffects.kSetHealPercent;
    }
}
