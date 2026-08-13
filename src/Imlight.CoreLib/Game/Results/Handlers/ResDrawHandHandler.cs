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
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResDrawHandHandler : BaseResultHandler<ResDrawHand> {

    public override bool Execute(IResultContext context) {
        var tid = (uint) Result.m_templateID;
        if (tid == 0 || CoreObjectFactory.GetCoreTemplate(tid) is not SpellTemplate) {
            // Creature-targeted draws (the retail golem draws) are superseded by the round script.
            return true;
        }

        context.GetPlayerRef().Tell(new TUTORIAL_108_PROTOCOL.MSG_TUTORIALREBUILDDUELHAND {
            SpellIdsToGrant = [tid],
            RecipientTemplateId = 1,
        });

        return true;
    }

}
