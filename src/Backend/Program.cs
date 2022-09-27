using System;
using Imlight.Realm;

namespace Imlight.Backend
{
    internal class Program
    {

        internal static RealmManager RealmManager { get; private set; }

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            // Realm test
            RealmManager = new RealmManager();
            RealmManager.CreateRealm("test realm");
            Console.WriteLine("Realm created!");

            Console.ReadKey();
        }
    }
}
