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
 *
 * ========================================================================
 * CRC32 
 * ========================================================================
 * 
 * PURPOSE:
 * This class implements the CRC32 checksum algorithm, which is used to
 * generate a 32-bit hash value from a sequence of bytes. It is commonly used
 * in data integrity checks and error detection.
 * 
 * USAGE EXAMPLE:
 * var crc = new Crc32();
 * crc.Update(data);
 * uint hash = crc.Hash;
 * 
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 04/27/2025
*/

namespace Imlight.CoreLib.Shared.Cryptography;

public sealed class Crc32 {

    private const uint DefaultInitialState = 0;

    public uint Hash { get; private set; }

    public Crc32()
        => Hash = DefaultInitialState;

    public Crc32(uint initial)
        => Hash = initial;

    public void Reset()
        => Hash = DefaultInitialState;

    public static uint Calculate(uint initialCrc, byte[] data) {
        uint crc = initialCrc;
        uint[] crcTable = GenerateCRC32Table();

        for (int i = 0; i < data.Length; i++) {
            byte b = data[i];
            crc = (crc >> 8) ^ crcTable[(crc ^ b) & 0xFF];
        }

        return crc;
    }

    private static uint[] GenerateCRC32Table() {
        var table = new uint[256];
        var polynomial = 0xEDB88320;

        for (uint i = 0; i < 256; i++) {
            uint c = i;
            for (int j = 0; j < 8; j++) {
                if ((c & 1) == 1) {
                    c = polynomial ^ (c >> 1);
                }
                else {
                    c >>= 1;
                }
            }
            table[i] = c;
        }

        return table;
    }

}
