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
 */

using System;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Results.Handlers;

/// <summary>
/// Restores the wizard's energy (used for pets, gardening, fishing, cantrips, etc.),
/// either a flat amount (<see cref="ResAddEnergy.m_useFlat"/>) or a percent of max (<see cref="ResAddEnergy.m_energyPercent"/>).
/// A bare result (no flat, no percent) restores to full.
/// </summary>
internal sealed class ResAddEnergyHandler : BaseResultHandler<ResAddEnergy> {

    private const float QUERY_WIZARD_TIMEOUT_SECONDS = 5.0f;

    public override bool Execute(IResultContext context) {
        // Context does not ship with a wizard reference, so we need to query for it.
        var queryWizardMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var queryTimeout = TimeSpan.FromSeconds(QUERY_WIZARD_TIMEOUT_SECONDS);
        var queryResponse = context
            .GetPlayerRef()
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryWizardMsg, queryTimeout).Result;
        if (queryResponse?.Wizard is not Wizard wizard) {
            Logger.Error("Handler failed to retrieve character data within {0} seconds.",
                Logger.Args(QUERY_WIZARD_TIMEOUT_SECONDS));

            return false;
        }

        var maxEnergy = wizard.GameStats.m_energyMax;
        var clientMax = wizard.GameStats.GetClientTypeAlternative().m_energyMax;
        var addedEnergy = Result.m_useFlat
            ? Result.m_energyFlat
            : Result.m_energyPercent > 0
                ? (int) (Result.m_energyPercent * maxEnergy)
                : maxEnergy;

        var energy = wizard.PetOwnerBehavior.Energy + addedEnergy;
        energy = Result.m_overfill != 0
            ? Math.Max(0, energy)
            : Math.Clamp(energy, 0, maxEnergy);

        wizard.UpdateEnergy(energy);
        context.GetPlayerRef().Tell(new PET_9_PROTOCOL.MSG_PETENERGYTICK {
            GlobalID = wizard.GameObjectID,
            Energy = energy,
            MaxEnergy = clientMax,
            TickTime = (int) wizard.PetOwnerBehavior.LastEnergyTickEpoch
        });

        return true;
    }

}
