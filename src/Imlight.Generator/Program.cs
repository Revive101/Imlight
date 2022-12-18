using System;

namespace Imlight.Generator
{
    internal class Program
    {

        static void Main(string[] args)
        {
            NetworkMessagesGeneratorOptions options = new NetworkMessagesGeneratorOptions()
            {
                Verbose = false,
                ClearEmptyLines = true,
            };
            NetworkMessagesGenerator.Generate(options);
        }

    }
}
