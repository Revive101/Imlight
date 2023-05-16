using Akka.Actor;
using WizUnraveler.Cache;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Patch.Services
{
    public class PatchService : MessageService
    {
        public PatchService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new PatchService(parentActor));
        }
        
        [MessageHandler(typeof(PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2))]
        private void ReceiveLatestFileListV2(PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2 message)
        {
            // Get the cached information stored on the patch server.
            var msg = new PATCH_105_PROTCOL.MSG_LATEST_CACHE_PROPERTIES();
            var rsp = AskServer<PATCH_105_PROTCOL.MSG_LATEST_CACHE_PROPERTIES>(msg);

            // Craft the appropriate response and send it back to the socket.
            var socketRsp = new PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2()
            {
                LatestVersion = rsp.Version,
                ListFileName = rsp.Name,
                ListFileSize = rsp.Size,
                ListFileCRC = rsp.CRC,
                ListFileURL = rsp.URL,
                URLPrefix = rsp.URLPrefix,
                URLSuffix = rsp.URLSuffix,
            };

            SendToSocket(socketRsp);
        }
    }
}
