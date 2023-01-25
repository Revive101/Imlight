using System;
using Imlight.Realm;
using Imlight.Engine;
using Imlight.Common;
using System.Diagnostics;
using System.Collections.Generic;

namespace Imlight.Backend
{
    internal class Program
    {

        internal static RealmManager RealmManager { get; private set; }

        static void Main(string[] args)
        {
            // Processor test
            ProcessorManager.StartNewProcessor();

            // Realm test
            RealmManager = new RealmManager();
            RealmManager.CreateRealm("test realm");

            Console.ReadKey();
        }
    }
}
