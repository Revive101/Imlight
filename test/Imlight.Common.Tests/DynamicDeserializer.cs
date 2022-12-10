using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using Imlight.Engine;
using Imlight.Common.Logger;
using Newtonsoft.Json;

namespace Imlight.Engine
{
    public static class DynamicDeserializer
    {

        private static readonly string _recordsFileLocation = $"{Directory.GetCurrentDirectory()}/records/";
        private static readonly Dictionary<byte, DynamicProtocol> _protocols = new Dictionary<byte, DynamicProtocol>();
        private static bool _hasInitialized = false;

        public static void Init()
        {
            // If this has already been initialized, wipe it clean.
            if (_hasInitialized) _protocols.Clear();

            // Load all the *Messages.xml files in the "records" directory.
            // DO NOT SAVE THESE FILES TO THE REPOSITORY. MAKE SURE THEY ARE LOCAL ONLY.
            string[] files = Directory.GetFiles(
                _recordsFileLocation, 
                "*Messages.xml", 
                SearchOption.TopDirectoryOnly);

            // Kinda need those files to make a server, eh?
            if (files.Count() <= 0)
                throw new Exception("No *Messages.xml files were found in the \"records\" directory!");

            // Iterate through files and create a DynamicProtocol object from each of them.
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];

                // The DynamicProtocol will build itself from it's ctor and given parameter.
                DynamicProtocol protocol = new DynamicProtocol(file);

                // Record the newly created protocol to the library, with format [ID]: Object
                _protocols.Add(protocol.Info.ServiceID, protocol);

                Log.Info($"Created protocol {protocol.Name}");
            }

            _hasInitialized = true;
        }

        internal static void Clear()
        {
            _protocols.Clear();
            _hasInitialized = false;
        }

    }
}
