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
 * Last Updated: 10/25/2026
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Auth;
using Imlight.CoreLib.Login;
using Imlight.CoreLib.Patch;
using Imlight.CoreLib.Shared.Networking;
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
/// The server runs indefinitely after initialization. A startup step that fails logs one Fatal line and exits with 1.
/// </remarks>
internal static class Program {

    // Major versions in order:
    // Imlight - PROTO   -- Marks the beginning of the project. Very early serialization and networking.
    // Imlight - NETHRA  -- We are in-game. The game is playable and mostly stable, but not feature complete.
    // Imlight - KALI    -- We feel very confident in the stability of previous systems, and are becoming feature complete.
    private const string ActorSystemName = "Imlight";
    private const string MajorVersion = "KALI";
    private const string ADMIN_ACCOUNT_PLACEHOLDER_EMAIL = "testtest@r101.com";
    private const string SHIPPED_ADMIN_ACCOUNT_PASSWORD = "compsci";

    private static ActorSystem s_imlightSystem;

    private static string s_loginServerName;
    private static ushort s_loginServerPort;
    private static string s_patchServerName;
    private static ushort s_patchServerPort;
    private static byte s_gameServerCount;
    private static byte s_maxGameServersAllowed;
    private static string s_gameServerName;
    private static ushort s_gameServerPort;
    private static string s_gameServerIp;
    private static string[] s_realmNames;
    private static string s_adminAccountUsername;
    private static string s_adminAccountPassword;

    private static void Main() {
        // =============================================================
        // THREAD POOL
        // =============================================================
        // Sessions block threads on actor asks while connecting and disconnecting, so a burst of
        // zone changes must not wait on the pool's slow ramp-up.
        _ = ThreadPool.SetMinThreads(workerThreads: 200, completionPortThreads: 200);

        // =============================================================
        // TIDBITS
        // =============================================================
        PrintTitle();

        // =============================================================
        // IMLIGHT CONFIGURATION
        // =============================================================
        // The logger reads its settings from Imlight.ini, so a missing file can only go to the console.
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config/Imlight.ini");
        if (!File.Exists(configPath)) {
            Console.Error.WriteLine($"Missing {configPath}. Restore Config/Imlight.ini from the release download.");
            Environment.ExitCode = 1;

            return;
        }

        ConfigurationManager.Initialize(configPath);
        if (!TryRunStartupStep("read Config/Imlight.ini", "Set it, then restart.", ReadSettings)) {
            return;
        }

        Logger.Information("Imlight configuration loaded.");

        // =============================================================
        // AKKA.NET CONFIGURATION
        // =============================================================
        if (!AkkaConfiguration.CreateActorSystem(ActorSystemName, out var system)) {
            Environment.ExitCode = 1;

            return;
        }

        Logger.Debug("Akka.NET system created.");
        s_imlightSystem = system;

        // =============================================================
        // RESOURCES
        // =============================================================
        // The patch server must start prior to any resources. This is to ensure that any
        // missing resources may be downloaded from the patch server as needed.
        if (!TryRunStartupStep("start the patch server", "Check [Patch Server] in Config/Imlight.ini, then restart.",
                () => StartPatchServer().Wait())) {
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (!TryRunStartupStep("load the game files", "Check the lines above, then restart.",
                () => _ = new ResourceContainer())) {
            return;
        }

        if (!TryRunStartupStep("load the world data", "Check the SpiralDB lines above, then restart.", LoadResources)) {
            return;
        }

        stopwatch.Stop();
        Logger.Information("Resources loaded in {ElapsedMs} ms.", Logger.Args(stopwatch.ElapsedMilliseconds));

        // =============================================================
        // SERVERS
        // =============================================================
        var loginServer = StartLoginServer();
        StartGameServers(loginServer);

        if (!TryRunStartupStep("prepare the player database", "Check [Database] in Config/Imlight.ini, then restart.",
                PreparePlayerDatabase)) {
            return;
        }

        LoginWarmup.Run();

        // The login and game servers accept no connection until everything above is done.
        if (!TryRunStartupStep("open the login and game server ports",
                "Check that no other program uses them, then restart.", () => StartListening(loginServer))) {
            return;
        }

        var loginAddress = string.IsNullOrWhiteSpace(s_gameServerIp)
            ? $"port {s_loginServerPort}"
            : $"{s_gameServerIp}:{s_loginServerPort}";
        Logger.Information("Imlight is ready. Players connect to {LoginAddress}.", Logger.Args(loginAddress));

        while (true) {
            // Sleep for 5 minutes.
            Thread.Sleep(300000);

            Logger.Debug("Still alive..");
        }
    }

    private static void StartListening(IActorRef loginServer) {
        var message = new SERVER_100_PROTOCOL.MSG_STARTLISTENING();
        _ = loginServer
            .Ask<SERVER_100_PROTOCOL.MSG_STARTLISTENING_COMPLETE>(message, Server.StartListeningTimeout)
            .GetAwaiter()
            .GetResult();
    }

    private static void LoadResources() {
        // Load SpiralDB — the in-memory world database from JSON files.
        SpiralDB.Load();

        if (SpiralDB.ZoneData.Count == 0) {
            throw new InvalidOperationException("SpiralDB loaded no world data.");
        }
    }

    private static IActorRef StartLoginServer() {
        var loginServerName = s_loginServerName;
        var loginServerPort = s_loginServerPort;

        var loginServerProps = LoginServer.Props(loginServerName, loginServerPort);
        var loginServerActor = s_imlightSystem.ActorOf(loginServerProps, loginServerName);

        Logger.Debug("New actor created under {SystemName}: {LoginServerName}",
            Logger.Args(s_imlightSystem.Name, loginServerName));

        return loginServerActor;
    }

    private static void StartGameServers(IActorRef loginServerActorRef) {
        var count = Math.Max(s_gameServerCount, (byte) 1);
        var basePort = s_gameServerPort;

        if (count > s_maxGameServersAllowed) {
            Logger.Warning("[Game Server] GameServerCount is {Count}, more than MaxGameServersAllowed ({Max}), so only " +
                "that many realms start. Raise MaxGameServersAllowed to run more.",
                Logger.Args(count, s_maxGameServersAllowed));
            count = s_maxGameServersAllowed;
        }

        if (s_realmNames.Length < count) {
            Logger.Warning("[Game Server] RealmNames lists {Names} names for {Count} realms, so the others are " +
                "named Realm-{FirstUnnamed} onward.", Logger.Args(s_realmNames.Length, count, s_realmNames.Length + 1));
        }

        for (byte i = 0; i < count; i++) {
            var realmName = i < s_realmNames.Length 
                ? s_realmNames[i]
                : $"Realm-{i + 1}";
            var serverName = $"{s_gameServerName}.{realmName}";
            var port = (ushort) (basePort + i);

            var msg = new SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER {
                Name = serverName,
                Port = port,
                RealmName = realmName
            };
            loginServerActorRef.Tell(msg);

            Logger.Information("Starting realm {Realm} on port {Port}.", Logger.Args(realmName, port));
        }
    }

    private static async Task StartPatchServer() {
        var defaultPatchServerName = s_patchServerName;
        var defaultPatchServerPort = s_patchServerPort;

        var patchProps = PatchServer.Props(defaultPatchServerName, defaultPatchServerPort);
        var actor = s_imlightSystem.ActorOf(patchProps, defaultPatchServerName);

        Logger.Debug("New actor created under {SystemName}: {PatchServerName}",
            Logger.Args(s_imlightSystem.Name, defaultPatchServerName));

        // Await initialization of the patch server.
        _ = await actor.Ask<SERVER_100_PROTOCOL.MSG_INITIALIZE_COMPLETE>(
            new SERVER_100_PROTOCOL.MSG_INITIALIZE()
        );
    }

    private static void PreparePlayerDatabase() {
        _ = PlayerDatabase.Instance.Store
            ?? throw new InvalidOperationException("The embedded database did not start, see the error above.");
        CreateEmbeddedDatabaseAccounts();

        OnlinePlayerCollection.Clear();
        TradeEscrowCollection.RecoverAll();
    }

    private static void CreateEmbeddedDatabaseAccounts() {
        // At least one account needs to exist.
        DatabaseUtilities.CreateEmbeddedDatabaseAccount(
            s_adminAccountUsername,
            ADMIN_ACCOUNT_PLACEHOLDER_EMAIL,
            s_adminAccountPassword,
            AuthLevel.Administrator);

        Logger.Information("Admin account '{Username}' is ready.", Logger.Args(s_adminAccountUsername));
        if (s_adminAccountPassword == SHIPPED_ADMIN_ACCOUNT_PASSWORD) {
            Logger.Warning("[Database] AdminAccountPassword is still the shipped default, so anyone can sign in " +
                "as '{Username}'. Set your own before the account is first created.",
                Logger.Args(s_adminAccountUsername));
        }
    }

    private static void ReadSettings() {
        s_loginServerName = ReadRequiredSetting("Login Server", "LoginServerName", value => value.AsString());
        s_loginServerPort = ReadRequiredSetting("Login Server", "LoginServerPort", value => value.AsUShort());
        s_patchServerName = ReadRequiredSetting("Patch Server", "PatchServerName", value => value.AsString());
        s_patchServerPort = ReadRequiredSetting("Patch Server", "PatchServerPort", value => value.AsUShort());
        s_gameServerCount = ReadRequiredSetting("Game Server", "GameServerCount", value => value.AsByte());
        s_maxGameServersAllowed = ReadRequiredSetting("Game Server", "MaxGameServersAllowed", value => value.AsByte());
        s_gameServerName = ReadRequiredSetting("Game Server", "GameServerName", value => value.AsString());
        s_gameServerPort = ReadRequiredSetting("Game Server", "GameServerPort", value => value.AsUShort());
        s_gameServerIp = ConfigurationManager.Settings["Game Server.GameServerIP"].AsString();
        s_realmNames = ConfigurationManager.Settings["Game Server.RealmNames"].AsList();
        s_adminAccountUsername = ReadRequiredSetting("Database", "AdminAccountUsername", value => value.AsString());
        s_adminAccountPassword = ReadRequiredSetting("Database", "AdminAccountPassword", value => value.AsString());
    }

    private static T ReadRequiredSetting<T>(string section, string key,
                                            Func<ConfigurationManager.ConfigValue, T> convert) {
        var value = ConfigurationManager.GetSetting($"{section}.{key}");
        if (string.IsNullOrWhiteSpace(value)) {
            throw new FormatException($"[{section}] {key} is missing.");
        }

        try {
            return convert(new ConfigurationManager.ConfigValue(value));
        } catch (Exception ex) when (ex is FormatException or OverflowException) {
            throw new FormatException($"[{section}] {key} = {value} is not a valid value.");
        }
    }

    private static bool TryRunStartupStep(string step, string fixHint, Action run) {
        try {
            run();

            return true;
        } catch (Exception ex) {
            Logger.Fatal("Imlight could not {Step}: {Reason} {Fix}",
                Logger.Args(step, ex.GetBaseException().Message, fixHint));
            Logger.Debug("{Exception}", Logger.Args(ex));
            Environment.ExitCode = 1;

            return false;
        }
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
