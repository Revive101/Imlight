/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.CoreLib.Login;
using Imlight.CoreLib.Patch;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.Director;

internal static class Program {
    // Major versions in order:
    // Imlight - PROTO   -- Marks the beginning of the project. Very early serialization and networking.
    // Imlight - NETHRA  -- We are in-game. The game is playable and mostly stable, but not feature complete.
    // Imlight - KALI    -- We feel very confident in the stability of previous systems. We are now focusing on combat.
    private const string ActorSystemName = "Imlight";
    private const string MajorVersion = "NETHRA";
    private const string Version = "1.2.0";

    private static ActorSystem s_imlightSystem;
    private static ResourceContainer s_resourceContainer;

    private static void Main() {
        // =============================================================
        // TIDBITS
        // =============================================================
        PrintTitle();

        // =============================================================
        // AKKA.NET CONFIGURATION
        // =============================================================
        Logger.Information("Getting Akka.NET configuration..");
        if (!AkkaConfiguration.CreateActorSystem(ActorSystemName, out var system)) {
            Logger.Fatal($"Could not find Akka.NET config file!");

            Console.ReadKey();
            return;
        }
        Logger.Information("Akka.NET system created.");
        s_imlightSystem = system;

        // =============================================================
        // RESOURCES
        // =============================================================
        // The patch server must start prior to any resources. This is to ensure that any
        // missing resources may be downloaded from the patch server as needed.
        var task = StartPatchServer();
        task.Wait();

        Logger.Information("Director is now explicitly loading resources..");
        s_resourceContainer = new ResourceContainer();
        Logger.Information("Director has called all resources to load.");

        // =============================================================
        // SERVERS
        // =============================================================
        var loginServer = StartLoginServer();
        StartGameServer(loginServer);

        // Force load dragon database. Create a dud account if the database ends up using the embedded database.
        _ = PlayerDatabase.Instance.Store;
        if (PlayerDatabase.Instance.IsEmbedded) {
            CreateEmbeddedDatabaseAccounts();
        }

        // Keep program busy with a while loop.
        Logger.Information("Imlight may now be connected to.");
        while (true) {
            // Sleep for 5 minutes.
            Thread.Sleep(300000);

            Logger.Information("Still alive..");
        }
    }

    private static IActorRef StartLoginServer() {
        var LoggerinServerName = ConfigurationManager.Settings.LoginServerName;
        var LoggerinServerPort = ConfigurationManager.Settings.LoginServerPort;

        var LoggerinProps = LoginServer.Props(LoggerinServerName, LoggerinServerPort);
        var LoggerinServer = s_imlightSystem.ActorOf(LoggerinProps, LoggerinServerName);

        Logger.Debug("New actor created under {systemName}: {LoggerinServerName}",
            Logger.Args(s_imlightSystem.Name, LoggerinServerName));

        return LoggerinServer;
    }

    private static void StartGameServer(IActorRef LoggerinActorRef) {
        var msg = new SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER();
        LoggerinActorRef.Tell(msg);
    }

    private static async Task StartPatchServer() {
        var defaultPatchServerName = ConfigurationManager.Settings.PatchServerName;
        var defaultPatchServerPort = ConfigurationManager.Settings.PatchServerPort;

        var patchProps = PatchServer.Props(defaultPatchServerName, defaultPatchServerPort);
        var actor = s_imlightSystem.ActorOf(patchProps, defaultPatchServerName);

        Logger.Debug("New actor created under {systemName}: {patchServerName}",
            Logger.Args(s_imlightSystem.Name, defaultPatchServerName));

        // Await initialization of the patch server.
        await actor.Ask<SERVER_100_PROTOCOL.MSG_INITIALIZE_COMPLETE>(new SERVER_100_PROTOCOL.MSG_INITIALIZE());
    }

    private static void CreateEmbeddedDatabaseAccounts() {
        Logger.Information("Creating embedded database accounts. If you don't see anything, they already exist!");

#if DEBUG
        // Generic developer accounts
        for (int i = 1; i <= 3; i++) {
            DatabaseUtilities.CreateEmbeddedDatabaseAccount($"dev{i}", $"dev{i}@r101.com", "dev9999", AuthLevel.Administrator);
        }
#endif

        // Dev accounts; Hi, devs! Feel free to make your own account and add it here.
        new Jooty("jooty", "2342", "jay@r101net", AuthLevel.Administrator);

        // Hard code hall monitor lead accounts. Don't share these passwords!
        new Mitsu("mitsu", "2034", "mitsu@r101.net", AuthLevel.Administrator);

        // Hall Monitor accounts.
        new PokemonHacker("pk", "7878", "pk@r101.net", AuthLevel.HallMonitor);
        new Tilr("tilr", "8080", "tilr@r101.net", AuthLevel.HallMonitor);

        // Quality Assurance accounts
        new B("b", "1121", "b@r101.net", AuthLevel.QualityAssurance);
        new Dalnakii("dalnakii", "0091", "b@r101.net", AuthLevel.QualityAssurance);
        new DarkLegend("darklegend", "1041", "darklegend@r101.net", AuthLevel.QualityAssurance);
        new FangYaoban("fangyaobang", "2290", "fang@r101.net", AuthLevel.QualityAssurance);
        new Griz("grizzly", "9142", "griz@r101.net", AuthLevel.QualityAssurance);
        new Nyakua("nyakua", "6142", "nyakua@r101.net", AuthLevel.QualityAssurance);
        new Pluto("pluto", "1224", "pluto@r101.net", AuthLevel.QualityAssurance);
        new Socks("socks", "8723", "socks@r101.net", AuthLevel.QualityAssurance);

        Logger.Information("Embedded database accounts created.");
    }

    private static void PrintTitle() {
        // Write the title. This is a bit of a mess, but it's the best I could do.
        Console.WriteLine(@" _____           _ _       _     _    ______   ");
        Console.WriteLine(@"|_   _|         | (_)     | |   | |   \ \ \ \  ");
        Console.WriteLine(@"  | |  _ __ ___ | |_  __ _| |__ | |_   \ \ \ \ ");
        Console.WriteLine(@"  | | | '_ ` _ \| | |/ _` | '_ \| __|   \ \ \ \");
        Console.WriteLine(@" _| |_| | | | | | | | (_| | | | | |_    / / / /");
        Console.WriteLine(@"|_____|_| |_| |_|_|_|\__, |_| |_|\__|  / / / / ");
        Console.WriteLine(@"===================== __/ |===========/_/_/_/  ");

        // Write the boot type.
        // Soon in the future Imlight will actually have different boot types. For now, it's just L&G, which stands
        // for Loggerin & Game.
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("   :: L&G Boot ::    ");
        Console.ForegroundColor = ConsoleColor.White;

        // Write version.
        var buildConfiguration = GetBuildConfiguration();
        Console.Write(@"|___/");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"   ({MajorVersion}-v{Version} {buildConfiguration})\n");
        Console.WriteLine("");
    }

    private static string GetBuildConfiguration() {
#if DEBUG
        return "DEBUG";
#else
			return "RELEASE";
#endif
    }
}
