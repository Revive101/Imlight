/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * IMLIGHT DIRECTOR SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Serves as the central orchestration point for initializing and managing 
 * the Imlight server ecosystem including Login, Game, and Patch servers.
 * 
 * USAGE EXAMPLE:
 * The application is launched directly with no command-line arguments.
 * All configuration is loaded from Config/Imlight.ini and Config/akka.conf files.
 * 
 * NOTE:
 * This system uses System.Threading and Akka.Actor for concurrency.
 * Resource loading may take significant time, and timing is logged.
 * Embedded database is initialized with a default admin account.
 *
 * TODO:
 * - Implement different boot types beyond L&G (Login & Game server)
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Login;
using Imlight.CoreLib.Patch;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.Director;

/// <summary>
/// Entry point for the Imlight Director - orchestrates the Login, Game, and Patch servers.
/// </summary>
/// <remarks>
/// The Director handles the initialization sequence including configuration loading, 
/// Akka.NET system setup, resource loading, server startup, and database preparation.
/// The server runs indefinitely after initialization with periodic "still alive" status messages.
/// </remarks>
internal static class Program {

    // Major versions in order:
    // Imlight - PROTO   -- Marks the beginning of the project. Very early serialization and networking.
    // Imlight - NETHRA  -- We are in-game. The game is playable and mostly stable, but not feature complete.
    // Imlight - KALI    -- We feel very confident in the stability of previous systems, and are becoming feature complete.
    private const string ActorSystemName = "Imlight";
    private const string MajorVersion = "KALI";

    private static ActorSystem s_imlightSystem;

    private static readonly string s_loginServerName = 
        ConfigurationManager.Settings["Login Server.LoginServerName"].AsString();
    private static readonly ushort s_loginServerPort = 
        ConfigurationManager.Settings["Login Server.LoginServerPort"].AsUShort();
    private static readonly string s_patchServerName = 
        ConfigurationManager.Settings["Patch Server.PatchServerName"].AsString();
    private static readonly ushort s_patchServerPort = 
        ConfigurationManager.Settings["Patch Server.PatchServerPort"].AsUShort();
    private static readonly byte s_gameServerCount = 
        ConfigurationManager.Settings["Game Server.GameServerCount"].AsByte();
    private static readonly string s_gameServerName = 
        ConfigurationManager.Settings["Game Server.GameServerName"].AsString();
    private static readonly ushort s_gameServerPort = 
        ConfigurationManager.Settings["Game Server.GameServerPort"].AsUShort();
    private static readonly string[] s_realmNames = 
        ConfigurationManager.Settings["Game Server.RealmNames"].AsString().Split(',');
    private static readonly string s_adminAccountUsername = 
        ConfigurationManager.Settings["Database.AdminAccountUsername"].AsString();
    private static readonly string s_adminAccountPassword = 
        ConfigurationManager.Settings["Database.AdminAccountPassword"].AsString();

    private static void Main() {
        // =============================================================
        // TIDBITS
        // =============================================================
        PrintTitle();

        // =============================================================
        // IMLIGHT CONFIGURATION
        // =============================================================
        ConfigurationManager.Initialize(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config/Imlight.ini")
        );
        Logger.Information("Imlight configuration loaded.");

        // =============================================================
        // AKKA.NET CONFIGURATION
        // =============================================================
        Logger.Information("Getting Akka.NET configuration..");
        if (!AkkaConfiguration.CreateActorSystem(ActorSystemName, out var system)) {
            Logger.Fatal($"Could not find Akka.NET config file!");
            _ = Console.ReadKey();

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

        // Load resources. Record the time it takes to load resources.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Logger.Information("Director is now explicitly loading resources..");
        var resourceContainer = new ResourceContainer();
        Logger.Information("Director has called all resources to load.");
        stopwatch.Stop();
        Logger.Information($"Resource loading completed in {0} ms.",
            Logger.Args(stopwatch.ElapsedMilliseconds));

        // =============================================================
        // SERVERS
        // =============================================================
        var loginServer = StartLoginServer();
        StartGameServers(loginServer);

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
        var loginServerName = s_loginServerName;
        var loginServerPort = s_loginServerPort;

        var loginServerProps = LoginServer.Props(loginServerName, loginServerPort);
        var loginServerActor = s_imlightSystem.ActorOf(loginServerProps, loginServerName);

        Logger.Debug("New actor created under {systemName}: {LoggerinServerName}",
            Logger.Args(s_imlightSystem.Name, loginServerName));

        return loginServerActor;
    }

    private static void StartGameServers(IActorRef loginServerActorRef) {
        var count = Math.Max(s_gameServerCount, (byte) 1);
        var basePort = s_gameServerPort;

        for (byte i = 0; i < count; i++) {
            var realmName = i < s_realmNames.Length 
                ? s_realmNames[i].Trim() 
                : $"Realm-{i + 1}";
            var serverName = $"{s_gameServerName}.{realmName}";
            var port = (ushort) (basePort + i);

            var msg = new SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER {
                Name = serverName,
                Port = port,
                RealmName = realmName
            };
            loginServerActorRef.Tell(msg);

            Logger.Information("Director requested game server '{Name}' on port {Port} (realm: {Realm}).",
                Logger.Args(serverName, port, realmName));
        }
    }

    private static async Task StartPatchServer() {
        var defaultPatchServerName = s_patchServerName;
        var defaultPatchServerPort = s_patchServerPort;

        var patchProps = PatchServer.Props(defaultPatchServerName, defaultPatchServerPort);
        var actor = s_imlightSystem.ActorOf(patchProps, defaultPatchServerName);

        Logger.Debug("New actor created under {systemName}: {patchServerName}",
            Logger.Args(s_imlightSystem.Name, defaultPatchServerName));

        // Await initialization of the patch server.
        _ = await actor.Ask<SERVER_100_PROTOCOL.MSG_INITIALIZE_COMPLETE>(
            new SERVER_100_PROTOCOL.MSG_INITIALIZE()
        );
    }

    private static void CreateEmbeddedDatabaseAccounts() {
        // At least one account needs to exist.
        var adminAccountUsername = s_adminAccountUsername;
        var adminAccountPassword = s_adminAccountPassword;
        DatabaseUtilities.CreateEmbeddedDatabaseAccount(
            adminAccountUsername,
            "testtest@r101.com",
            adminAccountPassword,
            AuthLevel.Administrator);

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
