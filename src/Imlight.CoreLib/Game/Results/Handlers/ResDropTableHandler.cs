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
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResDropTableHandler : BaseResultHandler<ResDropTable> {

    private const float QUERY_WIZARD_TIMEOUT_SECONDS = 5.0f;

    public override bool Execute(IResultContext context) {
        // Context does not ship with a wizard reference, so we need to query for it.
        var queryWizardMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var queryTimeout = TimeSpan.FromSeconds(QUERY_WIZARD_TIMEOUT_SECONDS);
        var queryResponse = context
            .GetPlayerRef()
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryWizardMsg, queryTimeout).Result;
        if (queryResponse == null) {
            Logger.Error("Handler failed to retrieve character data within {0} seconds.",
                Logger.Args(QUERY_WIZARD_TIMEOUT_SECONDS));

            return false;
        }
        var wizard = queryResponse.Wizard;
        if (wizard == null) {
            Logger.Error("Handler retrieved character data reply, but wizard was null after {0} seconds.",
                Logger.Args(QUERY_WIZARD_TIMEOUT_SECONDS));

            return false;
        }

        // The drop table doesn't really mean anything yet. It's just a template.
        // We need to actually "roll" it.
        var rollResults = DropTableRoller.Roll([Result.m_tableName],
                                               context.GetPlayerRef(),
                                               context.GetPlayerObj(),
                                               wizard);

        // Process the results.
        LootGranter.GrantAndDisplay(context.GetPlayerRef(), wizard, rollResults);

        return true;
    }

}