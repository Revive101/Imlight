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
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResGiveSpellHandler : BaseResultHandler<ResGiveSpell> {

    public override bool Execute(IResultContext context) {
        // m_templateID names the recipient (1 = the player, else a duel creature's template), m_spellID the
        // spell; a lone m_templateID is a spell for the player. Out of a duel the relay is a harmless no-op.
        var recipient = Result.m_spellID != 0 ? (uint) Result.m_templateID : 1;
        var spellId = Result.m_spellID != 0 ? (uint) Result.m_spellID : (uint) Result.m_templateID;
        if (spellId == 0) {
            return true;
        }

        context.GetPlayerRef().Tell(new TUTORIAL_108_PROTOCOL.MSG_TUTORIALREBUILDDUELHAND {
            SpellIdsToGrant = [spellId],
            RecipientTemplateId = recipient == 0 ? 1 : recipient,
        });

        return true;
    }

}
