using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Dynamic;
using Imlight.Common.Logger;

namespace Imlight.Engine
{
    internal class DynamicProtocol
    {

        internal class DynamicProtocolInformation
        {

            internal readonly byte ServiceID;
            internal readonly string ProtocolType;
            internal readonly Int32 ProtocolVersion;
            internal readonly string ProtocolDescription;

            internal readonly string Name;

            public DynamicProtocolInformation(byte serviceID, string protocolType, int protocolVersion, string protocolDescription)
            {
                ServiceID = serviceID;
                ProtocolType = protocolType;
                ProtocolVersion = protocolVersion;
                ProtocolDescription = protocolDescription;

                // Create a formmated name for logging.
                Name = $"{this.ProtocolType}_{this.ServiceID}_PROTOCOL";
            }

        }
        internal DynamicProtocolInformation Information { get; private set; }
        internal List<dynamic> Records { get; } = new List<dynamic>();
        internal string Name { get { return this.Information?.Name; } }

        private readonly string _recordsFilePath = $"{Directory.GetCurrentDirectory()}/Records";

        // ctor
        public DynamicProtocol(string path)
        {
            // ###################################################################################################
            // The methods here *must* throw exceptions. Simply logging the errors here will not be effecient,
            // and potientally massive data losses could happen if a protocol is not successfully loaded.
            // DO NOT KEEP RUNNING ON THESE EXCEPTIONS. IT IS VITAL.
            // ###################################################################################################

            // Read file contents from given path as XML.
            XmlDocument xmlDoc;
            if (!TryGetProtocolXml(path, out xmlDoc))
                throw new ArgumentException($"No xml file found at \"{path}\"!");

            // For formatted logging.
            string protocolName;

            // Attempt to set the DynamicProtocolInformation for this Protocol.
            if (TryGetProtocolInformation(xmlDoc, out var Information))
            {
                this.Information = Information;
                protocolName = Information.Name;
                Log.Debug($"Successfully set DynamicProtocolInformation for \"{protocolName}\".");
            }
            else
            {
                throw new ArgumentException($"DynamicProtocolInformation could not be set for given path \"{path}\"!");
            }

            // Attempt to set all the RECORDS for this Protocol.
            if (TrySetProtocolRecords(xmlDoc))
            {
                Log.Debug($"Successfully set each RECORD for \"{protocolName}\".");
            }
            else
            {
                throw new ArgumentException($"DynamicProtocol by \"{protocolName}\" unsuccessfully set it's own records.");
            }
        }

        /// <summary>
        /// Attempts to parse XML data from a path.
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
        private bool TryGetProtocolInformation(XmlDocument xmlDoc, out DynamicProtocolInformation Information)
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

                Information = new DynamicProtocolInformation(serviceID, protocolType, protocolVersion, protocolDescription);
            }
            catch (Exception ex)
            {
                Log.Fatal($"Failed to parse DynamicProtocolInformation from XML. | Exception: {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to parse the RECORDS from the given XMLDocument.
        /// </summary>
        /// <param name="xmlDoc">The successfully parsed XMLDocument.</param>
        /// <returns>True on success. False otherwise.</returns>
        private bool TrySetProtocolRecords(XmlDocument xmlDoc) 
        {
            // The second node will always be the messages node.
            XmlNode messagesNode = xmlDoc.ChildNodes[1];
            XmlNodeList recordsList = messagesNode.ChildNodes;

            /* Some protocols don't carry an ID. In which case, the ID is
             * the order in which the messages appear ordinally.
             */
            XmlNode[] recordsSorted = SortXmlMessagesOrdinally(recordsList);

            try
            {
                if (recordsSorted.Count() <= 0)
                {
                    Log.Fatal($"{this.Information.Name} somehow contained no RECORDs! This should not happen.");
                    return false;
                }

                for (int i = 0; i < recordsSorted.Count(); i++)
                {
                    XmlNode node = recordsSorted[i];

                    // Skip the _ProtocolInformation RECORD.
                    if (node.Name == "_ProtocolInformation") continue;
                    else if (node.NodeType == XmlNodeType.Comment) continue;

                    // The meat of a node is inside a nested node named RECORD.
                    XmlNode record = node.ChildNodes[0];

                    dynamic dyn = new ExpandoObject();

                    // Set the ServiceID for the RECORD.
                    // The records are already sorted at this point, so we can just use the iterator.
                    dyn._MsgID = i + 1;

                    // Add data to object by iterating over the record's elements.
                    foreach (var element in record.ChildNodes
                        .OfType<XmlElement>())
                    {
                        ((IDictionary<string, object>)dyn)[element.Name] = element.InnerText;
                    }
                    
                    this.Records.Add(dyn);
                }
            }
            catch (Exception ex)
            {
                Log.Fatal($"Could not successfully parse RECORDs. | Exception: {ex.Message}");
                return false;
            }

            return true;
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

    }
}
