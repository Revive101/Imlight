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

namespace Imlight.Backend
{
    internal class Program
    {
        private const string VERSION = "0.0.1";
        private const string ACTOR_SYSTEM_NAME = "Imlight";
        private const string LOGIN_SERVER_NAME = "Imlight-Login";
        private const string GAME_SERVER_NAME  = "Imlight-Game";
        private const string PATCH_SERVER_NAME = "Imlight-Patch";
        private const int    LOGIN_SERVER_PORT = 12000;
        private const int    GAME_SERVER_PORT  = 12600;
        private const int    PATCHSERVER_PORT  = 12300;

        private static ActorSystem _imlightSystem;

        static void Main(string[] args)
        {
            PrintTitle();

            // @todo: change this to a resource manager
            CoreObjectFactory.SetRoot($"{Directory.GetCurrentDirectory()}/Input/Root.wad");

            if (!AkkaConfiguration.CreateActorSystem(ACTOR_SYSTEM_NAME, out var system))
            {
                Log.Logger.Fatal($"Could not find Akka.NET config file!");

                Console.ReadKey();
                return;
            }

            _imlightSystem = system;

            // Otherwise, we've had a successful Akka.NET initialization and we're ready to start
            // the servers.
            StartLoginServer();
            StartGameServer();

            Console.ReadKey();
        }

        private static void StartLoginServer()
        {
            var actorFactoryProps = LoginServiceActorFactory.Props();

            // Create the TcpServer on the system we just created.
            var tcpListenerProps = TcpListenerActor.Props(LOGIN_SERVER_NAME, LOGIN_SERVER_PORT, actorFactoryProps);
            var tcpActor = _imlightSystem.ActorOf(tcpListenerProps, $"{LOGIN_SERVER_NAME}_{LOGIN_SERVER_PORT}");

            Log.Logger.Information($"Login server created with name {LOGIN_SERVER_NAME} under port {LOGIN_SERVER_PORT}.");
        }

        private static void StartGameServer()
        {
            var actorFactoryProps = GameServiceActorFactory.Props();

            // Create the TcpServer on the system we just created.
            var tcpListenerProps = TcpListenerActor.Props(GAME_SERVER_NAME, GAME_SERVER_PORT, actorFactoryProps);
            var tcpActor = _imlightSystem.ActorOf(tcpListenerProps, $"{GAME_SERVER_NAME}_{GAME_SERVER_PORT}");

            Log.Logger.Information($"Game server created with name {GAME_SERVER_NAME} under port {GAME_SERVER_PORT}.");
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
