/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResActorDialogHandler : BaseResultHandler<ResActorDialog> {

    public override bool Execute(IResultContext context) {
        var dialog = Result.m_dialog;

        // Serialize the actor dialog into network format.
        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(dialog, 16, out var serializedData)) {
            Logger.Error("Failed to serialize dialog.");
                
            return false;
        }

        var actorDialogMsg = new WIZARD_12_PROTOCOL.MSG_ACTORDIALOG {
            ActorDialog = serializedData
        };

        context.GetPlayerRef().Tell(actorDialogMsg, Self);

        return true;
    }

}