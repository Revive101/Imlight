using System;
using Imlight.Generator.Network;
using Imlight.Generator.ObjectProperty;

namespace Imlight.Generator
{
    internal class Program
    {
        private static void Main()
        {
            var options = new PropertyClassGeneratorOptions()
            {
                InputName = "PropertyClassDump.xml"
            };
            var gen = new NetworkMessagesGenerator(options);
            gen.Generate();
        }
    }
}
