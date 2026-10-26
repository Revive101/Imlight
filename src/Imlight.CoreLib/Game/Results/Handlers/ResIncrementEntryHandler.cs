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
 * RESULT HANDLER: INCREMENT ENTRY
 * ========================================================================
 *
 * PURPOSE:
 * Adds one to a global or quest registry entry of the triggering player.
 *
 * USAGE EXAMPLE:
 * Found by reflection in ResultDispatcher.RegisterResultHandlers, like every BaseResultHandler.
 *
 * NOTE:
 * Spawn-time enter events run this too, so arriving inside a volume counts.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResIncrementEntryHandler : BaseResultHandler<ResIncrementEntry> {

    private const float QUERY_WIZARD_TIMEOUT_SECONDS = 5.0f;

    public override bool Execute(IResultContext context) {
        if (context.GetPlayerObj() is null || Result is null || string.IsNullOrEmpty(Result.m_entryName)) {
            return false;
        }

        // Context does not ship with a wizard reference, so we need to query for it.
        var wizard = QueryWizard(context);
        if (wizard is null) {
            Logger.Error("ResIncrementEntry handler failed to retrieve character data within {TimeoutSeconds} seconds.",
                Logger.Args(QUERY_WIZARD_TIMEOUT_SECONDS));

            return false;
        }

        if (!Result.m_isQuestRegistry) {
            return wizard.IncrementRegistryValue(Result.m_entryName);
        }

        if (string.IsNullOrEmpty(Result.m_questName)) {
            return false;
        }

        return wizard.IncrementQuestRegistryValue(Result.m_questName, Result.m_entryName);
    }

    private static Wizard QueryWizard(IResultContext context) {
        var queryTimeout = TimeSpan.FromSeconds(QUERY_WIZARD_TIMEOUT_SECONDS);
        var queryResponse = context
            .GetPlayerRef()
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(), queryTimeout).Result;

        return queryResponse?.Wizard;
    }

}
