/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Security.Cryptography;
using System.Text;

namespace Imlight.Common.Cryptography;

public static class SessionKey
{
    public static string GenerateHash(string input, ulong salt)
    {
        byte[] saltBytes = BitConverter.GetBytes(salt);
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);

        byte[] combinedBytes = new byte[saltBytes.Length + inputBytes.Length];
        Buffer.BlockCopy(saltBytes, 0, combinedBytes, 0, saltBytes.Length);
        Buffer.BlockCopy(inputBytes, 0, combinedBytes, saltBytes.Length, inputBytes.Length);

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(combinedBytes);
            return Convert.ToBase64String(hashBytes);
        }
    }
        
    public static bool ValidateHash(string input, ulong salt, string expectedHash)
    {
        string generatedHash = GenerateHash(input, salt);
        return generatedHash.Equals(expectedHash);
    }
}