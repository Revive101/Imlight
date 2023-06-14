using System.IO;
using System.Threading.Tasks;
using Imlight.Server.Patch;
using Imlight.Server.Shared.Networking;

namespace Imlight.Server.Shared.Packets
{
    public sealed class PATCH_105_PROTCOL : IServerProtocol
    {
        public byte ServiceID { get; } = 105;
        public string ProtocolType { get; } = "PATCH";
        public int ProtocolVersion { get; } = 1;
        public string ProtocolDescription { get; } = "Internal Patch Server Messages.";

        public class MSG_LATEST_CACHE_PROPERTIES : IServerMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 105;

            public string Name;
            public string URL;
            public string URLPrefix;
            public string URLSuffix;
            public uint Version;
            public uint CRC;
            public uint Size;
        }

        public class MSG_DOWNLOAD_WAD_REQUEST : IServerMessage
        {
            public byte MessageOrder { get; } = 2;
            public byte ServiceID { get; } = 105;

            public string WadName;
        }

        public class MSG_DOWNLOAD_FILE_RESULT : IServerMessage
        {
            public byte MessageOrder { get; } = 3;
            public byte ServiceID { get; } = 105;

            public MemoryStream FileStream;
        }
        
        public class MSG_LATESTFILELIST : IServerMessage
        {
            public byte MessageOrder { get; } = 4;
            public byte ServiceID { get; } = 105;

            public LatestFileList LatestFileList;
        } 
    }
}
