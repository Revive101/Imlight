using System.Collections.Generic;

namespace Imlight.Server.Patch
{
    public record LatestFileList
    {
        public List<LatestFile> Files { get; init; }
    }

    public record LatestFile
    {
        public string SourceFileName { get; init; }
        public string TargetFileName { get; init; }
        public uint FileType { get; init; }
        public uint Size { get; init; }
        public uint HeaderSize { get; init; }
        public uint CompressedHeaderSize { get; init; }
        public uint Crc { get; init; }
        public uint HeaderCrc { get; init; }
    }
}