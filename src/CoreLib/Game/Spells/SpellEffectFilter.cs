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
                if (!IsEnemyTarget(effect)) {
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

    public static List<Spell> FilterSpellsByBuff(List<Spell> spells) {
        var filteredSpells = new List<Spell>();
        foreach (var spell in spells) {
            // The spell type doesn't carry spell effects. That instead comes from the spell template.
            var spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(spell.m_templateID);
            if (spellTemplate == null) {
                continue;
            }

            // Filter spells that buff the caster or their team.
            foreach (var effect in spellTemplate.m_effects) {
                if (!IsBuffEffect(effect) || !IsFriendlyTarget(effect)) {
                    continue;
                }

                filteredSpells.Add(spell);
            }
        }

        return filteredSpells;
    }

    public static List<Spell> FilterSpellsByDebuff(List<Spell> spells) {
        var filteredSpells = new List<Spell>();
        foreach (var spell in spells) {
            // The spell type doesn't carry spell effects. That instead comes from the spell template.
            var spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(spell.m_templateID);
            if (spellTemplate == null) {
                continue;
            }

            // Filter spells that debuff the enemy team.
            foreach (var effect in spellTemplate.m_effects) {
                if (!IsBuffEffect(effect) || !IsEnemyTarget(effect)) {
                    continue;
                }

                filteredSpells.Add(spell);
            }
        }

        return filteredSpells;
    }

    private static bool IsEnemyTarget(SpellEffect effect) {
        var t = effect.m_effectTarget;
        return t is SpellEffect.kEffectTarget.kAtLeastOneEnemy
                 or SpellEffect.kEffectTarget.kEnemyTeam
                 or SpellEffect.kEffectTarget.kEnemyTeamAllAtOnce
                 or SpellEffect.kEffectTarget.kEnemySingle
                 or SpellEffect.kEffectTarget.kMultiTargetEnemy
                 or SpellEffect.kEffectTarget.kAtLeastOneEnemy
                 or SpellEffect.kEffectTarget.kPreselectedEnemySingle;
    }

    private static bool IsFriendlyTarget(SpellEffect effect) {
        var t = effect.m_effectTarget;
        return t is SpellEffect.kEffectTarget.kFriendlyMinion
                 or SpellEffect.kEffectTarget.kFriendlyTeam
                 or SpellEffect.kEffectTarget.kFriendlyTeamAllAtOnce
                 or SpellEffect.kEffectTarget.kFriendlySingle
                 or SpellEffect.kEffectTarget.kMultiTargetFriendly
                 or SpellEffect.kEffectTarget.kFriendlySingleNotMe;
    }

    private static bool IsDamageEffect(SpellEffect effect) {
        var t = effect.m_effectType;

        // If this is a random damage effect, enter the internal effect list of that and see
        // if any of them deal damage.
        if (effect is RandomSpellEffect rse) {
            var isRandomDamage = false;
            foreach (var e in rse.m_effectList) {
                if (IsDamageEffect(e)) {
                    isRandomDamage = true;
                    break;
                }
            }

            return isRandomDamage;
        }

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

    private static bool IsBuffEffect(SpellEffect effect) {
        var t = effect.m_effectType;
        return t is SpellEffect.kSpellEffects.kModifyIncomingDamage
                 or SpellEffect.kSpellEffects.kModifyIncomingDamageFlat
                 or SpellEffect.kSpellEffects.kModifyIncomingDamageOverTime
                 or SpellEffect.kSpellEffects.kModifyIncomingHeal
                 or SpellEffect.kSpellEffects.kModifyIncomingHealFlat
                 or SpellEffect.kSpellEffects.kModifyIncomingHealOverTime
                 or SpellEffect.kSpellEffects.kModifyIncomingArmorPiercing
                 or SpellEffect.kSpellEffects.kModifyOutgoingDamage
                 or SpellEffect.kSpellEffects.kModifyOutgoingDamageFlat
                 or SpellEffect.kSpellEffects.kModifyOutgoingHeal
                 or SpellEffect.kSpellEffects.kModifyOutgoingHealFlat
                 or SpellEffect.kSpellEffects.kModifyOutgoingArmorPiercing
                 or SpellEffect.kSpellEffects.kModifyAccuracy
                 or SpellEffect.kSpellEffects.kAbsorbDamage
                 or SpellEffect.kSpellEffects.kStunBlock
                 or SpellEffect.kSpellEffects.kStunResist;
    }
}
