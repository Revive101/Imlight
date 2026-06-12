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

using Imcodec.IO;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Imlight.CoreLib.Shared.Cryptography;

internal static class Rec1 {

    private const byte TWOFISH_BLOCK_SIZE = 0x10;
    private const byte TWOFISH_KEY_SIZE = 2 * TWOFISH_BLOCK_SIZE;
    private const byte TWOFISH_NONCE_SIZE = TWOFISH_BLOCK_SIZE;

    private const byte KEY_CONSTANT = 0x17;
    private const byte IV_CONSTANT = 0xB6;

    /// <summary>
    /// Encodes the given data using the Twofish cipher in OFB mode with no padding.
    /// </summary>
    /// <param name="data">The data to encode.</param>
    /// <param name="sid">The session ID.</param>
    /// <param name="timeSecs">The number of seconds since the Unix epoch.</param>
    /// <param name="timeMillis">The number of milliseconds within the current second.</param>
    /// <returns>The encoded data.</returns>
    internal static ByteString Encode(ByteString data, ushort sid, uint timeSecs, uint timeMillis) {
        var key = DeriveTwofishKey(sid, timeSecs, timeMillis);
        var nonce = DeriveTwofishNonce();

        var cipher = CipherUtilities.GetCipher("Twofish/OFB/NoPadding");
        cipher.Init(true, new ParametersWithIV(new KeyParameter(key), nonce));

        return cipher.DoFinal(data);
    }

    /// <summary>
    /// Decodes the given encoded data using the specified session ID, time in seconds, and time in milliseconds.
    /// </summary>
    /// <param name="encodedData">The encoded data to decode.</param>
    /// <param name="sid">The session ID to use for key derivation.</param>
    /// <param name="timeSecs">The time in seconds to use for key derivation.</param>
    /// <param name="timeMillis">The time in milliseconds to use for key derivation.</param>
    /// <returns>The decoded data as a <see cref="ByteString"/>.</returns>
    internal static ByteString Decode(byte[] encodedData, ushort sid, uint timeSecs, uint timeMillis) {
        var key = DeriveTwofishKey(sid, timeSecs, timeMillis);
        var nonce = DeriveTwofishNonce();

        var cipher = CipherUtilities.GetCipher("Twofish/OFB/NoPadding");
        cipher.Init(false, new ParametersWithIV(new KeyParameter(key), nonce));
        
        return cipher.DoFinal(encodedData);
    }

    private static byte[] DeriveTwofishKey(ushort sessionID, uint timeSecs, uint timeMillis) {
        var key = new byte[TWOFISH_KEY_SIZE];

        for (var i = 0; i < key.Length; i++) {
            key[i] = (byte) (KEY_CONSTANT + i);
        }

        key[4] = (byte) (sessionID & 0xff);
        key[5] = 0;
        key[6] = (byte) (sessionID >> 8 & 0xff);

        key[8] = (byte) (timeSecs & 0xff);
        key[9] = (byte) (timeSecs >> 16 & 0xff);
        key[12] = (byte) (timeSecs >> 8 & 0xff);
        key[13] = (byte) (timeSecs >> 24 & 0xff);

        key[14] = (byte) (timeMillis & 0xff);
        key[15] = (byte) (timeMillis >> 8 & 0xff);

        return key;
    }

    private static byte[] DeriveTwofishNonce() {
        var iv = new byte[TWOFISH_NONCE_SIZE];

        for (var i = 0; i < iv.Length; i++) {
            iv[i] = (byte) (IV_CONSTANT - i);
        }

        return iv;
    }
    
}
