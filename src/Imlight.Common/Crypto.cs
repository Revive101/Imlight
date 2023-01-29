using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Imlight.Common
{
    public static class Crypto
    {
        public static string PassKey3(string CK2, ushort SessionID, uint Milliseconds)
        {
            var bCK2 = Encoding.UTF8.GetBytes(CK2);
            var sha512 = new SHA512Managed();
            var state = sha512.ComputeHash(bCK2);

            // Salt
            var unixTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            var salt = Encoding.UTF8.GetBytes((SessionID + unixTime + Milliseconds).ToString());
            state = sha512.ComputeHash(state.Concat(salt).ToArray());

            return Convert.ToBase64String(state);
        }
    }
}
