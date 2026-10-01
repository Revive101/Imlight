using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace Imlight.CoreLib.Shared.Networking;

/// <summary>Reassembles KiNP frames independently of TCP receive boundaries.</summary>
public sealed class KiPacketStream {
    private byte[] _pending = new byte[4096];
    private int _count;

    public IReadOnlyList<byte[]> Append(ReadOnlySpan<byte> bytes) {
        var frames = new List<byte[]>();
        // Consume incrementally to bound retained data even when many frames arrive together.
        while (!bytes.IsEmpty) {
            var required = _count < 4 ? 4 : FrameLength();
            var take = Math.Min(required - _count, bytes.Length);
            if (_pending.Length < required) Array.Resize(ref _pending, required);
            bytes[..take].CopyTo(_pending.AsSpan(_count));
            _count += take;
            bytes = bytes[take..];
            if (_count < 4) continue;
            var length = FrameLength();
            if (_count == length) {
                frames.Add(_pending.AsSpan(0, length).ToArray());
                _count = 0;
            }
        }
        return frames;
    }

    private int FrameLength() {
        if (BinaryPrimitives.ReadUInt16LittleEndian(_pending) != 0xF00D)
            throw new InvalidDataException("Invalid KiNP frame signature.");
        var length = BinaryPrimitives.ReadUInt16LittleEndian(_pending.AsSpan(2));
        // The current message codec supports only the short KiNP format. Never
        // feed an extended header into that decoder as if it were a short frame.
        if (length >= 0x8000 || length < 5)
            throw new InvalidDataException("Unsupported or invalid KiNP frame length.");
        return length + 4;
    }
}
