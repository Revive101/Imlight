/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Imlight.Server.Database;
using Imlight.Server.Login.Models;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;

namespace Imlight.Server.Data
{
    public static class Util
    {
        /// <summary>
        /// Creates and returns a debug account.
        /// </summary>
        /// <returns></returns>
        public static Account GetFakeAccount()
        {
            // Create a new debug account.
            var userName = Faker.Internet.UserName();
            var email = Faker.Internet.Email();
            var password = Faker.Identification.SocialSecurityNumber();
            var fakeAcc = new Account(userName, email, password)
            {
                AuthLevel = AuthLevel.Administrator
            };

            // Create characters.
            var serializer = new ObjectSerializer();

            // Destiny
            var destinyRawData = "4C8F6E110100000000007200000000000" +
                "00000000000000000000052078DD072100" +
                "000CA186380310000000088BEC104000B0" +
                "0000000610001000300000000000000000" +
                "00000000000000000840CAB0400002300";
            var destinyRawBytes = StringToByteArray(destinyRawData);
            var destinyCreationData = (TypeCache.WizardCharacterCreationInfo)serializer.Deserialize(destinyRawBytes);
            var destiny = new Character(destinyCreationData, new GID(fakeAcc.ID));
            fakeAcc.AddCharacter(destiny);
            
            // Kevin
            var kevinRawData =
                "4C8F6E1101000000000072000000" +
                "0000000000000000000000000052" +
                "078DD072200000B5882240110100" +
                "000088BEC1040000000000001B0A" +
                "C079000000000000000000000000" +
                "000000000000B336F80400005D00";
            var kevinRawBytes = StringToByteArray(kevinRawData);
            var kevinCreationData = (TypeCache.WizardCharacterCreationInfo)serializer.Deserialize(kevinRawBytes);
            var kevin = new Character(kevinCreationData, new GID(fakeAcc.ID));
            fakeAcc.AddCharacter(kevin);

            return fakeAcc;
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
