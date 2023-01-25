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
                InputName = "test.xml"
            };
            var gen = new PropertyClassGenerator(options);
            gen.Generate();
        }
    }
}
