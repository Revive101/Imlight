using System;
using Imlight.Realm;
using Imlight.Common;
using Imlight.Patch;
using Imlight.Net;
using System.Diagnostics;
using System.Collections.Generic;
using Akka;
using Akka.Actor;

namespace Imlight.Backend
{
    internal class Program
    {

        private const string VERSION = "0.0.1";
        private static ActorSystem _mainActorSystem;

        static void Main(string[] args)
        {
            PrintTitle();
            _mainActorSystem = ActorSystem.Create("ImlightSystem");
            var realmManagerActor = _mainActorSystem.ActorOf(RealmManagerActor.Props("DeveloperRealm", 0, 12000), "DeveloperRealm");
            //var patchManagerActor = _mainActorSystem.ActorOf(PatchManagerActor.Props("DeveloperPatch", 1, 12600), "DeveloperPatch");

            Console.ReadKey();
        }

        private static void PrintTitle()
        {
            // I just like having fun.
            Log.Logger.Information(@"==============================================================================================");
            Log.Logger.Information(@"  ______ __       __ __         ______   ______   __    __  ________");
            Log.Logger.Information(@" /      |/ \     /  |/  |      /      | /      \ /  |  /  |/       |");
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
