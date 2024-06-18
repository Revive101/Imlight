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
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.Director.EmbeddedAccounts;
using Imlight.Common.ObjectProperty;
using System.Linq;
using System.Net.NetworkInformation;
using Imlight.Common.Cryptography;
using System.Collections.Generic;

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
        var blob = "70 C7 21 63 01 00 00 00 AE 97 AE 64 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 15 00 57 69 7A 61 72 64 51 75 65 73 74 47 6F 61 6C 73 5F 4B 69 6C 6C 00 00 C7 0D 27 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 02 00 00 00 01 00 00 13 00 57 69 7A 61 72 64 4D 6F 62 73 5F 30 30 30 30 30 31 31 36 12 00 5A 6F 6E 65 4C 6F 63 4E 61 6D 65 5F 35 39 35 33 39 30 20 00 57 69 7A 61 72 64 43 69 74 79 2F 57 43 5F 53 74 72 65 65 74 73 2F 57 43 5F 55 6E 69 63 6F 72 6E 32 00 47 55 49 2F 4E 70 63 50 6F 72 74 72 61 69 74 73 2F 50 6F 72 74 72 61 69 74 5F 47 68 6F 73 74 5F 4C 6F 73 74 53 6F 75 6C 5F 4D 4F 42 5F 41 2E 64 64 73 00 00 00 00 00 00 00 00 00 00";
        var bytes = blob.Split(' ').Select(x => Convert.ToByte(x, 16)).ToArray();
        var ser = new ObjectSerializer().OnBehaviors(SerializerOptions.Behaviors.None);
        var obj = ser.Deserialize(bytes);

        MakeHashes();

        var prop1 = StringHash.HashPropertyName("m_manaFlat", "int");
        var prop2 = StringHash.HashPropertyName("m_manaPercent", "float");

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
        new MoMi("MoMi", "joji1", "MoMi@r101net", AuthLevel.Administrator);
        new Joji("joji", "jootysocoollike", "joji@r101.net", AuthLevel.Administrator);
        new MoMi("MoMi", "2109", "MoMi@r101.net", AuthLevel.Administrator);
        new Phill("phill", "gay", "phill@r101.net", AuthLevel.Administrator);
        new Jeff("jeff", "jefffakename", "jeff@r101.net", AuthLevel.Administrator);
        new Rocket("rocket", "7969", "rocket@r101.net", AuthLevel.Developer);

        // Hard code hall monitor lead accounts. Don't share these passwords!
        new Mitsu("mitsu", "2034", "mitsu@r101.net", AuthLevel.Administrator);
        new Kid("Niduus", "Niduus", "Niduus@r101.net", AuthLevel.Administrator);

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
        new Tommyw3b("tommyw3b", "9871", "Tommyw3b@r101.net", AuthLevel.QualityAssurance);
        new Storm("kf55", "kcsd10", "Storm@r101.net", AuthLevel.QualityAssurance);
        new EmbeddedAccounts.Ping("ping", "7041", "storm@r101.net", AuthLevel.QualityAssurance);
        new GMZ("GMZ", "1194", "gmz@r101.net", AuthLevel.QualityAssurance);
        new Abyss("Wizard", "0010", "abyss@r101.net", AuthLevel.QualityAssurance);
        new J3("J3", "4001", "J3@r101.net", AuthLevel.QualityAssurance);
        new ATM("ATM", "9312", "ATM@r101.net", AuthLevel.QualityAssurance);

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

    private static void MakeHashes() {
        // Open a text file "/home/jay/Projects/Imlight-resources/manifests/"
        // Read each line
        var lines = System.IO.File.ReadAllLines("/home/jay/Projects/Imlight-resources/manifests/result_dump.txt");

        // Some lines are formatted as "Class: class ResAddTrainingPoints"
        // Grab these lines.
        var classLines = lines.Where(x => x.Contains("Class: class")).ToList();

        // Split by the colon and trim
        var splitLines = classLines.Select(x => x.Split(':').Select(y => y.Trim()).ToArray()).ToList();

        // Write to file
        using var writer = new System.IO.StreamWriter("/home/jay/Projects/Imlight-resources/manifests/hashes.txt");
        foreach (var line in splitLines) {
            var hash = StringHash.Compute(line[1]);
            var className = line[1].Split(' ')[1];
            writer.WriteLine($"{hash} => new {className}(),");
        }

        writer.Write("\n\n\n");

        foreach (var line in splitLines) {
            var hash = StringHash.Compute(line[1]);
            var className = line[1].Split(' ')[1];

            // Find all lines indented under the current class definition
            var classLine = lines.First(x => x.Contains(line[1]));
            var index = Array.IndexOf(lines, classLine);
            var properties = new List<string>();
            for (int i = index + 1; i < lines.Length; i++) {
                if (lines[i].Contains("Property:")) {
                    properties.Add(lines[i].Trim());
                } else {
                    break;
                }
            }

            writer.WriteLine($"public class {className} : TypeCache.Result {{");
            writer.WriteLine($"    public override uint GetHash() => {hash};\n");

            // Write comments for each property
            foreach (var property in properties) {
                writer.WriteLine($"    // {property}");
            }

            writer.WriteLine("}");
            writer.Write("\n");
        }
    }
}
