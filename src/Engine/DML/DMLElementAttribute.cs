using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Engine.DML
{
    [System.AttributeUsage(System.AttributeTargets.Field |
                           System.AttributeTargets.Property)]
    public class DMLElementAttribute : System.Attribute
    {
        public string SerializedType;

        public DMLElementAttribute(string serializedType)
        {
            SerializedType = serializedType;
        }
    }
}
