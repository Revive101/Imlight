using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Common
{
    public static class RandomGen
    {

        private const string RANDOM_STRING_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private static readonly Random s_random = new Random();

        /// <summary>
        /// Caches the min and max value for generic numerical values.
        /// </summary>
        private static readonly Dictionary<string, Tuple<object, object>> valueClampCache = new();

        /// <summary>
        /// Generates a random 32-bit numerical value.
        /// </summary>
        /// <typeparam name="T">The numerical data type to contrain to.</typeparam>
        /// <returns>A random 32-bit numerical value.</returns>
        public static T SignedNumber<T>() where T : struct, IComparable<T>
        {
            if (!IsSigned<T>()) throw new NotImplementedException("Unsigned numericals are not supported by this method!");

            // All values are used with 32-bit integer values.
            // Before returning, we cast them into their respective type.

            // Find minimum and maximum constraints for numerical data type
            Int32 minVal = Convert.ToInt32(MinValue<T>());
            Int32 maxVal = Convert.ToInt32(MaxValue<T>());

            // Instantiate a random number between the min and max values.
            // Use absolute value to convert to a positive value.
            var r = new Random();
            Int32 num = Math.Abs(r.Next(minVal, maxVal));

            return (T)Convert.ChangeType(num, typeof(T));
        }

        /// <summary>
        /// Generates a random string. This is unsuitable for anything security related.
        /// </summary>
        /// <param name="stringLength">The length of the string generated.</param>
        /// <returns>A randomly generated string of length.</returns>
        public static string String(int stringLength)
        {
            if (stringLength <= 0) 
                throw new ArgumentOutOfRangeException($"{nameof(stringLength)} cannot be lower than zero!");

            char[] buffer = new char[stringLength];
            for (int i = 0; i < stringLength; i++)
            {
                buffer[i] = RANDOM_STRING_CHARS[s_random.Next(RANDOM_STRING_CHARS.Length)];
            }

            return new string(buffer);
        }

        private static T MinValue<T>() where T : struct, IComparable<T>
        {
            // Check to see if we've cached this value already.
            var typestr = typeof(T).ToString();
            if (valueClampCache.TryGetValue(typestr, out var result))
            {
                return (T)Convert.ChangeType(result.Item1, typeof(T));
            }
            // If the cache doesn't contain the value, get it manually.
            else
            {
                try
                {
                    var min = (T)typeof(T).GetField("MinValue").GetValue("null");

                    // Cache the result.
                    var max = (T)typeof(T).GetField("MaxValue").GetValue("null");
                    var cacheMin = Convert.ChangeType(min, TypeCode.Int64);
                    var cacheMax = Convert.ChangeType(max, TypeCode.UInt64);
                    valueClampCache.Add(typestr, Tuple.Create(cacheMin, cacheMax));

                    // Return.
                    return min;
                }
                catch
                {
                    throw new InvalidOperationException($"Unsupported type {typeof(T)}");
                }
            }
        }

        private static T MaxValue<T>() where T : struct, IComparable<T>
        {
            // Check to see if we've cached this value already.
            var typestr = typeof(T).ToString();
            if (valueClampCache.TryGetValue(typestr, out var result))
            {
                return (T)Convert.ChangeType(result.Item2, typeof(T));
            }
            // If the cache doesn't contain the value, get it manually.
            else
            {
                try
                {
                    var max = (T)typeof(T).GetField("MaxValue").GetValue("null");

                    // Cache the result.
                    var min = (T)typeof(T).GetField("MinValue").GetValue("null");
                    var cacheMin = Convert.ChangeType(min, TypeCode.Int64);
                    var cacheMax = Convert.ChangeType(max, TypeCode.UInt64);
                    valueClampCache.Add(typestr, Tuple.Create(cacheMin, cacheMax));

                    // Return.
                    return max;
                }
                catch
                {
                    throw new InvalidOperationException($"Unsupported type {typeof(T)}");
                }
            }
        }

        private static bool IsSigned<T>()
        {
            return Convert.ToBoolean(typeof(T).GetField("MinValue").GetValue(null));
        }

        public static class Unused
        {
            /// <summary>
            /// Generates an unused random 32-bit numerical value.
            /// </summary>
            /// <typeparam name="T">The numerical data type.</typeparam>
            /// <param name="ids">The list of currently unavailable numbers.</param>
            /// <returns>A random 32-bit numerical value not included in the ID list.</returns>
            public static T SignedNumber<T>(IEnumerable<T> ids) where T : struct, IComparable<T>
            {
                if (!IsSigned<T>()) throw new NotImplementedException("Unsigned numericals are not supported by this method!");

                // All values are used with 32-bit integer values.
                // Before returning, we cast them into their respective type.

                // Find minimum and maximum constraints for numerical data type
                Int32 minVal = Convert.ToInt32(MinValue<T>());
                Int32 maxVal = Convert.ToInt32(MaxValue<T>());

                var r = new Random();
                bool foundNum = false;
                do
                {
                    Int32 num = r.Next(minVal, maxVal);
                    if (!ids
                        .Any(x => (Int32)Convert.ChangeType(x, TypeCode.Int32) == num)) 
                        return (T)Convert.ChangeType(num, typeof(T));
                    else continue;
                } while (!foundNum);

                return default;
            }
        }

    }
}
