using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Common.Logger
{
    internal class LogUnsupportedInternalException : Exception
    {

        public LogUnsupportedInternalException(string message) : base(message) { }

    }
}
