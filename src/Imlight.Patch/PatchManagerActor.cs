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

            Receive<PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2>(x => ReceiveLatestFileList(x));
        }

        private void ReceiveLatestFileList(PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2 message)
        {
            Sender.Tell(new PATCH_8_PROTOCOL.MSG_LATEST_FILE_LIST_V2()
            {
                // Test.
                ListFileType = 2,
                LatestVersion = 1,
                ListFileSize = 39528,
                ListFileCRC = 0x9ceb63d7,
                ListFileName = "LatestFileList.bin",
                ListFileURL = "https://fastupload.io/download/1k0emb0gyzVb4/4QGd0qhSJITfgmZ/LatestFileList.bin",
                URLPrefix = @"D:\Wizard101(EN)",
                URLSuffix = "",
                ListFileTime = 0
            });
        }
    }
}
