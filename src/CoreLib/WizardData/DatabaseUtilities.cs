/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Security.Cryptography;
using System.Text;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.Common.Utilities;
using Imlight.Common;
using Imlight.Common.ObjectProperty;
using Imlight.Common.Caches;
using Imlight.CoreLib.Login.Models;

namespace Imlight.CoreLib.WizardData;

public static class DatabaseUtilities {
    private const string DestinyRawData = "4C8F6E110100000000007200000000000" +
                                          "00000000000000000000052078DD072100" +
                                          "000CA186380310000000088BEC104000B0" +
                                          "0000000610001000300000000000000000" +
                                          "00000000000000000840CAB0400002300";
    private const string KevinRawData = "4C8F6E1101000000000072000000" +
                                        "0000000000000000000000000052" +
                                        "078DD072200000B5882240110100" +
                                        "000088BEC1040000000000001B0A" +
                                        "C079000000000000000000000000" +
                                        "000000000000B336F80400005D00";

    /// <summary>
    /// Creates and returns a fake account with random details.
    /// </summary>
    /// <returns></returns>
    public static Account CreateFakeDatabaseAccount() {
        // Create a new debug account.
        var userName = Faker.Internet.UserName();
        var email = Faker.Internet.Email();
        var password = Faker.Identification.SocialSecurityNumber();
        var fakeAcc = new Account(userName, email, password);

        // Destiny
        var destiny = GetCharacterFromRawCreationData(DestinyRawData);
        fakeAcc.AddCharacter(destiny);

        // I fucking hate Kevin.
        var kevin = GetCharacterFromRawCreationData(KevinRawData);
        fakeAcc.AddCharacter(kevin);

        // Add the fake account to the database.
        AccountCollection.CreateAccount(fakeAcc);
        return fakeAcc;
    }

    /// <summary>
    /// Creates an account from given details. Persists immediately.
    /// </summary>
    /// <param name="username"></param>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <param name="auth"></param>
    /// <returns></returns>
    public static Account CreateEmbeddedDatabaseAccount(
        string username,
        string email,
        string password,
        AuthLevel auth = AuthLevel.None) {
        var acc = new Account(username, email, password) { AuthLevel = auth };

        // Destiny
        //var destiny = GetCharacterFromRawCreationData(DestinyRawData);
        //acc.AddCharacter(destiny);
        //
        //// I fucking hate Kevin.
        //var kevin = GetCharacterFromRawCreationData(KevinRawData);
        //acc.AddCharacter(kevin);

        // Save the account to the database.
        var created = AccountCollection.CreateAccount(acc);
        if (!created) {
            Logger.Debug("A dud account by username {0} already exists in the embedded dragon database. Skipping..",
                Logger.Args(username));
        }

        return acc;
    }

    /// <summary>
    /// Hashes a plaintext password with <see cref="SHA512"/>.
    /// </summary>
    /// <param name="plaintextPassword"></param>
    /// <returns></returns>
    public static string CreateHashedPassword(string plaintextPassword) {
        using var sha512 = SHA512.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(plaintextPassword);

        return Convert.ToBase64String(sha512.ComputeHash(passwordBytes));
    }

    private static Character GetCharacterFromRawCreationData(string rawData) {
        var serializer = new ObjectSerializer()
            .OnMode(SerializerOptions.Mode.Compact)
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask(SerializerOptions.PropertyFlags.Transmit | SerializerOptions.PropertyFlags.AuthorityTransmit);

        var destinyRawBytes = DataManipulation.StringToByteArray(rawData);
        var destinyCreationData = (TypeCache.WizardCharacterCreationInfo) serializer.Deserialize(destinyRawBytes);
        var destiny = new Character(destinyCreationData);

        return destiny;
    }
}
