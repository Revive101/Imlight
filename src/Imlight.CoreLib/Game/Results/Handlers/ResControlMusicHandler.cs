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
using Imlight.Common;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResControlMusicHandler : BaseResultHandler<ResControlBackgroundMusic> {

    private uint _actionHash;

    public override bool Execute(IResultContext context) {
        if (_actionHash == 0) {
            if (Result == null) {
                Logger.Error("Tried to create a {0}, but the result was null.",
                    Logger.Args(GetType().Name));
                    
                return false;
            }

            _actionHash = StringHash.Compute(Result.m_action);
        }

        var msg = new WIZARD_12_PROTOCOL.MSG_CONTROLMUSIC {
            FadeTime = 2.0f,
            Action = (int) _actionHash,
        };

        context.GetPlayerRef().Tell(msg);

        return true;
    }

}