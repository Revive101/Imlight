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

            CoreObjectFactory.SetRoot($"{Directory.GetCurrentDirectory()}/Input/Root.wad");

            _imlightSystem = ActorSystem.Create(ACTOR_SYSTEM_NAME);

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

        private static void cktest()
        {
            ushort sid = 3258;
            uint secs = 1617815695;
            uint milli = 805;
            var test = "+FO9W7DLYNuvLdwvnMaxtJrSD+/h7HHfpzSNKv6G4UomKKoy+uwknGbqrtz4KNHSIS6McowtSTXtQBwwq7bwSQ==";

            var y = ClientKey.VerifyCK1("1", sid, secs, milli, test);
        }

        private static void rec1test()
        {
            ushort sid = 44761;
            uint secs = 1675419571;
            uint millis = 763;
            var ck1 = "+FO9W7DLYNuvLdwvnMaxtJrSD+/h7HHfpzSNKv6G4UomKKoy+uwknGbqrtz4KNHSIS6McowtSTXtQBwwq7bwSQ==";
            var rec1Input = new byte[]
            {
                0xab, 0xb3, 0xf6, 0xeb, 0x61, 0x20, 0x34, 0x54,
                0x77, 0x3c, 0x09, 0x65, 0xeb, 0x9c, 0x67, 0x49,
                0xa2, 0xae, 0xac, 0x97, 0xe3, 0x38, 0x79, 0xb4,
                0x40, 0x68, 0xc3, 0x0e, 0xd3, 0xc4, 0xad, 0xee,
                0x0a, 0xa4, 0x3c, 0xec, 0xb4, 0xa4, 0x7b, 0xb1,
                0xeb, 0x36, 0x91, 0x9e, 0xed, 0x32, 0xc1, 0x1b,
                0xc7, 0x44, 0x7f, 0xc6, 0x47, 0x31, 0x6c, 0x98,
                0x2b, 0x6a, 0x9b, 0x27, 0x58, 0x0c, 0x1c, 0xab,
                0x32, 0x84, 0x0b, 0x57, 0x41, 0x25, 0x60, 0xf4,
                0xf3, 0xf5, 0x8c, 0x66, 0xc5, 0x55, 0xa0, 0xb2,
                0x2b, 0x1d, 0x76, 0x47, 0x43, 0x07, 0x48, 0x4f,
                0x65, 0xa4, 0x79, 0x5b, 0x50, 0xee, 0x97, 0x66,
                0xcf, 0x1a, 0x76, 0xd8, 0xfb, 0xc5, 0x52, 
            };

            var enc = Rec1.Encode(sid, "1", ck1, secs, millis);

            var dec = Rec1.Decode(rec1Input, sid, secs, millis);
            var decStr = new WizUnraveler.ByteString(dec);
        }
    }
}
