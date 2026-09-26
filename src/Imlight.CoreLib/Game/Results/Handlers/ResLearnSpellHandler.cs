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
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResLearnSpellHandler : BaseResultHandler<ResLearnSpell> {

    private const float QUERY_WIZARD_TIMEOUT_SECONDS = 5.0f;

    public override bool Execute(IResultContext context) {
        // Context does not ship with a wizard reference, so we need to query for it.
        var queryWizardMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var queryTimeout = TimeSpan.FromSeconds(QUERY_WIZARD_TIMEOUT_SECONDS);
        var queryResponse = context
            .GetPlayerRef()
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryWizardMsg, queryTimeout).Result;
        if (queryResponse == null) {
            Logger.Error("ResLearnSpell handler failed to retrieve character data within {0} seconds.",
                Logger.Args(QUERY_WIZARD_TIMEOUT_SECONDS));

            return false;
        }

        var wizard = queryResponse.Wizard;

        var spell = SpellFactory.GetSpell(Result.m_templateID);
        if (spell is null) {
            Logger.Error("ResLearnSpell handler could not resolve a spell for template ID {0}.",
                Logger.Args(Result.m_templateID));

            return false;
        }

        if (!wizard.LearnSpell(spell)) {
            // Already known; the learn is idempotent.
            return true;
        }

        // The attach payload with the spellbook was already sent, so push the new spell to the client.
        context.GetPlayerRef().Tell(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK {
            SpellID = (int) Result.m_templateID,
        });

        return true;
    }

}
