using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.IO
{
    public static class Util
    {
        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        public static IEnumerable<T> GetAttributesFromType<T>(object type) where T : Attribute
        {
            return type.GetType().GetProperties()
                .Where(f => f.IsDefined(typeof(T), false))
                .Cast<T>();
        }
    }
}
