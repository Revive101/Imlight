/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Imlight.Server.Game.Models;
using Imlight.Server.Login.Models;
using Imlight.Server.WizardData.Collections;
using SharpDX;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;

namespace Imlight.Server.Shared.Resources
{
    public static class Util
    {
        /// <summary>
        /// Creates and returns a debug account.
        /// </summary>
        /// <returns></returns>
        public static Account CreateFakeDatabaseAccount()
        {
            // Create a new debug account.
            var userName = Faker.Internet.UserName();
            var email = Faker.Internet.Email();
            var password = Faker.Identification.SocialSecurityNumber();
            var fakeAcc = new Account(userName, email, password);

            // Create characters.
            var serializer = new ObjectSerializer();

            // Destiny
            const string destinyRawData = "4C8F6E110100000000007200000000000" +
                                          "00000000000000000000052078DD072100" +
                                          "000CA186380310000000088BEC104000B0" +
                                          "0000000610001000300000000000000000" +
                                          "00000000000000000840CAB0400002300";
            var destinyRawBytes = StringToByteArray(destinyRawData);
            var destinyCreationData = (TypeCache.WizardCharacterCreationInfo)serializer.Deserialize(destinyRawBytes);
            var destiny = new Character(destinyCreationData);
            fakeAcc.AddCharacter(destiny);
            
            // I fucking hate Kevin.
            const string kevinRawData = "4C8F6E1101000000000072000000" +
                                        "0000000000000000000000000052" +
                                        "078DD072200000B5882240110100" +
                                        "000088BEC1040000000000001B0A" +
                                        "C079000000000000000000000000" +
                                        "000000000000B336F80400005D00";
            var kevinRawBytes = StringToByteArray(kevinRawData);
            var kevinCreationData = (TypeCache.WizardCharacterCreationInfo)serializer.Deserialize(kevinRawBytes);
            var kevin = new Character(kevinCreationData);
            fakeAcc.AddCharacter(kevin);
            
            // Add the fake account to the database.
            AccountCollection.CreateAccount(fakeAcc);

            return fakeAcc;
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public static string GetCompactStringFromVector(Vector4 vector)
            => $"{vector.X},{vector.Y},{vector.Z},{vector.W}";
        
        public static string GetCompactStringFromVector(Vector3 vector, Vector3 orientation)
            => $"{vector.X},{vector.Y},{vector.Z},{orientation.Z}";
        
        public static Vector4 GetVectorFromCompactString(string loc)
        {
            if (!loc.Contains(','))
                return Vector4.Zero;
            
            var components = loc.Split(",");
            var x = float.TryParse(components[0], out var xVal) ? xVal : 0;
            var y = float.TryParse(components[1], out var yVal) ? yVal : 0;
            var z = float.TryParse(components[2], out var zVal) ? zVal : 0;
            var d = float.TryParse(components[3], out var dVal) ? dVal : 0;

            return new Vector4(x, y, z, d);
        }
        
        public static bool IsDateTimeNowBetween(DateTime start, DateTime end)
        {
            // Get the current year from DateTime.Now
            var currentYear = DateTime.Now.Year;

            // Create new DateTime instances with the current year but the same day and month
            var startWithCurrentYear = new DateTime(currentYear, start.Month, start.Day);
            var endWithCurrentYear = new DateTime(currentYear, end.Month, end.Day);

            // Check if DateTime.Now is between startWithCurrentYear and endWithCurrentYear
            return DateTime.Now >= startWithCurrentYear && DateTime.Now <= endWithCurrentYear;
        }
    }
}
