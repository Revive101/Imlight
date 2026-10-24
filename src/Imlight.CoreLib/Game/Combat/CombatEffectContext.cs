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
 * COMBAT EFFECT CONTEXT
 * ========================================================================
 * 
 * PURPOSE:
 * Carries the per-action values every effect handler needs: the source spell's
 * template id and the rolled crit multiplier.
 * 
 * USAGE EXAMPLE:
 * var context = new CombatEffectContext(spell.m_templateID, critMultiplier);
 * CombatEffectApplicator.ApplyEffect(effect, charms, caster, targets, context);
 * 
 * NOTE:
 * One context per queued action, shared by all effects of that action.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// The per-action values passed to every effect handler.
/// </summary>
/// <param name="SpellTemplateId">The template id of the spell being cast.</param>
/// <param name="CritMultiplier">The crit multiplier rolled for the action.</param>
internal readonly record struct CombatEffectContext(uint SpellTemplateId, float CritMultiplier);
