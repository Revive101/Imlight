using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net;
using WizUnraveler;
using WizUnraveler.Cache;

namespace Imlight.Patch
{
    public class PatchManagerActor : ServerReceiverActor
    {
        public PatchManagerActor(string Name, sbyte ID, ushort port) : base(Name, ID, port) { }

        public static Props Props(string Name, sbyte ID, ushort port)
        {
            return Akka.Actor.Props.Create(() => new PatchManagerActor(Name, ID, port));
        }

        protected override void ConfigureReceivers()
        {
            base.ConfigureReceivers();

            Receive<PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2>(x =>
            {
                Sender.Tell(new PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2()
                {
                    // Test.
                    ListFileType = 2,
                    LatestVersion = 1,
                    ListFileSize = 867048,
                    ListFileCRC = 0x53f5404c,
                    ListFileName = "LatestFileList.bin",
                    ListFileURL = @"C:\ProgramData\KingsIsle Entertainment\Wizard101(IT)\PatchInfo\LatestFileList_English.bin",
                    URLPrefix = "http://dlcl.gfsrv.net/wizard101en/patch/LatestBuild",
                    URLSuffix = "",
                    ListFileTime = 0
                });
            });
        }
    }
}
