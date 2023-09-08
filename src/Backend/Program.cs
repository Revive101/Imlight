/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common.Configuration;
using Imlight.Common.Utilities;
using Imlight.Server.Login;
using Imlight.Server.Login.Models;
using Imlight.Server.Patch;
using Imlight.Server.Shared.Packets;
using Imlight.Server.Shared.Resources;
using Imlight.Server.WizardData;
using Imlight.Server.WizardData.Implementations;

namespace Imlight.Backend
{
    internal static class Program
    {
        private const string ActorSystemName = "Imlight";
        private const string VersionScriptBash = @"
            total_commits=$(git rev-list --count HEAD);
            num_merges=$(git log --oneline --merges | wc -l)
            num_features=$(git log --oneline | grep -c 'feat:');
            num_fixes=$(git log --oneline | grep -c 'fix:');
            major=$((num_merges));
            minor=$((num_features));
            patch=$((num_fixes));
            version=""$major.$minor.$patch"";
            echo $version";
        private const string VersionScriptBatch = @"
            @echo off
            for /f %%A in ('git rev-list --count HEAD') do set total_commits=%%A
            for /f %%B in ('git log --oneline --merges ^| find /c /v """"') do set num_merges=%%B
            for /f %%C in ('git log --oneline ^| find /c ""feat:""') do set num_features=%%C
            for /f %%D in ('git log --oneline ^| find /c ""fix:""') do set num_fixes=%%D

            set /a major=num_merges
            set /a minor=num_features
            set /a patch=num_fixes

            set version=%major%.%minor%.%patch%
            echo %version%";

        private static ActorSystem _imlightSystem;

        private static void Main()
        {
            // =============================================================
            // TIDBITS
            // =============================================================
            PrintTitle();

            // =============================================================
            // AKKA.NET CONFIGURATION
            // =============================================================
            Log.Information("Getting Akka.NET configuration..");
            if (!AkkaConfiguration.CreateActorSystem(ActorSystemName, out var system))
            {
                Log.Fatal($"Could not find Akka.NET config file!");

                Console.ReadKey();
                return;
            }
            Log.Information("Akka.NET system created.");
            _imlightSystem = system;

            // =============================================================
            // RESOURCES
            // =============================================================
            // The patch server must start prior to any resources. This is to ensure that any
            // missing resources may be downloaded from the patch server as needed.
            var task = StartPatchServer();
            task.Wait();

            Log.Information("Gathering appropriate resources..");
            var resourceLoadResult = ResourceManager.Initialize();
            if (!resourceLoadResult)
            {
                Log.Fatal($"Could not load resources!");

                Console.Read();
                return;
            }
            Log.Information("Resources successfully allocated.");

            // =============================================================
            // SERVERS
            // =============================================================
            var loginServer = StartLoginServer();
            StartGameServer(loginServer);
            
            // Force load dragon database. Create a dud account if the database ends up using the embedded database.
            _ = PlayerDatabase.Instance.Store;
            if (PlayerDatabase.Instance.IsEmbedded)
                CreateEmbeddedDatabaseAccounts();

            // Keep program busy with a while loop.
            Log.Information("Imlight may now be connected to.");
            while (true)
            {
                // Sleep for 5 minutes.
                Thread.Sleep(300000);
                
                Log.Information("Still alive..");
            }
        }

        private static IActorRef StartLoginServer()
        {
            var loginServerName = ConfigurationManager.Settings.LoginServerName;
            var loginServerPort = ConfigurationManager.Settings.LoginServerPort;
            
            var loginProps = LoginServer.Props(loginServerName, loginServerPort);
            var loginServer = _imlightSystem.ActorOf(loginProps, loginServerName);
            
            Log.Debug("New actor created under {systemName}: {loginServerName}", 
                Log.Args(_imlightSystem.Name, loginServerName));
            
            return loginServer;
        }

        private static void StartGameServer(IActorRef loginActorRef)
        {
            var msg = new SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER();
            loginActorRef.Tell(msg);
        }

        private static async Task StartPatchServer()
        {
            var defaultPatchServerName = ConfigurationManager.Settings.PatchServerName;
            var defaultPatchServerPort = ConfigurationManager.Settings.PatchServerPort;
            
            var patchProps = PatchServer.Props(defaultPatchServerName, defaultPatchServerPort);
            var actor = _imlightSystem.ActorOf(patchProps, defaultPatchServerName);
            
            Log.Debug("New actor created under {systemName}: {patchServerName}", 
                Log.Args(_imlightSystem.Name, defaultPatchServerName));

            // Await initialization of the patch server.
            await actor.Ask<SERVER_100_PROTOCOL.MSG_INITIALIZE_COMPLETE>(new SERVER_100_PROTOCOL.MSG_INITIALIZE());
        }

        private static void CreateEmbeddedDatabaseAccounts()
        {
            // Create 9 accounts with the username "admin" and a number from 1 to 9.
            for (int i = 1; i <= 9; i++)
            {
                DatabaseUtilities.CreateEmbeddedDatabaseAccount($"admin{i}", $"{i}@r101.com", "debug", AuthLevel.Administrator);
            }
        }

        private static void PrintTitle()
        {
            // Write the title. This is a bit of a mess, but it's the best I could do. 
            Console.WriteLine(@" _____           _ _       _     _    ______   ");
            Console.WriteLine(@"|_   _|         | (_)     | |   | |   \ \ \ \  ");
            Console.WriteLine(@"  | |  _ __ ___ | |_  __ _| |__ | |_   | | | | ");
            Console.WriteLine(@"  | | | '_ ` _ \| | |/ _` | '_ \| __|   \ \ \ \");
            Console.WriteLine(@" _| |_| | | | | | | | (_| | | | | |_    / / / /");
            Console.WriteLine(@"|_____|_| |_| |_|_|_|\__, |_| |_|\__|  | | | | ");
            Console.WriteLine(@"===================== __/ |===========/_/_/_/  ");
            
            // Write the boot type.
            // Soon in the future Imlight will actually have different boot types. For now, it's just L&G, which stands
            // for Login & Game.
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("   :: L&G Boot ::    ");
            Console.ForegroundColor = ConsoleColor.White;
            
            // Write version.
            var version = RunSemanticVersionScript();
            var buildConfiguration = GetBuildConfiguration();
            Console.Write(@"|___/");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"   (proto-v{version} {buildConfiguration})\n");
            Console.WriteLine("");
        }
        
        private static string RunSemanticVersionScript()
        {
            var process = new Process();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = "/c \"" + VersionScriptBatch.Replace("\"", "\\\"") + "\"";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                process.StartInfo.FileName = "bash";
                process.StartInfo.Arguments = "-c \"" + VersionScriptBash.Replace("\"", "\\\"") + "\"";
            }
            else
            {
                throw new NotSupportedException("Unsupported operating system");
            }

            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Trim();
        }

        private static string GetBuildConfiguration()
        {
#if DEBUG
            return "DEBUG";
#else
            return "RELEASE";
#endif
        }
    }
}
