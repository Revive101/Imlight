/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common.Utilities;
using Imlight.Server.Login;
using Imlight.Server.Database;
using Imlight.Server.Patch;
using Imlight.Server.Shared.Packets;

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
            // The patch server must start prior to any resources. This is to ensure that any
            // missing resources may be downloaded from the patch server as needed.
            var task = StartPatchServer();
            task.Wait();

            Log.Logger.Information("Gathering appropriate resources..");
            var resourceLoadResult = ResourceManager.Initialize();
            if (!resourceLoadResult)
            {
                Log.Logger.Fatal($"Could not load resources!");

                Console.Read();
                return;
            }
            Log.Logger.Information("Resources successfully allocated.");

            // =============================================================
            // SERVERS
            // =============================================================
            StartLoginServer();
            // TODO: The game server should also start here.

            Console.Read();
        }

        private static void StartLoginServer()
        {
            var loginProps = LoginServer.Props();
            _imlightSystem.ActorOf(loginProps, LoginServer.DEFAULT_LOGIN_SERVER_NAME);
            
            Log.Logger.Debug($"New actor created under {_imlightSystem.Name}: {LoginServer.DEFAULT_LOGIN_SERVER_NAME}");
        }

        private static async Task StartPatchServer() 
        {
            var patchProps = PatchServer.Props();
            var actor = _imlightSystem.ActorOf(patchProps, PatchServer.DEFAULT_PATCH_SERVER_NAME);
            
            Log.Logger.Debug($"New actor created under {_imlightSystem.Name}: {PatchServer.DEFAULT_PATCH_SERVER_NAME}");

            // Await initialization of the patch server.
            await actor.Ask<SERVER_100_PROTOCOL.MSG_INITIALIZE_COMPLETE>(new SERVER_100_PROTOCOL.MSG_INITIALIZE());
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
