/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.CoreLib.Login;
using Imlight.CoreLib.Patch;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.Director;

internal static class Program {
    // Major versions in order:
    // Imlight - PROTO   -- Marks the beginning of the project. Very early serialization and networking.
    // Imlight - NETHRA  -- We are in-game. The game is playable and mostly stable, but not feature complete.
    // Imlight - KALI    -- We feel very confident in the stability of previous systems, and are becoming feature complete.
    private const string ActorSystemName = "Imlight";
    private const string MajorVersion = "KALI";

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
        CreateEmbeddedDatabaseAccounts();

        OnlinePlayerCollection.Clear();

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
        // At least one account needs to exist.
        var adminAccountUsername = ConfigurationManager.Settings.AdminAccountUsername;
        var adminAccountPassword = ConfigurationManager.Settings.AdminAccountPassword;
        DatabaseUtilities.CreateEmbeddedDatabaseAccount(adminAccountUsername, "testtest@r101.com", adminAccountPassword, AuthLevel.Administrator);
        Logger.Information("Created admin account.");

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
        // for Login & Game server.
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("   :: L&G Boot ::    ");
        Console.ForegroundColor = ConsoleColor.White;

        // Write the version. Our schema is NNyWWc, where NN is the two-digit year, and WW is the week of the year.
        // c is the build configuration, which is either DEBUG or RELEASE.
        var buildConfiguration = GetBuildConfiguration();
        Console.Write(@"|___/");
        Console.ForegroundColor = ConsoleColor.DarkGray;

        // Get the current year and week of the year.
        var year = DateTime.Now.Year.ToString().Substring(2);
        DateTime today = DateTime.Now;
        int quarter = (today.Month - 1) / 3 + 1;
        // Get the ISO week number
        int week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                        today,
                        CalendarWeekRule.FirstDay,
                        DayOfWeek.Monday);

        Console.Write($" ({MajorVersion} {year}Q{quarter}.{week}c {buildConfiguration})\n");
        Console.WriteLine("");
    }

    private static string GetBuildConfiguration() {
        #if DEBUG
            return "dev";
        #else
            // We're calling release builds canary since we're not public and deploying to QA instead.
		    return "canary";
        #endif
    }
}
