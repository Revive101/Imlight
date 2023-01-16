using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Engine.DML
{
    [System.Serializable]
    public class DMLException : Exception
    {

        public DMLException()
        { }

        public DMLException(string message)
            : base(message)
        { }

        public DMLException(string message, Exception innerException)
            : base(message, innerException)
        { }

    }
}
