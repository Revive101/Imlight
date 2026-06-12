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

using System.IO;
using Imlight.CoreLib.Patch;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Shared.Packets;

public sealed class PATCH_105_PROTOCOL : IServerProtocol {
    
    public byte ServiceID { get; } = 105;
    public string ProtocolType { get; } = "PATCH";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Patch Server Messages.";

    public class MSG_LATEST_CACHE_PROPERTIES : IServerMessage {
        
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

    public class MSG_DOWNLOAD_WAD_REQUEST : IServerMessage {
        
        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 105;

        public string WadName;
        
    }

    public class MSG_DOWNLOAD_FILE_RESULT : IServerMessage {
        
        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 105;

        public MemoryStream FileStream;
        
    }

    public class MSG_LATESTFILELIST : IServerMessage {
        
        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 105;

        public LatestFileList LatestFileList;
        
    }
    
}
