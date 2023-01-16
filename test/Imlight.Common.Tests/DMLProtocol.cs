using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using Imlight.Common.Logger;

namespace Imlight.Engine.DML
{
    public class DMLProtocol
    {

        public class DMLProtocolInformation
        {
            internal readonly byte ServiceID;
            internal readonly string ProtocolType;
            internal readonly Int32 ProtocolVersion;
            internal readonly string ProtocolDescription;

            internal readonly string Name;

            public DMLProtocolInformation(byte serviceID, string protocolType, int protocolVersion, string protocolDescription)
            {
                ServiceID = serviceID;
                ProtocolType = protocolType;
                ProtocolVersion = protocolVersion;
                ProtocolDescription = protocolDescription;

                // Create a formmated name for logging.
                Name = $"{this.ProtocolType}_{this.ServiceID}_PROTOCOL";
            }
        }
        public DMLProtocolInformation Information { get; private set; }
        public Dictionary<byte, DMLRecord> Records { get; private set; }
        public string Name { get { return this.Information?.Name; } }

        /// <summary>
        /// Sets this object's data from an XML file path.
        /// </summary>
        /// <param name="recordFilePath">The local file path to the DML protocol XML file.</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public void FromXMLTemplate(string recordFilePath)
        {
            // Read file contents as XML from the parameter.
            XmlDocument xmlDoc;
            if (!TryGetProtocolXml(recordFilePath, out xmlDoc))
                throw new ArgumentException($"No xml file found at \"{recordFilePath}\"!");

            // Attempt to set the DMLProtocolInformation for this Protocol.
            if (TryGetProtocolInformation(xmlDoc, out var Information))
            {
                this.Information = Information;
                Log.Verbose($"DMLProtocol \"{this.Name}\" set Informationrmation.");
            }
            else throw new ArgumentException($"DMLProtocolInformation could not be set for given path \"{recordFilePath}\"!");

            // Attempt to set each DMLRecord for this protocol using the given XML document.
            if (TryGetProtocolRecords(xmlDoc, out var records))
            {
                this.Records = records;
                Log.Verbose($"DMLProtocol \"{this.Name}\" set all records.");
            }
            else throw new Exception($"DMLProtocol \"{this.Name}\" could not set records.");
        }

        /// <summary>
        /// Returns a DML Record from a record, and fill the elements data with a byte array. Useful for creating DML records from network packets.
        /// </summary>
        /// <param name="ID">The ID of the DML record.</param>
        /// <param name="rawBytes">The raw byte array to parse as the DML elements of the record.</param>
        /// <returns>The DML record, with all elements filled out.</returns>
        public DMLRecord CreateDMLRecordFromBinary(byte ID, byte[] rawBytes)
        {
            if (!TryCloneRecordTemplate(ID, out DMLRecord record))
                throw new ArgumentException($"Could not find a DML record with id [{ID}]!");

            record.FromBinary(rawBytes);

            return record;
        }

        /// <summary>
        /// Attempts to clone a record by ID.
        /// </summary>
        /// <param name="ID">The ID of the record to search for.</param>
        /// <param name="record">The outgoing record, if one exists.</param>
        /// <returns>True, if a record is found. False otherwise.</returns>gh
        public bool TryCloneRecordTemplate(byte ID, out DMLRecord record)
        {
            record = default;
            if (this.Records.TryGetValue(ID, out var val))
            {
                record = (DMLRecord)val.Clone();
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Attempts to parse the record data of a *Messages.xml file from a path.
        /// </summary>
        /// <param name="path">The path to the XML file.</param>
        /// <param name="document">The returning XMLDocument object.</param>
        /// <returns>True, if succeeded. False otherwise.</returns>
        private bool TryGetProtocolXml(string path, out XmlDocument document)
        {
            document = default;
            if (!File.Exists(path)) return false;

            // Log
            Log.Debug($"Attempting to get XML data from \"{path}\" ... ");

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(path);

                document = xmlDoc;
            }
            catch (Exception ex)
            {
                Log.Fatal($"Failed to parse XML data for \"{path}\" | Exception: {ex.Message}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Attempts to parse the _ProtocolInformation RECORD from the given XMLDocument.
        /// </summary>
        /// <param name="xmlDoc">The successfully parsed XMLDocument.</param>
        /// <returns>True on success. False otherwise.</returns>
        private bool TryGetProtocolInformation(XmlDocument xmlDoc, out DMLProtocolInformation Information)
        {
            Information = default;

            // Create ProtocolInformation object based off the Informationrmation from the _ProtocolInformation node.
            // These fields can be converted ahead of time, since the ProtocolInformation field data types are not unique.
            try
            {
                byte serviceID = Convert.ToByte(xmlDoc.DocumentElement.SelectSingleNode("//_ProtocolInformation/RECORD/ServiceID").InnerText);
                string protocolType = xmlDoc.DocumentElement.SelectSingleNode("//_ProtocolInformation/RECORD/ProtocolType").InnerText;
                Int32 protocolVersion = Convert.ToInt32(xmlDoc.DocumentElement.SelectSingleNode("//_ProtocolInformation/RECORD/ProtocolVersion").InnerText);
                string protocolDescription = xmlDoc.DocumentElement.SelectSingleNode("//_ProtocolInformation/RECORD/ProtocolDescription").InnerText;

                Information = new DMLProtocolInformation(serviceID, protocolType, protocolVersion, protocolDescription);
            }
            catch (Exception ex)
            {
                Log.Fatal($"Failed to parse DMLProtocolInformation from XML. | Exception: {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to parse each DML record from the given XMLDocument.
        /// </summary>
        /// <param name="xmlDoc">The XML document.</param>
        /// <param name="records">The returning array of DMLRecord objects, parsed from the given XML document.</param>
        /// <returns>True, if the DMLRecords are found and successfully parsed. False otherwise.</returns>
        private bool TryGetProtocolRecords(XmlDocument xmlDoc, out Dictionary<byte, DMLRecord> records)
        {
            records = new Dictionary<byte, DMLRecord>();

            // The first node is the XML version node.
            XmlNode messagesNode = xmlDoc.ChildNodes[1];
            XmlNodeList recordsList = messagesNode.ChildNodes;

            // Some protocols don't list their records with a message ID.
            // If this is the case, the message ID is the index of which the messages appear ordinally.
            XmlNode[] recordsSorted = SortXmlMessagesOrdinally(recordsList);

            if (recordsSorted.Count() <= 0) return false;

            try
            {
                foreach (XmlNode node in recordsSorted)
                {
                    // Skip the _ProtocolInformation & comments.
                    if (node.Name == "_ProtocolInformation" 
                        || node.NodeType == XmlNodeType.Comment) continue;

                    // The Informationrmation is stored in a nested node labeled "RECORD".
                    XmlNode recordRaw = node.ChildNodes[0];
                    
                    // Create new DMLRecord object and set all it's data.
                    DMLRecord dmlRecord = new DMLRecord();
                    bool dmlRecordCreationResult = dmlRecord.FromXml(recordRaw);

                    // Get the protocol ID.
                    var index = (byte)(Array.IndexOf(recordsSorted, node));

                    // If it succeeded in creating, add it to the records list.
                    // If we couldn't set the DMLRecord data, return false. No need to continue.
                    if (dmlRecordCreationResult) records.Add(index, dmlRecord);
                    else return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Sort a protocol's child RECORDs ordinally.
        /// </summary>
        /// <param name="messagesChildrenList"></param>
        /// <returns>An array of ordinally sorted XML nodes.</returns>
        private XmlNode[] SortXmlMessagesOrdinally(XmlNodeList messagesChildrenList)
        {
            /*
             * This was a pita for me earlier, so I'm going to document it here:
             * <MSG_PING>                                                 <---- THIS IS WHAT TO SORT BY.
                 <RECORD>
                   <_MsgType TYPE="UBYT" NOXFER="TRUE">1</_MsgType>  
                   <_MsgName TYPE="STR" NOXFER="TRUE">MSG_PING</_MsgName> <---- THIS IS NOT THE SORT.
                   <_MsgDescription TYPE="STR" NOXFER="TRUE">PING request.</_MsgDescription>
                   <_MsgHandler TYPE="STR" NOXFER="TRUE">MSG_Ping</_MsgHandler>
                   <_MsgAccessLvl TYPE="UBYT" NOXFER="TRUE">0</_MsgAccessLvl>
                 </RECORD>
               </MSG_PING>
            */

            // Sorting using this method will always leve _ProtocolInformation as the very last element.
            // Create an empty array to inevitably fill with sorted messages.
            XmlNode[] sortedNodes = new XmlNode[messagesChildrenList.Count];

            // Copy everything to the new array.
            // Array.Copy does NOT work here, as the XmlNode object is only accessable via the iterator.
            // @todo: Looking for a better fix.
            for (int i = 0; i < messagesChildrenList.Count; i++)
            {
                sortedNodes[i] = messagesChildrenList[i];
            }

            Array.Sort(sortedNodes, (x, y) => String.CompareOrdinal(x.Name, y.Name));
            return sortedNodes;
        }

        public void DisposeRecords()
        {
            if (this.Records.Count <= 0) return;

            foreach (DMLRecord record in this.Records.Values)
            {
                record.Dispose();
            }
        }

    }
}
