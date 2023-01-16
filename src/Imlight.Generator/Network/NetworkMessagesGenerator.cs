using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Imlight.Internals;
using Imlight.Common;

namespace Imlight.Generator.Network
{
    internal static class NetworkMessagesGenerator
    {

        private static readonly string _outputPath = $"{Directory.GetCurrentDirectory()}/output";
        private static readonly string _inputFolderPath = $"{Directory.GetCurrentDirectory()}/Input/Records/";
        private static readonly string[] _allMessageFileNames = new string[]
        {
            "AISClientMessages.xml",
            "BaseMessages.xml",
            "CatchAKeyMessages.xml",
            "ChooChooZooMessages.xml",
            "ConcentrationMessages.xml",
            "DoodleDougMessages.xml",
            "Dueling_DiegoMessages.xml",
            "ExtendedBaseMessages.xml",
            "GameMessages.xml",
            "HotShotsMessages.xml",
            "HousingMessages.xml",
            "LoginMessages.xml",
            "MoveBehaviorMessages.xml",
            "PatchMessages.xml",
            "PetMessages.xml",
            "PhysicsBehaviorMessages.xml",
            "PotionMotionMessages.xml",
            "QuestMessages.xml",
            "ScriptDebuggerMessages.xml",
            "ShockALockMessages.xml",
            "SkullRidersMessages.xml",
            "SoblocksMessages.xml",
            "TestManagerMessages.xml",
            "WizardMessages.xml",
            "WizCombatMessages.xml"
        };
        private static readonly Dictionary<string, (Type, DMLType)> _internalTypeTranslationDict = new()
        {
            { "BYT",    (typeof(sbyte),   DMLType.BYT)   },
            { "UBYT",   (typeof(byte),    DMLType.UBYT)  },
            { "UBYTE",  (typeof(byte),    DMLType.UBYT)  },     // Appears exactly 1 time. 
            { "USHRT",  (typeof(ushort),  DMLType.USHRT) },
            { "USHORT", (typeof(ushort),  DMLType.USHRT) },     // Appears exactly 1 time.
            { "INT",    (typeof(int),     DMLType.INT)   },
            { "UINT",   (typeof(uint),    DMLType.UINT)  },
            { "STR",    (typeof(string),  DMLType.STR)   },
            { "WSTR",   (typeof(string),  DMLType.WSTR)  },
            { "FLT",    (typeof(float),   DMLType.FLT)   },
            { "DBL",    (typeof(double),  DMLType.DBL)   },
            { "GID",    (typeof(ulong),   DMLType.GID)   },
        };

        internal static void Generate(GeneratorOptions generatorOptions)
        {
            Log.Logger.Information("Starting Network message generation..");

            if (!IsRecordsDirectoryValid())
            {
                Log.Logger.Fatal($"NetworkMessages could not be generated! The directory [{_inputFolderPath}] either doesn't exist or is invalid!");
                return;
            }

            for (int i = 0; i < _allMessageFileNames.Length; i++)
            {
                string path = $"{_inputFolderPath}/{_allMessageFileNames[i]}";

                // The filename is the end of the path after the last '/', and it's extension trimmed off.
                string fileName = path.Split('/').Last().Split('.')[0];

                Log.Logger.Debug($"Starting work on {fileName}..");

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(path);

                CodeCompileUnit compileUnit = CreateProtocolClassFromXml(xmlDoc, generatorOptions, out var protocolName);
                CodeDomProvider domProvider = CodeDomProvider.CreateProvider("CSharp");
                string outputPath = $"{GetOrCreateOutputDirectory()}/{protocolName}.cs";
                CodeGeneratorOptions options = new CodeGeneratorOptions()
                {
                    BracingStyle = generatorOptions.CurlyBraceNewline ? "C" : "Block",
                    IndentString = generatorOptions.IndentString,
                    ElseOnClosing = false,
                };
                using (StreamWriter writer = new StreamWriter(outputPath))
                {
                    domProvider.GenerateCodeFromCompileUnit(compileUnit, writer, options);
                }

                if (generatorOptions.ClearEmptyLines)
                    File.WriteAllLines(outputPath, File.ReadAllLines(outputPath).Where(l => !string.IsNullOrWhiteSpace(l)));

                Log.Logger.Information($"Class {protocolName}.cs generated!");
            }

            Log.Logger.Information("All network protocol classes have been generated. ");
            Log.Logger.Warning("Despite generation being complete, it's highly recommended as a developer to validate each file personally " +
                "before committing them to a project.");
        }

        private static CodeCompileUnit CreateProtocolClassFromXml(XmlDocument xmlDoc,
                                                                  GeneratorOptions generatorOptions,
                                                                  out string protocolName)
        {
            if (xmlDoc is null) throw new ArgumentNullException(nameof(xmlDoc));
            protocolName = default;

            // First we need to gather Informationrmation from the XMLDocument.
            byte serviceID = Convert.ToByte(xmlDoc.DocumentElement.SelectSingleNode("//_ProtocolInfo/RECORD/ServiceID").InnerText);
            string protocolType = xmlDoc.DocumentElement.SelectSingleNode("//_ProtocolInfo/RECORD/ProtocolType").InnerText;
            Int32 protocolVersion = Convert.ToInt32(xmlDoc.DocumentElement.SelectSingleNode("//_ProtocolInfo/RECORD/ProtocolVersion").InnerText);
            string protocolDescription = xmlDoc.DocumentElement.SelectSingleNode("//_ProtocolInfo/RECORD/ProtocolDescription").InnerText;

            // I cannot believe I even have to type this, but for whatever reason, Kingsisle put WizardCombat messages into the DOODLEDOUG protocol.
            // This is the fallback to rename that protocol to WIZARDCOMBAT_MESSAGES.
            if (serviceID == 51) protocolType = "WIZARDCOMBAT_MESSAGES";

            // Another fallback. Catch a key minigame protocol *should* be MG9 but is instead MG3.
            // These developers get paid 6 figures salaries, by the way.
            if (serviceID == 54)
            {
                protocolType = "MG9_MESSAGES";
                protocolDescription = "Messages for MG9 MinigameWindow Mini-Game";
            }

            // Create a nicely formatted name that will serve as the name of the generated class.
            protocolName = $"{protocolType.ToUpper()}_{serviceID}_PROTOCOL";

            // Example calling convention:
            // `Imlight.Internals.DML.GAME_5_PROTOCOL.MSG_ATTACH`

            // Create a basic compilation unit. It will contain our namespace, and a single base class.
            // This base class will be the protocol.
            CodeCompileUnit compileUnit = new();
            CodeNamespace codeNamespace = new("Imlight.Internals.DML");
            CodeTypeDeclaration codeClass = new(protocolName);
            codeClass.IsClass = true;
            codeClass.TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit;
            codeClass.Attributes = MemberAttributes.Public | MemberAttributes.Static;
            // Add interface
            CodeTypeReference codeClassReference = new("INetworkProtocol");
            codeClass.BaseTypes.Add(codeClassReference);
            // Add imports
            CodeNamespaceImport codeClassSystemImport = new("System");
            codeNamespace.Imports.Add(codeClassSystemImport);

            // Create properties. In turn this also implements the INetworkProtocol interface.
            // The IndentedTextWriter does not work on generic property snippets, so the tabs must be literal.
            var serviceIdProp           = CreateGenericPropertrySnippet($"        public byte ServiceID {{ get; }} = {serviceID};");
            var protocolTypeProp        = CreateGenericPropertrySnippet($"        public string ProtocolType {{ get; }} = \"{protocolType}\";");
            var protocolVersionProp     = CreateGenericPropertrySnippet($"        public Int32 ProtocolVersion {{ get; }} = {protocolVersion};");
            var protocolDescriptionProp = CreateGenericPropertrySnippet($"        public string ProtocolDescription {{ get; }} = \"{protocolDescription}\";");
            // Add each member to the class.
            codeClass.Members.Add(serviceIdProp);
            codeClass.Members.Add(protocolTypeProp);
            codeClass.Members.Add(protocolVersionProp);
            codeClass.Members.Add(protocolDescriptionProp);
            // This is an empty member and serves as a whitespace buffer between the members of the protocol and the sub classes.
            codeClass.Members.Add(CreateGenericPropertrySnippet("        // == RECORDS =="));

            Log.Logger.Debug($"Created empty protocol class {codeClass.Name}. Properties set, moving onto record sub classes.");

            // Now, we're going to iterate through each record in the Xml and create a class for it.
            // These record classes will be added as subclasses to the protocol class.
            XmlNode messagesNode = xmlDoc.ChildNodes[1]; // Skip the first node, as that's the Xml version.
            XmlNodeList recordsList = messagesNode.ChildNodes;

            // Some protocol records do not carry a `_MsgOrder` property. If this is the case, the ID of the message
            // will instead be the index of the message as it appears ordinally sorted.
            // First, check the first record and see if it contains a metadata field for ordering. If it does, we don't need to bother ordering it ourselves.
            XmlNode[] properUsingList = DoesProtocolNeedOrdering(recordsList) 
                ? SortXmlMessagesOrdinally(recordsList) 
                : recordsList.Cast<XmlNode>().ToArray();

            // Finally, add the record sub-classes to the protocol class.
            AddRecordClassesToProtocol(ref codeClass, properUsingList, generatorOptions);

            // Add together.
            compileUnit.Namespaces.Add(codeNamespace);
            codeNamespace.Types.Add(codeClass);

            return compileUnit;
        }

        private static void AddRecordClassesToProtocol(ref CodeTypeDeclaration r_protocolClass,
                                                       XmlNode[] recordsSortedList,
                                                       GeneratorOptions generatorOptions)
        {
            // There are some duplicate records. It memory, only the first instance is regarded. The others simply arent loaded.
            HashSet<string> seenValues = new HashSet<string>();
            // To make up for the discrepency of a missing record for the generated message ID, this variable exists:
            int duplicateRecordCount = 0;

            // Save what sub classes we create so we can make a dispatcher later on.
            Dictionary<byte, string> createdClasses = new Dictionary<byte, string>();

            // Iterate through each record Xml.
            foreach (XmlNode recordXmlBase in recordsSortedList)
            {
                if (recordXmlBase.Name == "_ProtocolInfo" || recordXmlBase.NodeType == XmlNodeType.Comment) continue;
                if (!seenValues.Add(recordXmlBase.Name))
                {
                    duplicateRecordCount++;
                    continue;
                }

                Log.Logger.Debug($"Starting class creation of record {recordXmlBase.Name}..");

                // The Informationrmation is stored in a nested node labeled "RECORD".
                var recordXml = recordXmlBase.ChildNodes[0];

                // Create a class for this record.
                CodeTypeDeclaration codeRecordClass = new(recordXmlBase.Name);
                codeRecordClass.IsClass = true;
                codeRecordClass.TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;
                // Add interface.
                CodeTypeReference codeRecordInterfaceReference = new(nameof(INetworkMessage));
                codeRecordClass.BaseTypes.Add(codeRecordInterfaceReference);

                // If `_MsgType` or `_MsgAccessLvl` are not found, we can create them ourselves.
                bool msgTypeExists = false, msgLvlExists = false;

                // Finally, start iterating through the Xml elements, and create properties for each of them.
                int index = 0;
                foreach (var element in recordXml.ChildNodes.OfType<XmlElement>())
                {
                    // Look for potential record metadata nodes.
                    if (element.Name.StartsWith('_'))
                    {
                        switch (element.Name)
                        {
                            case "_MsgName":
                                if (!generatorOptions.Verbose) break;
                                var nameProp = CreateGenericPropertrySnippet($"            public const string Name = \"{element.InnerText}\";");
                                codeRecordClass.Members.Add(nameProp);
                                break;
                            case "_MsgDescription":
                                if (!generatorOptions.Verbose) break;
                                var descProp = CreateGenericPropertrySnippet($"            public const string Description = \"{element.InnerText}\";");
                                codeRecordClass.Members.Add(descProp);
                                break;
                            case "_MsgHandler":
                                if (!generatorOptions.Verbose) break;
                                var handProp = CreateGenericPropertrySnippet($"            public const string Handler = \"{element.InnerText}\";");
                                codeRecordClass.Members.Add(handProp);
                                break;
                            case "_MsgAccessLvl":
                                if (!generatorOptions.Verbose) break;
                                var levlProp = CreateGenericPropertrySnippet($"            public const byte AccessLevel = {element.InnerText};");
                                codeRecordClass.Members.Add(levlProp);
                                msgLvlExists = true;
                                break;
                            case "_MsgOrder":
                            case "_MsgType":
                                index = Int32.Parse(element.InnerText);
                                var typeProp = CreateGenericPropertrySnippet($"            public byte MessageOrder {{ get; }} = {element.InnerText};");
                                codeRecordClass.Members.Add(typeProp);
                                msgTypeExists = true;
                                break;
                        }
                        continue;
                    }

                    // Otherwise, create a custom field.
                    string rawType = GetDataTypeFromXmlElement(element);
                    if (!_internalTypeTranslationDict.TryGetValue(rawType, out var type))
                        throw new Exception($"Could not translate internal type [{rawType}]!");

                    string typeWithoutNamespace = type.Item1.ToString().Split('.').Last();
                    var propertySnippet = CreateGenericPropertrySnippet($"            [DMLElement({nameof(DMLType)}.{type.Item2})] public {typeWithoutNamespace} {element.Name};");
                    codeRecordClass.Members.Add(propertySnippet);
                }

                // Not all protocols are made equal. Some records will be missing metadata fields.
                // If any of our metadata nodes are still left empty:
                if (!msgTypeExists)
                {
                    index = Array.IndexOf(recordsSortedList, recordXmlBase) + 1 - duplicateRecordCount;
                    var typeProp = CreateGenericPropertrySnippet($"            public byte MessageOrder {{ get; }} = {index};");
                    codeRecordClass.Members.Add(typeProp);
                }
                if (generatorOptions.Verbose && !msgLvlExists)
                {
                    // If the access level metadata node doesn't exist, it's safe to assume that a client of any authority can call.
                    var levlProp = CreateGenericPropertrySnippet("            public const byte AccessLevel = 0;");
                    codeRecordClass.Members.Add(levlProp);
                }

                createdClasses.Add((byte)index, recordXmlBase.Name);

                // Finally, add the new record class as a subclass to the referenced protocol class.
                r_protocolClass.Members.Add(codeRecordClass);
            }

            // Create dispatcher method.
            CreateDispatcherMethod(ref r_protocolClass, ref createdClasses, generatorOptions);
        }

        private static void CreateDispatcherMethod(ref CodeTypeDeclaration r_protocolClass, ref Dictionary<byte, string> r_classes, GeneratorOptions options)
        {
            /*
             * End goal is to create a Dispatch method that looks something along the lines of this.
             * 
             * public static INetworkRecord Dispatch(byte messageId)
               {
	                switch (messageId)
                    {
                        case 1: return new MSG_AISMESSAGE();
     	                default: return null;
                    }
                }
            */

            Log.Logger.Information("Starting write on Dispatcher method..");

            // CodeDom doesn't support switch/case. It needs to be written manually.
            // Create the IndentedWriter and set options.
            StringWriter sw = new StringWriter();
            IndentedTextWriter tw = new IndentedTextWriter(sw);

            // By the time the dispatcher is created, we're already 2 tabs in.
            tw.Indent = 2;

            // Start by writing the method itself
            // I do not know why, but the indentation does not apply to the first line. The tabs must be literal.
            if (options.CurlyBraceNewline)
            {
                tw.WriteLine($"        public {nameof(INetworkMessage)} Dispatch(byte id)");
                tw.WriteLine("{");
                tw.Indent++;
                tw.WriteLine("switch (id)");
                tw.WriteLine("{");
                tw.Indent++;
            }
            else
            {
                tw.WriteLine($"        public {nameof(INetworkMessage)} Dispatch(byte id) ");
                tw.Indent++;
                tw.WriteLine("switch (id) {");
                tw.Indent++;
            }
            
            // Iterate through r_classes and create a case statements from the options.
            foreach (KeyValuePair<byte, string> entry in r_classes)
            {
                tw.WriteLine($"case ({entry.Key}): return new {entry.Value}();");
            }

            tw.WriteLine("default: throw new InternalException($\"No message was found at ID {id} for this protocol!\");");
            tw.Indent--;
            tw.WriteLine("}"); // Switch case closing brace.
            tw.Indent--;
            tw.WriteLine("}"); // Method closing brace.

            var dispatcherProp = CreateGenericPropertrySnippet(sw.ToString());
            r_protocolClass.Members.Add(dispatcherProp);

            Log.Logger.Information("Dispatcher method written!");

            sw.Dispose();
            tw.Dispose();
        }

        private static bool IsRecordsDirectoryValid() 
        {
            // Validate that the directory exists, and that each *Messages.xml file is accounted for.
            // @TODO: The documentation should contain a quick start article on how developers can aquire these files.
            if (!Directory.Exists(_inputFolderPath)) return false;

            for (int i = 0; i < _allMessageFileNames.Length; i++)
            {
                string file = _allMessageFileNames[i];
                if (!File.Exists($"{_inputFolderPath}/{file}"))
                {
                    Log.Logger.Fatal($"Xml file {file} doesn't exist in records directory!");
                    return false;
                }
            }

            return true;
        }

        private static string GetOrCreateOutputDirectory()
        {
            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);

            return _outputPath;
        }

        private static bool DoesProtocolNeedOrdering(XmlNodeList protocolRecordsNode)
        {
            if (protocolRecordsNode is null) throw new ArgumentNullException(nameof(protocolRecordsNode));

            // Skip element 0, as that's the _ProtocolInformation.
            var nodeToSearchBase = protocolRecordsNode[1];
            var nodeToSearchRecord = nodeToSearchBase.ChildNodes[0];
            for (int i = 0; i < nodeToSearchRecord.ChildNodes.Count; i++)
            {
                var node = nodeToSearchRecord.ChildNodes[i];
                if (node.Name == "_MsgType" || node.Name == "_MsgOrder")
                {
                    return false;
                }
            }

            return true;
        }

        private static XmlNode[] SortXmlMessagesOrdinally(XmlNodeList messagesChildrenList)
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

        private static string GetDataTypeFromXmlElement(XmlElement element)
        {
            string dataTypeString = element.GetAttribute("TYPE");

            // Unfortunately this has to exist because the developers are inconsistent.
            // Failsafe conditions:
            if (dataTypeString == "")
            {
                string mistypedDataTypeString = element.GetAttribute("TPYE");
                if (mistypedDataTypeString == "")
                {
                    if (element.Name == "GlobalID") dataTypeString = "GID";
                }
                else dataTypeString = mistypedDataTypeString;
            }

            return dataTypeString;
        }

        private static CodeSnippetTypeMember CreateGenericPropertrySnippet(string snippet) 
            => new CodeSnippetTypeMember() { Text = snippet };

    }
}
