using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Internals
{
    [System.AttributeUsage(System.AttributeTargets.Field |
                           System.AttributeTargets.Property)]
    public class DMLElementAttribute : System.Attribute
    {
        public DMLType SerializedType;

        public DMLElementAttribute(DMLType serializedType)
        {
            SerializedType = serializedType;
        }
    }
}
