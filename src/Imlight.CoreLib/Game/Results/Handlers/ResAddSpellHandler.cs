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

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Packets;
using System;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResAddSpellHandler : BaseResultHandler<ResAddSpell> {
    private readonly ObjectSerializer _serializer = new(Versionable: false);
    private readonly PropertyFlags _combatParticipantHandFlags = (PropertyFlags) 5;
    private const float QUERY_WIZARD_TIMEOUT_SECONDS = 5.0f;
    private uint _templateID;
    public override bool Execute(IResultContext context) {
        _templateID = Result.m_templateID;
        var spell = SpellFactory.GetSpell(_templateID);

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
        wizard.AddTemporarySpell(spell);
        var spells = wizard.SpellbookBehavior.TemporarySpells;
        Hand hand = new Hand {
            m_spellList = spells
        };
        _serializer.Serialize(hand, _combatParticipantHandFlags, out var buffer);
        context.GetPlayerRef().Tell(new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATHAND {
            ParticipantID = context.GetPlayerObj().m_globalID,
            HandData = buffer
        });
        return true;
    }

}