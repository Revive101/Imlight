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
 * PET XP AWARD
 * ========================================================================
 *
 * PURPOSE:
 * Gives a pet XP the way a player should see it: adds it, saves the pet, tells the client and grants
 * the loot of every level reached. Also applies a set of stat changes together with the XP its points are worth.
 *
 * USAGE EXAMPLE:
 * PetXpAward.Give(SessionActor.ActorRef, playerObj, wizard, pet, 4, PetXpSource.Combat);
 *
 * NOTE:
 * The client expects the level-up windows first, then the pet's new state, then the floating XP text.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.MessageLayer;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Pet.Progression;

/// <summary>
/// Gives pets XP and tells the owner's client about it.
/// </summary>
internal static class PetXpAward {

    private const byte SHOW_LEVEL_UP_WINDOW = 1;

    /// <summary>
    /// Adds XP to a pet, saves the pet, sends the client its new state and grants the loot of the levels reached.
    /// </summary>
    /// <param name="session">The owner's session, which receives the messages.</param>
    /// <param name="playerObj">The owner's game object, which the level loot requirements are checked against.</param>
    /// <param name="wizard">The owner.</param>
    /// <param name="pet">The pet item.</param>
    /// <param name="xp">The XP to add, before any bonus for the source.</param>
    /// <param name="source">Where the XP comes from.</param>
    /// <returns>What the pet received, which is <see cref="PetXpResult.None"/> if it cannot gain XP.</returns>
    internal static PetXpResult Give(IActorRef session, CoreObject playerObj, Wizard wizard, WizClientObjectItem pet, uint xp, PetXpSource source) {
        var result = PetProgression.AddXp(pet, xp, source);
        if (!result.Applied) {
            return result;
        }

        wizard.SavePet(pet);
        SendProgress(session, playerObj, wizard, pet, result);

        return result;
    }

    /// <summary>
    /// Applies a set of stat changes to a pet, then gives it the XP those points are worth plus any extra XP:
    /// the stats are saved and sent even if the pet cannot gain XP.
    /// </summary>
    /// <param name="session">The owner's session, which receives the messages.</param>
    /// <param name="playerObj">The owner's game object, which the level loot requirements are checked against.</param>
    /// <param name="wizard">The owner.</param>
    /// <param name="pet">The pet item.</param>
    /// <param name="mods">The stat changes. The set itself is left as it is.</param>
    /// <param name="source">Where the points come from, which decides the XP bonus.</param>
    /// <param name="extraXp">XP on top of the points, such as a game score's, before any bonus for the source.</param>
    /// <param name="createSummary">Builds the message the client gets before the pet's new state, such as a game's
    /// end screen. May be null.</param>
    /// <returns>The stat result, whose Mods copy is what a game result sends, and the XP result.</returns>
    internal static (PetStatModResult Stats, PetXpResult Xp) GiveStatMods(IActorRef session,
                                                                          CoreObject playerObj,
                                                                          Wizard wizard,
                                                                          WizClientObjectItem pet,
                                                                          PetStatModificationSet mods,
                                                                          PetXpSource source,
                                                                          uint extraXp = 0,
                                                                          Func<PetStatModResult, PetXpResult, IMessage> createSummary = null) {
        var stats = PetProgression.ApplyStatMods(pet, mods);
        var xp = PetProgression.AddXp(pet, (uint) Math.Min((ulong) stats.Xp + extraXp, uint.MaxValue), source);
        if (stats.Applied > 0 || xp.Applied) {
            wizard.SavePet(pet);
        }

        if (createSummary is not null) {
            session.Tell(createSummary(stats, xp), null);
        }

        if (xp.Applied) {
            SendProgress(session, playerObj, wizard, pet, xp);
        } else if (stats.Applied > 0 && PetBehaviorUpdate.TryCreate(pet, out var update)) {
            session.Tell(update, null);
        }

        return (stats, xp);
    }

    /// <summary>
    /// Creates the messages that follow a grant of XP, in the order the client expects.
    /// </summary>
    /// <param name="pet">The pet item, already holding the new state.</param>
    /// <param name="result">What the grant did.</param>
    /// <returns>The messages to send.</returns>
    internal static List<IMessage> CreateMessages(WizClientObjectItem pet, PetXpResult result) {
        List<IMessage> messages = [];
        if (CoreObjectFactory.FindBehaviorInstance<ClientPetItemBehavior>(pet, out var behavior)) {
            messages.AddRange(result.LevelUps.Select(levelUp => new PET_9_PROTOCOL.MSG_PETLEVELUP {
                GlobalID = pet.m_globalID,
                OverallRating = (byte) behavior.m_overallRating,
                ActiveRating = levelUp.ActiveRating,
                PetLevel = levelUp.Level,
                NewTalent = levelUp.NewTalent,
                NewDerbyPower = levelUp.NewDerbyPower,
                NewJewel = 0,
                Display = SHOW_LEVEL_UP_WINDOW
            }));
        }

        if (PetBehaviorUpdate.TryCreate(pet, out var update)) {
            messages.Add(update);
        }

        messages.Add(new WIZARD2_53_PROTOCOL.MSG_GAINPETXP { PetGID = pet.m_globalID, XP = result.Granted });

        return messages;
    }

    private static void SendProgress(IActorRef session, CoreObject playerObj, Wizard wizard, WizClientObjectItem pet, PetXpResult result) {
        foreach (var message in CreateMessages(pet, result)) {
            session.Tell(message, null);
        }

        foreach (var levelUp in result.LevelUps) {
            PetLevelLoot.Grant(session, playerObj, wizard, pet, levelUp.Level);
        }
    }

}
