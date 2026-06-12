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
using Imlight.CoreLib.Shared.Resources;
using Imlight.Common;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.MessageLayer.Generated;
using Imcodec.Types;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResPlaySoundHandler : BaseResultHandler<ResPlaySound> {

    private string _soundName;
    private uint _templateId;

    public override bool Execute(IResultContext context) {
        if (_soundName == null) {
            _soundName = Result.m_soundName;
            _templateId = CoreObjectFactory.GetCoreTemplateID(x => x.m_filename.ToString().Contains(_soundName));

            if (_templateId == 0) {
                Logger.Error("Sound {0} tried to gather data, but a template ID was not found.",
                    Logger.Args(_soundName));
                    
                return false;
            }
        }

        if (_templateId == 0) {
            return false;
        }

        var msg = new GAME_5_PROTOCOL.MSG_PLAYSOUND { SoundID = new GID(_templateId) };
        context.GetPlayerRef().Tell(msg);

        return true;
    }

}