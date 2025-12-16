/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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