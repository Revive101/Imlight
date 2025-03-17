/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Patch.Services;

public class PatchService : MessageService
{
    public PatchService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new PatchService(parentActor));

    [MessageHandler(typeof(PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2))]
    private void ReceiveLatestFileListV2(PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2 message) {
        // Get the cached information stored on the patch server.
        var msg = new PATCH_105_PROTOCOL.MSG_LATEST_CACHE_PROPERTIES();
        var rsp = AskServer<PATCH_105_PROTOCOL.MSG_LATEST_CACHE_PROPERTIES>(msg);

        // Craft the appropriate response and send it back to the socket.
        var socketRsp = new PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2() {
            LatestVersion = rsp.Version,
            ListFileName = rsp.Name,
            ListFileSize = rsp.Size,
            ListFileCRC = rsp.CRC,
            ListFileURL = rsp.URL,
            ListFileType = 1, // This causes the client to fail parsing the file if it is not 1. Do not change !
            URLPrefix = rsp.URLPrefix,
            URLSuffix = rsp.URLSuffix,
        };

        SendToSocket(socketRsp);
    }
}
