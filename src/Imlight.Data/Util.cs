using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;

namespace Imlight.Data
{
    public static class Util
    {
        private static Account _debugAccount;

        /// <summary>
        /// Creates and returns a debug account.
        /// </summary>
        /// <returns></returns>
        public static Account GetDebugAccount()
        {
            if (_debugAccount is not null)
                return _debugAccount;

            // Create a new debug account.
            _debugAccount = new Account("Chi", "Chi2Chomp@mail.com", "Password");
            _debugAccount.AuthLevel = AuthLevel.Administrator;

            var rawData = "4C8F6E110100000000007200000000000" +
                "00000000000000000000052078DD072100" +
                "000CA186380310000000088BEC104000B0" +
                "0000000610001000300000000000000000" +
                "00000000000000000840CAB0400002300";
            var rawBytes = StringToByteArray(rawData);
            var serializer = new ObjectSerializer();
            var creationData = (TypeCache.WizardCharacterCreationInfo)serializer.Deserialize(rawBytes);
            var debugCharacter = new Character(creationData, new GID(_debugAccount.ID));
            _debugAccount.AddCharacter(debugCharacter);

            return _debugAccount;
        }

        // Creates a new account with a random username, password, and email.
        public static Account GetEmptyAccount()
        {
            var username = Guid.NewGuid().ToString();
            var password = Guid.NewGuid().ToString();
            var email = Guid.NewGuid().ToString();

            return new Account(username, email, password);
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}
