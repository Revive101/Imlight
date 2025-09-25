/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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