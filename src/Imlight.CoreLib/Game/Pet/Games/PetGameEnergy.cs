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
 *
 * ========================================================================
 * PET GAME ENERGY
 * ========================================================================
 *
 * PURPOSE:
 * Checks and charges the energy a pet game costs, and refunds it when a game is aborted.
 *
 * USAGE EXAMPLE:
 * var result = PetGameEnergy.TryCharge(wizard, gameName, petLevel, out var cost);
 * if (result == PetGameEnergyResult.NotEnoughEnergy) { ... }
 * SendToSocket(PetGameEnergy.BuildTick(wizard));
 *
 * NOTE:
 * The cost comes from PetGameConfig by the pet's level. The refund percent is PetRules.GameAbortRefundPercent.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Pet.Games;

internal enum PetGameEnergyResult {

    Charged,
    UnknownGame,
    NotEnoughEnergy

}

/// <summary>
/// The energy side of pet games.
/// </summary>
internal static class PetGameEnergy {

    private const int PERCENT_DIVISOR = 100;

    /// <summary>
    /// Checks whether the wizard can pay for a game, without charging anything.
    /// </summary>
    /// <param name="wizard">The owner.</param>
    /// <param name="gameName">The game name as it appears in PetGames.xml.</param>
    /// <param name="petLevel">The pet's level.</param>
    /// <returns>Charged if the wizard can pay, otherwise why not.</returns>
    internal static PetGameEnergyResult CheckCost(Wizard wizard, string gameName, int petLevel) {
        if (!PetGameConfig.TryGetEnergyCost(gameName, petLevel, out var cost)) {
            return PetGameEnergyResult.UnknownGame;
        }

        return wizard.PetOwnerBehavior.Energy < cost ? PetGameEnergyResult.NotEnoughEnergy : PetGameEnergyResult.Charged;
    }

    /// <summary>
    /// Charges the cost of a game to the wizard. Nothing is charged unless the result is Charged.
    /// </summary>
    /// <param name="wizard">The owner.</param>
    /// <param name="gameName">The game name as it appears in PetGames.xml.</param>
    /// <param name="petLevel">The pet's level.</param>
    /// <param name="cost">The cost looked up, 0 if the game is unknown.</param>
    /// <returns>Whether the cost was charged, the game is unknown, or the wizard has too little energy.</returns>
    internal static PetGameEnergyResult TryCharge(Wizard wizard, string gameName, int petLevel, out int cost) {
        if (!PetGameConfig.TryGetEnergyCost(gameName, petLevel, out cost)) {
            return PetGameEnergyResult.UnknownGame;
        }

        var energy = wizard.PetOwnerBehavior.Energy;
        if (energy < cost) {
            return PetGameEnergyResult.NotEnoughEnergy;
        }

        wizard.UpdateEnergy(energy - cost);

        return PetGameEnergyResult.Charged;
    }

    /// <summary>
    /// Gives back the configured share of a charged cost, never above the wizard's maximum energy.
    /// </summary>
    /// <param name="wizard">The owner.</param>
    /// <param name="cost">The cost that was charged.</param>
    /// <returns>The energy given back.</returns>
    internal static int Refund(Wizard wizard, int cost) {
        var refund = cost * PetRules.GameAbortRefundPercent / PERCENT_DIVISOR;
        var energy = wizard.PetOwnerBehavior.Energy;
        var room = Math.Max(0, GetMaxEnergy(wizard) - energy);
        refund = Math.Min(refund, room);
        if (refund <= 0) {
            return 0;
        }

        wizard.UpdateEnergy(energy + refund);

        return refund;
    }

    /// <summary>
    /// Builds the energy message for the client to show the wizard's current energy.
    /// </summary>
    /// <param name="wizard">The owner.</param>
    /// <returns>The message, ready to send to the wizard's socket.</returns>
    internal static PET_9_PROTOCOL.MSG_PETENERGYTICK BuildTick(Wizard wizard)
        => new() {
            GlobalID = wizard.GameObjectID,
            Energy = wizard.PetOwnerBehavior.Energy,
            MaxEnergy = GetMaxEnergy(wizard),
            TickTime = (int) wizard.PetOwnerBehavior.LastEnergyTickEpoch
        };

    private static int GetMaxEnergy(Wizard wizard)
        => MagicLevelsConfig.GetPlayerLevelInfo(wizard.MagicSchoolBehavior.MagicSchool, wizard.MagicSchoolBehavior.Level).m_petEnergy;

}
