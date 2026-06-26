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

using System.Collections.Generic;

namespace Imlight.CoreLib.Patch;

public record LatestFileList {
    
    public List<LatestFile> Files { get; init; }
}

public record LatestFile {
    
    public string SourceFileName { get; init; }
    public string TargetFileName { get; init; }
    public uint FileType { get; init; }
    public uint Size { get; init; }
    public uint HeaderSize { get; init; }
    public uint CompressedHeaderSize { get; init; }
    public uint Crc { get; init; }
    public uint HeaderCrc { get; init; }
    
}

/// <summary>
/// Cached properties of the LatestFileList.bin download, used to serve
/// <see cref="PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2"/> responses to clients.
/// </summary>
public record PatchCacheProperties {

    public string Name { get; init; }
    public string Url { get; init; }
    public string UrlPrefix { get; init; }
    public string UrlSuffix { get; init; }
    public uint Version { get; init; }
    public uint Crc { get; init; }
    public uint Size { get; init; }

}
