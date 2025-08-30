/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.Common;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;

namespace Imlight.CoreLib.Game.Zone.Triggers;

internal sealed class ControlMusicHandler<T> : BaseResultHandler<ResControlBackgroundMusic> where T : Result {

    private readonly uint _actionHash;

    // ctor
    public ControlMusicHandler(ZoneTrigger trigger) : base(trigger) {
        var action = trigger.TriggerData.m_results.m_results[0];
        if (action is null) {
            _actionHash = 0;

            return;
        }

        if (action is not ResControlBackgroundMusic resControlBackgroundMusic) {
            Logger.Error("Tried to create a {0}, but the action was not a ResControlBackgroundMusic.",
                Logger.Args(GetType().Name));

            return;
        }

        this._actionHash = StringHash.Compute(resControlBackgroundMusic.m_action);
    }

    public override bool Execute(IActorRef playerRef, CoreObject playerObj) {
        var msg = new WIZARD_12_PROTOCOL.MSG_CONTROLMUSIC {
            FadeTime = 2.0f, // todo: is this always 2.0?
            Action = (int) _actionHash,
        };

        playerRef.Tell(msg);

        return true;
    }

}