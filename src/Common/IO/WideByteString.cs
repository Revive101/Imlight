/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Diagnostics;
using System.Text;

namespace Imlight.Common.IO;

[DebuggerDisplay("{ToString()}")]
public readonly struct WideByteString {
    private readonly byte[] _bytes;

    public WideByteString(byte[] bytes) {
        _bytes = bytes;
    }

    public WideByteString(string str) {
        _bytes = Encoding.Unicode.GetBytes(str);
    }

    public static implicit operator string(WideByteString byteString) {
        return byteString._bytes is null
            ? string.Empty
            : Encoding.Unicode.GetString(byteString._bytes);
    }

    public static implicit operator WideByteString(string str) {
        if (str is null) {
            return new WideByteString();
        }

        return new WideByteString(Encoding.Unicode.GetBytes(str));
    }

    public static implicit operator byte[](WideByteString byteString) {
        return byteString._bytes;
    }

    public static implicit operator WideByteString(byte[] buffer) {
        return new WideByteString(buffer);
    }

    public override string? ToString() {
        return _bytes is null ? null : Encoding.Unicode.GetString(_bytes);
    }

    public int Length => _bytes?.Length ?? 0;
}
