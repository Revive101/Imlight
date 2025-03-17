/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Zone.Core;
using static Imlight.Common.Caches.TypeCache;
using static Imlight.Common.Caches.ServerTypeCache;
using Imlight.CoreLib.Shared.Resources;
using Imlight.Common;
using Imlight.Common.ObjectProperty.PropertyReflection;

namespace Imlight.CoreLib.Game.Zone.Triggers;

internal sealed class PlaySoundHandler<T> : BaseResultHandler<ResPlaySound> where T : Result {

    private readonly string _soundName;
    private readonly uint _templateId;

    // ctor
    public PlaySoundHandler(ZoneTrigger trigger) : base(trigger) { 
        this._soundName = Result.m_soundName;
        this._templateId = CoreObjectFactory.GetCoreTemplateID(x => x.m_filename.ToString().Contains(_soundName));

        if (_templateId == 0) {
            Logger.Error("Sound {0} tried to gather data, but a template ID was not found.",
                Logger.Args(_soundName));
        }
    }

    public override void Execute(IActorRef playerRef, CoreObject playerObj)  {
        if (_templateId == 0) {
            return;
        }

        var msg = new GAME_5_PROTOCOL.MSG_PLAYSOUND { SoundID = new GID(_templateId) };
        playerRef.Tell(msg);
    }

}