using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Crypto;
using Imlight.Login;
using System;
using System.IO;
using Imlight.Net;
using WizUnraveler;
using WizUnraveler.Cache;
using Imlight.Game;
using Imlight.Resources;

namespace Imlight.Backend
{
    internal class Program
    {
        private const string VERSION = "0.0.1";
        private const string ACTOR_SYSTEM_NAME = "Imlight";

        private static ActorSystem _imlightSystem;

        static void Main(string[] args)
        {
            // =============================================================
            // TIDBITS
            // =============================================================
            PrintTitle();

            // =============================================================
            // AKKA.NET CONFIGURATION
            // =============================================================
            Log.Logger.Information("Getting Akka.NET configuration..");

            if (!AkkaConfiguration.CreateActorSystem(ACTOR_SYSTEM_NAME, out var system))
            {
                Log.Logger.Fatal($"Could not find Akka.NET config file!");

                Console.ReadKey();
                return;
            }

            Log.Logger.Information("Akka.NET configuration complete.");

            _imlightSystem = system;

            // =============================================================
            // RESOURCES
            // =============================================================
            Log.Logger.Information("Gathering appropriate resources..");

            CoreObjectFactory.SetRoot($"{Directory.GetCurrentDirectory()}/Input/Root.wad");

            Log.Logger.Information("Resources successfully allocated.");

            // =============================================================
            // SERVERS
            // =============================================================
            Log.Logger.Information("Starting servers..");

            StartLoginServer();

            Log.Logger.Information("All servers started.");

            Console.ReadKey();
        }

        private static void StartLoginServer()
        {
            var loginProps = LoginServer.Props();
            _imlightSystem.ActorOf(loginProps, "Imlight.LoginServer");
        }

        private static void PrintTitle()
        {
            // I just like having fun.
            Log.Logger.Information(@"==============================================================================================");
            Log.Logger.Information(@" ______  __       __  __        ______   ______   __    __  ________");
            Log.Logger.Information(@"/      |/  \     /  |/  |      /      | /      \ /  |  /  |/       |");
            Log.Logger.Information(@"$$$$$$/ $$  \   /$$ |$$ |      $$$$$$/ /$$$$$$  |$$ |  $$ |$$$$$$$$/");
            Log.Logger.Information(@"  $$ |  $$$  \ /$$$ |$$ |        $$ |  $$ | _$$/ $$ |__$$ |   $$ |");
            Log.Logger.Information(@"  $$ |  $$$$  /$$$$ |$$ |        $$ |  $$ |/    |$$    $$ |   $$ |");
            Log.Logger.Information(@"  $$ |  $$ $$ $$/$$ |$$ |        $$ |  $$ |$$$$ |$$$$$$$$ |   $$ |");
            Log.Logger.Information(@" _$$ |_ $$ |$$$/ $$ |$$ |_____  _$$ |_ $$ \__$$ |$$ |  $$ |   $$ |");
            Log.Logger.Information(@"/ $$   |$$ | $/  $$ |$$       |/ $$   |$$    $$/ $$ |  $$ |   $$ |");
            Log.Logger.Information(@"$$$$$$/ $$/      $$/ $$$$$$$$/ $$$$$$/  $$$$$$/  $$/   $$/    $$/");
            Log.Logger.Information(@"==============================================================================================");
            Log.Logger.Information($"Imlight v{VERSION} -- Developed and maintained by Revive101.");
            Log.Logger.Information(@"==============================================================================================");
        }
    }
}
