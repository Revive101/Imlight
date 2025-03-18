/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;

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

    private static bool IsEnemyTarget(SpellEffect effect)
        => effect.m_effectTarget is kEffectTarget.kAtLeastOneEnemy
                 or kEffectTarget.kEnemyTeam
                 or kEffectTarget.kEnemyTeamAllAtOnce
                 or kEffectTarget.kEnemySingle
                 or kEffectTarget.kMultiTargetEnemy
                 or kEffectTarget.kAtLeastOneEnemy
                 or kEffectTarget.kPreselectedEnemySingle;

    private static bool IsFriendlyTarget(SpellEffect effect)
        => effect.m_effectTarget is kEffectTarget.kFriendlyMinion
                 or kEffectTarget.kFriendlyTeam
                 or kEffectTarget.kFriendlyTeamAllAtOnce
                 or kEffectTarget.kFriendlySingle
                 or kEffectTarget.kMultiTargetFriendly
                 or kEffectTarget.kFriendlySingleNotMe;

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

        return t is kSpellEffects.kDamage
                 or kSpellEffects.kDamageNoCrit
                 or kSpellEffects.kDamageOverTime
                 or kSpellEffects.kDamagePerTotalPipPower;
    }

    private static bool IsHealingEffect(SpellEffect effect) =>
        effect.m_effectType is kSpellEffects.kHeal
                 or kSpellEffects.kHealOverTime
                 or kSpellEffects.kHealByWard
                 or kSpellEffects.kHealPercent
                 or kSpellEffects.kMaxHealthHeal
                 or kSpellEffects.kSetHealPercent;

    private static bool IsBuffEffect(SpellEffect effect) =>
        effect.m_effectType is kSpellEffects.kModifyIncomingDamage
                 or kSpellEffects.kModifyIncomingDamageFlat
                 or kSpellEffects.kModifyIncomingDamageOverTime
                 or kSpellEffects.kModifyIncomingHeal
                 or kSpellEffects.kModifyIncomingHealFlat
                 or kSpellEffects.kModifyIncomingHealOverTime
                 or kSpellEffects.kModifyIncomingArmorPiercing
                 or kSpellEffects.kModifyOutgoingDamage
                 or kSpellEffects.kModifyOutgoingDamageFlat
                 or kSpellEffects.kModifyOutgoingHeal
                 or kSpellEffects.kModifyOutgoingHealFlat
                 or kSpellEffects.kModifyOutgoingArmorPiercing
                 or kSpellEffects.kModifyAccuracy
                 or kSpellEffects.kAbsorbDamage
                 or kSpellEffects.kStunBlock
                 or kSpellEffects.kStunResist;

}
