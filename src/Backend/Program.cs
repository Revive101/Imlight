using System;
using Imlight.Realm;
using Imlight.Engine;
using Imlight.Common;

namespace Imlight.Backend
{
    internal class Program
    {

        internal static RealmManager RealmManager { get; private set; }

        static void Main(string[] args)
        {
            // Processor test
            DynamicDeserializer.Init();
            ProcessorManager.StartNewProcessor();

            // Realm test
            RealmManager = new RealmManager();
            RealmManager.CreateRealm("test realm");

            Console.ReadKey();
        }
    }
}
