using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Internals
{
    [AttributeUsage(AttributeTargets.Field)]
    public class PropertyAttribute : Attribute
    {

        public uint Hash;
        public int Flags;

        public PropertyAttribute(uint hash, int flags)
        {
            this.Hash = hash;
            this.Flags = flags;
        }
    }
}
