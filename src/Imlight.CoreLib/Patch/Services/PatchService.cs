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
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Patch.Services;

internal class PatchService(SessionActor sessionActor) : MessageService(sessionActor) {

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
            ListFileTime = rsp.FileTime,
            URLPrefix = rsp.URLPrefix,
            URLSuffix = rsp.URLSuffix,
            Locale = message.Locale,
        };

        SendToSocket(socketRsp);
    }
    
}
