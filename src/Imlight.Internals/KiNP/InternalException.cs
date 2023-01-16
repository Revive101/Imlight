using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Internals
{
    public class InternalException : Exception
    {
        public InternalException()
        {
        }

        public InternalException(string message) : base(message)
        {
        }

        public InternalException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InternalException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
