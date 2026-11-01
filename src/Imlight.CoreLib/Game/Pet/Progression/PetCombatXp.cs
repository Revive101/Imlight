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
 * PET COMBAT XP
 * ========================================================================
 *
 * PURPOSE:
 * Gives the equipped pet the XP its combat XP talents earn when its owner wins a duel.
 *
 * USAGE EXAMPLE:
 * PetCombatXp.Award(SessionActor.ActorRef, GetActiveGameObject(), GetActiveWizard());
 *
 * NOTE:
 * The only combat pet XP in the client data is a talent whose effect names a loot table. The XP is the
 * ExperienceAmount of the tables it rolls, because a SpiralDB drop table has no pet XP entry. A table
 * SpiralDB does not have is skipped and logged once, never replaced with invented XP.
 *
 * TODO:
 * - Do talents of the same effect category stack? Every expressed one rolls its table here.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Linq;
using System;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Pet.Progression;

/// <summary>
/// Pet XP from winning a duel.
/// </summary>
internal static class PetCombatXp {

    /// <summary>
    /// Finds the loot tables the pet's expressed talents roll after a duel and SpiralDB has.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <returns>The distinct table names. A name SpiralDB lacks is left out and logged the first time.</returns>
    internal static string[] FindTables(WizClientObjectItem pet) {
        if (!PetProgression.TryGetPet(pet, out var behavior, out _)) {
            return [];
        }

        var names = (behavior.m_expressedTalents ?? [])
            .Select(talentId => PetTalentRegistry.TryGetById(talentId, out var talent) ? talent.Template as PetTalentTemplate : null)
            .Where(template => template is not null)
            .SelectMany(template => template.m_effectList.OfType<CombatPetXPEffectInfo>())
            .Select(effect => effect.m_lootTableName.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal);

        return DropTableRoller.KeepKnownTables(names);
    }

    /// <summary>
    /// Rolls the equipped pet's combat XP tables and gives the pet the XP they hold.
    /// </summary>
    /// <param name="session">The owner's session.</param>
    /// <param name="playerObj">The owner's game object, which the table requirements are checked against.</param>
    /// <param name="wizard">The owner.</param>
    /// <returns>What the pet received, which is <see cref="PetXpResult.None"/> if there is no pet, no table or no XP.</returns>
    internal static PetXpResult Award(IActorRef session, CoreObject playerObj, Wizard wizard) {
        var pet = wizard.GetEquippedPetItem();
        var tables = FindTables(pet);
        if (tables.Length == 0) {
            return PetXpResult.None;
        }

        var loot = DropTableRoller.Roll(tables, session, playerObj, wizard);

        return PetXpAward.Give(session, playerObj, wizard, pet, (uint) Math.Max(0, loot.ExperienceAmount), PetXpSource.Combat);
    }

}
