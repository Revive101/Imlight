/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.ObjectProperty.PropertyReflection;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Imlight.Common.Utilities;

public static class RandomGen
{
    public static T GenerateUniqueID<T>(List<T> list)
        where T : struct, IComparable, IConvertible, IEquatable<T>
    {
        // Check that T is a numerical type
        if (!typeof(T).IsPrimitive || typeof(T) == typeof(bool) || typeof(T) == typeof(char) || typeof(T) == typeof(IntPtr) || typeof(T) == typeof(UIntPtr))
        {
            throw new ArgumentException("Type parameter must be a numerical type.");
        }

        // Generate a new unique ID
        T newId;
        do
        {
            dynamic max = default(T);
            foreach (T element in list)
            {
                if (element.CompareTo(max) > 0)
                {
                    max = element;
                }
            }
            newId = max + (dynamic)1;
        } while (list.Contains(newId));

        return newId;
    }

    public static GID GenerateGUID()
    {
        var buffer = Guid.NewGuid().ToByteArray(); // generate a new GUID
        var ulongType = BitConverter.ToUInt64(buffer, 0);

        return new GID(ulongType);
    }

    public static ulong GenerateHash(string input)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            ulong hash = BitConverter.ToUInt64(hashBytes, 0);
            return hash;
        }
    }
}
