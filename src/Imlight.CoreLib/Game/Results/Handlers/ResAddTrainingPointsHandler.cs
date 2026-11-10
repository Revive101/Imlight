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
 * RES ADD TRAINING POINTS HANDLER
 * ========================================================================
 *
 * PURPOSE:
 * Grants a training point when a quest result awards one.
 *
 * USAGE EXAMPLE:
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: WonderlandWorkshop with Claude (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 11/09/2026
 */

using System;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResAddTrainingPointsHandler : BaseResultHandler<ResAddTrainingPoints> {

    // todo: the result's amount is not in Imcodec's type or SpiralDB. Is it ever more than 1?
    private const int TrainingPointsGranted = 1;
    private const float QueryWizardTimeoutSeconds = 5.0f;

    public override bool Execute(IResultContext context) {
        var queryResponse = context
            .GetPlayerRef()
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(
                new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(),
                TimeSpan.FromSeconds(QueryWizardTimeoutSeconds)).Result;
        if (queryResponse?.Wizard is null) {
            Logger.Warning("Result {Result} was skipped: the character could not be read, most likely because the player logged out.",
                Logger.Args(nameof(ResAddTrainingPoints)));

            return false;
        }

        LootGranter.UpdateWizardTP(context.GetPlayerRef(), queryResponse.Wizard, TrainingPointsGranted);

        return true;
    }

}
