/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Security.Cryptography;
using System.Text;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.WizardData;

public static class DatabaseUtilities {

    /// <summary>
    /// Creates a new account in the embedded database.
    /// </summary>
    /// <param name="username">The username of the account.</param>
    /// <param name="email">The email address of the account.</param>
    /// <param name="plaintextPassword">The plain text password of this account.</param>
    /// <param name="auth">The authentication level of the account. Default is AuthLevel.None.</param>
    /// <returns>The created Account object.</returns>
    public static Account CreateEmbeddedDatabaseAccount(string username,
                                                        string email,
                                                        string plaintextPassword,
                                                        AuthLevel auth = AuthLevel.None) {
        var passwordHash = CreateHashedPassword(plaintextPassword);
        var acc = new Account(username, email, passwordHash) { AuthLevel = auth };

        // Save the account to the database.
        var sucess = AccountCollection.CreateAccount(acc);
        if (!sucess) {
            return null;
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
    
}
