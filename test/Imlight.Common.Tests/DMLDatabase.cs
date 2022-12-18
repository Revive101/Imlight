using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common.Logger;

namespace Imlight.Engine.DML
{
    /// <summary>
    /// Creates and manages DMLProtocol instances.
    /// </summary>
    public static class DMLDatabase
    {

        private static readonly string _recordsFileLocation = $"{Directory.GetCurrentDirectory()}/records/";

        private static Dictionary<byte, DMLProtocol> _protocols;
        private static bool _hasInitialized;

        /// <summary>
        /// Initializes all DML protocols from the XML files in the 'Records' local directory.
        /// </summary>
        public static void Initialize()
        {
            // If protocols have already been initialized, warn the user and continue.
            if (_hasInitialized)
            {
                Log.Warn("DMLProtocolManager was already initialized! Did you mean to do this?");
                ClearAndDisposeProtocols();
            }

            // Search for the local record files. They're in XML format and come from the Wizard101 client.
            // Be sure to read the w101r documenatation on this; do NOT commit these records to any repository.
            if (TryGetRecordsFiles(out var files))
            {
                Log.Info("Found record files..");

                _protocols = new Dictionary<byte, DMLProtocol>();
                foreach (var file in files)
                {
                    // Create a new DMLProtocol object and set it's data.
                    DMLProtocol protocol = new DMLProtocol();
                    protocol.FromXMLTemplate(file);

                    // Record the newly created protocol to the library, with format [ID]: Object
                    _protocols.Add(protocol.Info.ServiceID, protocol);

                    Log.Info($"Created protocol {protocol.Name}");
                }

                _hasInitialized = true;
            }
            else
            {
                Log.Fatal("Could not get record files!");
                return;
            }
        }

        /// <summary>
        /// Attempts to retrieve a DML protocol by id.
        /// </summary>
        /// <param name="id">The ID of the DML protocol to search for.</param>
        /// <param name="dmlProtocol">The outgoing variable equal to the DML protocol, if one is found.</param>
        /// <returns>True, if a protocol is found. False otherwise.</returns>
        public static bool TryGetProtocolByID(byte id, out DMLProtocol dmlProtocol)
        {
            dmlProtocol = default;
            if (_protocols.TryGetValue(id, out var val))
            {
                dmlProtocol = val;
                return true;
            }
            else return false;
        }


        /// <summary>
        /// Searches and returns a DML record.
        /// </summary>
        /// <param name="serviceID">The service ID of the protocol.</param>
        /// <param name="messageID">The message ID of the record.</param>
        /// <returns>A DMLRecord object.</returns>
        public static DMLRecord GetRecord(byte serviceID, byte messageID)
        {
            if (!TryGetProtocolByID(serviceID, out DMLProtocol protocol))
                throw new DMLException($"A protocol by id [{serviceID}] was not found.");
            if (protocol.TryCloneRecordTemplate(messageID, out DMLRecord record))
                throw new DMLException($"A record template by id [{messageID}] on protocol [{protocol.Name}] was not found.");

            return record;
        }

        private static bool TryGetRecordsFiles(out string[] files)
        {
            // Load all the *Messages.xml files in the "records" directory.
            // DO NOT SAVE THESE FILES TO THE REPOSITORY. MAKE SURE THEY ARE LOCAL ONLY.
            files = Directory.GetFiles(
                _recordsFileLocation,
                "*Messages.xml",
                SearchOption.TopDirectoryOnly);
            
            return files.Count() > 0;
        }

        private static void ClearAndDisposeProtocols()
        {
            foreach (DMLProtocol protocol in _protocols.Values)
            {
                protocol.DisposeRecords();
            }
            _protocols.Clear();
            _protocols = null;
        }

    }
}
