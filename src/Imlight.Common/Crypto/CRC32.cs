namespace Imlight.Common.Crypto
{
    public static class CRC32
    {
        private static readonly uint[] _table;

        static CRC32()
        {
            const uint poly = 0x4C11DB7u;
            _table = new uint[256];

            for (uint i = 0; i < 256; i++)
            {
                var crc = i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc >> 1) ^ ((crc & 1) * poly);
                }
                _table[i] = crc;
            }
        }

        public static uint Compute(byte[] bytes)
        {
            var crc = 0xFFFFFFFFu;
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                byte index = (byte)(crc ^ bytes[i]);
                crc = (crc >> 8) ^ _table[index];
            }
            return ~crc;
        }
    }
}