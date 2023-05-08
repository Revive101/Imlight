using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Crypto;
using Imlight.Login;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Imlight.Data;
using Imlight.Net;
using WizUnraveler;
using WizUnraveler.Cache;
using Imlight.Game;
using Imlight.Patch;
using Imlight.Data;
using WizUnraveler.ObjectProperty;

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

            Log.Logger.Information("Akka.NET system created.");

            _imlightSystem = system;

            // =============================================================
            // RESOURCES
            // =============================================================
            // TODO: Move this until after out patch server begins.
            Log.Logger.Information("Gathering appropriate resources..");

            var resourceLoadResult = ResourceManager.Initialize();
            if (!resourceLoadResult)
            {
                Log.Logger.Fatal($"Could not load resources!");

                Console.ReadKey();
                return;
            }

            Log.Logger.Information("Resources successfully allocated.");

            // =============================================================
            // SERVERS
            // =============================================================
            Log.Logger.Information("Starting servers..");

            StartLoginServer();
            StartPatchServer();

            Console.Read();
        }

        private static void StartLoginServer()
        {
            var loginProps = LoginServer.Props();
            _imlightSystem.ActorOf(loginProps, LoginServer.DEFAULT_LOGIN_SERVER_NAME);
            
            Log.Logger.Debug($"New actor created under {_imlightSystem.Name}: {LoginServer.DEFAULT_LOGIN_SERVER_NAME}");
        }

        private static void StartPatchServer() 
        {
            var patchProps = PatchServer.Props();
            _imlightSystem.ActorOf(patchProps, PatchServer.DEFAULT_PATCH_SERVER_NAME);
            
            Log.Logger.Debug($"New actor created under {_imlightSystem.Name}: {PatchServer.DEFAULT_PATCH_SERVER_NAME}");
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
            Log.Logger.Information($"Imlight v{VERSION} -- Developed and maintained by Wizard101Rewritten.");
            Log.Logger.Information(@"==============================================================================================");
        }
        
    }
}
