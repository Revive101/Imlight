using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Generator
{
    internal class NetworkMessagesGeneratorOptions
    {

        /// <summary>
        /// Choose whether to compile protocols with record metadata such as message access level and description.
        /// </summary>
        internal bool Verbose = false;

        /// <summary>
        /// If true, brackets will open on a newline instead of appending to the end of the previous.
        /// </summary>
        internal bool CurlyBraceNewline = true;

        /// <summary>
        /// How many spaces should the identation be.
        /// </summary>
        internal string IndentString = "    ";

        /// <summary>
        /// If true, empty lines will be removed to compact the generation together.
        /// </summary>
        internal bool ClearEmptyLines = true;

    }
}
