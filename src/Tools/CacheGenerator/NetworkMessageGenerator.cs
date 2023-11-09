using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Xml;
using Serilog;
using Serilog.Core;
using Imlight.Common.MessageLayer;
using Imlight.Common.IO;

namespace CacheGenerator;

public static class NetworkMessageGenerator {
    private const string CacheNamespace = "Common.Caches";
    private const string UnknownProtocolType = "UNKNOWN_PROTOCOL_TYPE";
    private const string MetadataNodePrefix = "_";
    private const string MetadataDescriptionName = "_MsgDescription";
    private const string MetadataAccessLevelName = "_MsgAccessLvl";
    private const string MetadataOrderName = "_MsgOrder";
    private const string MetadataTypeName = "_MsgType";
    private const string ProtocolTabLength = "\t\t";
    private const string RecordTabLength = "\t\t\t";
    private const string NetworkProtocolInterfaceName = nameof(MessageProtocol);
    private const TypeAttributes ProtocolClassTypeAttributes
        = TypeAttributes.Public | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed;
    private const TypeAttributes RecordClassTypeAttributes
        = TypeAttributes.Public | TypeAttributes.Sealed;

    private static readonly string[] CodeClassImports = {
        "System",
        "Common.IO",
        "Common.MessageLayer",
    };
    private static readonly Dictionary<string, Type> InternalTypeTranslationDict = new() {
        { "BYT",    (typeof(sbyte))          },
        { "BOOL",   (typeof(bool))           },
        { "UBYT",   (typeof(byte))           },
        { "UBYTE",  (typeof(byte))           },     // Appears exactly 1 time.
        { "USHRT",  (typeof(ushort))         },
        { "USHORT", (typeof(ushort))         },     // Appears exactly 1 time.
        { "INT",    (typeof(int))            },
        { "UINT",   (typeof(uint))           },
        { "STR",    (typeof(ByteString))     },
        { "WSTR",   (typeof(WideByteString)) },
        { "FLT",    (typeof(float))          },
        { "DBL",    (typeof(double))         },
        { "GID",    (typeof(ulong))          },
    };
    private static Logger Log { get; } = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateLogger();

    /// <summary>
    /// Generates C# classes from the provided XML document.
    /// </summary>
    /// <param name="xmlDocuments">The XML document for the protocol.</param>
    /// <param name="path"></param>
    /// <returns>True, if the operation succeeded. Otherwise, false.</returns>
    public static bool GenerateCSharpFromXmlProtocols(XmlDocument[] xmlDocuments, string path) {
        if (xmlDocuments is null) {
            throw new NullReferenceException(nameof(xmlDocuments));
        }

        var compileUnit = CreateCodeCompileUnit(xmlDocuments);
        var domProvider = CodeDomProvider.CreateProvider("CSharp");
        var compileOptions = new CodeGeneratorOptions() {
            BracingStyle = "C",
            IndentString = "\t",
            ElseOnClosing = true,
            BlankLinesBetweenMembers = false,
            VerbatimOrder = false
        };

        // Write to file.
        var tw = new IndentedTextWriter(new StreamWriter(path, false), "\t");
        domProvider.GenerateCodeFromCompileUnit(compileUnit, tw, compileOptions);
        tw.Close();

        return true;
    }

    private static CodeCompileUnit CreateCodeCompileUnit(IEnumerable<XmlDocument> xmlDocuments) {
        // Start working with CodeDom.
        var compileUnit = new CodeCompileUnit();
        var codeNamespace = new CodeNamespace(CacheNamespace);

        // Add imports to the namespace.
        foreach (var import in CodeClassImports) {
            var importNamespace = new CodeNamespaceImport(import);
            codeNamespace.Imports.Add(importNamespace);
        }

        // Iterate through each protocol and generate a class for it.
        foreach (var xmlDoc in xmlDocuments) {
            var protocolHeader = new ProtocolHeader(xmlDoc);
            var codeClass = CreateProtocolTypeDeclaration(protocolHeader);

            // Add the sub-records to the class.
            var xmlRecords = GetXmlRecordsFromProtocol(xmlDoc);
            var createdClasses = AddSubRecordsToProtocol(ref codeClass, xmlRecords, protocolHeader.ServiceId);

            codeNamespace.Types.Add(codeClass);
        }

        compileUnit.Namespaces.Add(codeNamespace);
        return compileUnit;
    }

    private static Dictionary<byte, string> AddSubRecordsToProtocol(ref CodeTypeDeclaration protocolClass,
                                                                    IEnumerable<XmlNode> xmlRecords,
                                                                    byte serviceId) {
        // There are sometimes duplicate records. Only the first instance of a record is regarded.
        var seenRecords = new HashSet<string>();
        var duplicateRecordCount = 0;
        var createdClasses = new Dictionary<byte, string>();

        var xmlBases = xmlRecords as XmlNode[] ?? xmlRecords.ToArray();
        foreach (var xmlBase in xmlBases) {
            // Skip metadata, comments, and duplicate records.
            if (xmlBase.Name.StartsWith(MetadataNodePrefix) || xmlBase.NodeType == XmlNodeType.Comment) {
                continue;
            }
            if (!seenRecords.Add(xmlBase.Name)) {
                duplicateRecordCount++;
                continue;
            }

            var msgOrderFallback = Array.IndexOf(xmlBases, xmlBase) + 1 - duplicateRecordCount;
            var codeClass = CreateRecordTypeDeclaration(xmlBase, serviceId, (byte) msgOrderFallback);

            protocolClass.Members.Add(codeClass);

            // Check to see if the message has a _MsgOrder or _MsgType property. If it does, we'll use that as the key.
            // Otherwise, we'll use the index of the message as it appears in the protocol.
            // Delve in 1 layer to get into the RECORD node.
            if (xmlBase.ChildNodes[0]!
                .ChildNodes
                .OfType<XmlElement>()
                .Any(x => x.Name is MetadataOrderName or MetadataTypeName)) {
                var msgOrder = xmlBase.ChildNodes[0]!
                    .ChildNodes
                    .OfType<XmlElement>()
                    .First(x => x.Name is MetadataOrderName or MetadataTypeName);
                var msgOrderValue = byte.Parse(msgOrder.InnerText);
                createdClasses.Add(msgOrderValue, codeClass.Name);
                continue;
            }

            // If we're here, the message doesn't have a _MsgOrder property.
            // We'll use the index of the message as it appears in the protocol.
            createdClasses.Add(unchecked((byte) msgOrderFallback), codeClass.Name);
        }

        return createdClasses;
    }

    private static CodeTypeDeclaration CreateProtocolTypeDeclaration(ProtocolHeader protocolHeader) {
        // Create a fancy name for the protocol.
        var protocolName = $"{protocolHeader.ProtocolType ?? UnknownProtocolType}_{protocolHeader.ServiceId}_PROTOCOL";

        var codeClass = new CodeTypeDeclaration(protocolName) {
            IsClass = true,
            TypeAttributes = ProtocolClassTypeAttributes,
            Attributes = MemberAttributes.Static
        };

        // Add our interface.
        ImplementNetworkProtocolInterfaceProperties(ref codeClass, protocolHeader);

        return codeClass;
    }

    private static CodeTypeDeclaration CreateRecordTypeDeclaration(XmlNode xmlNode,
                                                                   byte serviceId,
                                                                   byte messageOrderFallback = 0) {
        var codeClass = new CodeTypeDeclaration(xmlNode.Name) {
            TypeAttributes = ProtocolClassTypeAttributes
        };

        // Add our interface.
        ImplementNetworkMessageInterfaceProperties(ref codeClass, xmlNode, serviceId, messageOrderFallback);

        // Add the properties. Dive one layer in to get into the RECORD node.
        var xmlRecord = xmlNode.ChildNodes[0];
        for (var i = 0; i < xmlRecord!.ChildNodes.Count; i++) {
            var xmlElement = xmlRecord.ChildNodes[i] as XmlElement;
            if (xmlElement is null) {
                continue;
            }
            if (xmlElement!.Name.StartsWith(MetadataNodePrefix)) {
                continue;
            }

            var rawType = GetDataTypeFromXmlElement(xmlElement);
            if (!InternalTypeTranslationDict.TryGetValue(rawType, out var type)) {
                throw new InvalidOperationException(
                    $"Could not find type {rawType} in the type translation dictionary!");
            }

            // Create the property snippet.
            var formattedType = type.ToString().Split('.').Last();
            var formattedAttributeName = nameof(MessageElementAttribute).Replace("Attribute", "");
            var propSnippetText =
                $"{RecordTabLength}[{formattedAttributeName}(\"{rawType}\")] " +
                $"public {formattedType} {xmlElement.Name};";

            // Append a newline character, unless it's the last property.
            if (i != xmlRecord.ChildNodes.Count - 1) {
                propSnippetText += Environment.NewLine;
            }

            var propSnippet = CreateGenericPropertySnippet(propSnippetText);

            codeClass.Members.Add(propSnippet);
        }

        return codeClass;
    }

    private static void ImplementNetworkProtocolInterfaceProperties(ref CodeTypeDeclaration codeClass,
                                                                    ProtocolHeader protocolHeader) {
        var networkInterfaceRef = new CodeTypeReference(NetworkProtocolInterfaceName);
        codeClass.BaseTypes.Add(networkInterfaceRef);

        // Implement the INetworkProtocol interface.
        var interfaceProperties = typeof(MessageProtocol).GetProperties();
        for (var i = 0; i < interfaceProperties.Length; i++) {
            var property = interfaceProperties[i];
            var propertySnippetText = $"{ProtocolTabLength}public override {property.PropertyType.Name} {property.Name} {{ get; }}";

            // Check to see if the ProtocolHeader has a value for this property of the same name.
            // If it does, we'll append the text to include the value.
            var protocolHeaderProperties = typeof(ProtocolHeader).GetProperties();
            if (protocolHeaderProperties.Any(x => x.Name == property.Name)) {
                var protocolHeaderProperty = protocolHeaderProperties.First(x => x.Name == property.Name);
                var protocolHeaderValue = protocolHeaderProperty.GetValue(protocolHeader);

                // If the property is a string, we'll wrap it in quotes.
                if (property.PropertyType.Name.ToLower() == "string") {
                    propertySnippetText += $" = \"{protocolHeaderValue}\";";
                }
                else {
                    propertySnippetText += $" = {protocolHeaderValue};";
                }

                // Append newline character, unless it's the last one.
                if (i != interfaceProperties.Length - 1) {
                    propertySnippetText += Environment.NewLine;
                }
            }

            var propertySnippet = CreateGenericPropertySnippet(propertySnippetText);
            codeClass.Members.Add(propertySnippet);
        }
    }

    private static void ImplementNetworkMessageInterfaceProperties(ref CodeTypeDeclaration codeClass,
                                                                   XmlNode xmlNode,
                                                                   byte serviceId,
                                                                   byte messageOrderFallback) {
        var networkInterfaceRef = new CodeTypeReference(nameof(IMessage));
        codeClass.BaseTypes.Add(networkInterfaceRef);

        // Implement the interface properties. Go one layer down to enter the RECORD element.
        bool wroteMessageOrder = false, wroteAccessLevel = false;
        var recordNode = xmlNode.ChildNodes[0];
        foreach (var xmlElement in recordNode!.ChildNodes.OfType<XmlElement>()) {
            if (!xmlElement.Name.StartsWith(MetadataNodePrefix)) {
                continue;
            }

            var xmlInnerText = xmlElement.InnerText;
            switch (xmlElement.Name) {
                case MetadataDescriptionName:
                    var descSnippetText = xmlInnerText == ""
                        ? $"{RecordTabLength}// This message has no description.\n"
                        : $"{RecordTabLength}// {xmlInnerText.Replace("\n", $"\n// ")}\n";
                    var descProp = CreateGenericPropertySnippet(descSnippetText);
                    codeClass.Members.Add(descProp);
                    break;
                case MetadataAccessLevelName:
                    var accessLevelSnippetText = $"{RecordTabLength}public byte AccessLevel {{ get; }} = {xmlInnerText};\n";
                    var lvlProp = CreateGenericPropertySnippet(accessLevelSnippetText);
                    codeClass.Members.Add(lvlProp);
                    wroteAccessLevel = true;
                    break;
                case MetadataOrderName:
                case MetadataTypeName:
                    var orderSnippetText = $"{RecordTabLength}public byte MessageOrder {{ get; }} = {xmlInnerText};\n";
                    var orderProp = CreateGenericPropertySnippet(orderSnippetText);
                    codeClass.Members.Add(orderProp);
                    wroteMessageOrder = true;
                    break;
            }
        }

        // If we didn't write the message order, we'll use the fallback.
        if (!wroteMessageOrder) {
            var orderSnippetText = $"{RecordTabLength}public byte MessageOrder {{ get; }} = {messageOrderFallback};\n";
            var orderProp = CreateGenericPropertySnippet(orderSnippetText);
            codeClass.Members.Add(orderProp);
        }

        // If we didn't write the access level, just write 0.
        if (!wroteAccessLevel) {
            var accessLevelSnippetText = $"{RecordTabLength}public byte AccessLevel {{ get; }} = 0;\n";
            var accessLevelProp = CreateGenericPropertySnippet(accessLevelSnippetText);
            codeClass.Members.Add(accessLevelProp);
        }

        // Write the service ID.
        var serviceIdSnippetText = $"{RecordTabLength}public byte ServiceId {{ get; }} = {serviceId};";

        // Append a newline character only if the record has a field other than metadata.
        if (!recordNode.ChildNodes.OfType<XmlElement>().All(x => x.Name.StartsWith(MetadataNodePrefix))) {
            serviceIdSnippetText += Environment.NewLine;
        }

        var serviceIdProp = CreateGenericPropertySnippet(serviceIdSnippetText);
        codeClass.Members.Add(serviceIdProp);
    }

    private static XmlNode[] GetXmlRecordsFromProtocol(XmlDocument xmlDocument) {
        var messagesNode = xmlDocument.ChildNodes[1]; // Skip the first node, as that's the Xml version.
        var recordsList = messagesNode.ChildNodes;

        // Some protocol records do not carry a `_MsgOrder` property. If this is the case, the ID of the message
        // will instead be the index of the message as it appears ordinal sorted.
        // First, check the first record and see if it contains a metadata field for ordering. If it does, we don't need
        // to bother ordering it ourselves.
        var properUsingList = DoesProtocolNeedOrdering(recordsList)
            ? SortXmlRecordsOrdinal(recordsList)
            : recordsList.Cast<XmlNode>().ToArray();

        return properUsingList;
    }

    private static bool DoesProtocolNeedOrdering(XmlNodeList protocolRecordsNode) {
        if (protocolRecordsNode is null) {
            throw new ArgumentNullException(nameof(protocolRecordsNode));
        }

        // Skip element 0, as that's the _ProtocolInformation.
        var nodeToSearchBase = protocolRecordsNode[1];
        var nodeToSearchRecord = nodeToSearchBase.ChildNodes[0];
        for (int i = 0; i < nodeToSearchRecord.ChildNodes.Count; i++) {
            var node = nodeToSearchRecord.ChildNodes[i];
            if (node.Name == "_MsgType" || node.Name == "_MsgOrder") {
                return false;
            }
        }

        return true;
    }

    private static XmlNode[] SortXmlRecordsOrdinal(XmlNodeList messagesChildrenList) {
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
        for (int i = 0; i < messagesChildrenList.Count; i++) {
            sortedNodes[i] = messagesChildrenList[i];
        }

        Array.Sort(sortedNodes, (x, y) => String.CompareOrdinal(x.Name, y.Name));
        return sortedNodes;
    }

    private static string GetDataTypeFromXmlElement(XmlElement element) {
        var attributeNames = new Dictionary<string, string> {
            {"TYPE", "TYPE"},
            {"TPYE", "TYPE"},
            {"TYP", "TYPE" },
        };

        foreach (var attributeName in attributeNames.Keys) {
            if (element.HasAttribute(attributeName)) {
                return element.GetAttribute(attributeName);
            }
        }

        // Failsafe conditions:
        return element.Name == "GlobalID" ? "GID" :
            // no matching attribute found, return empty string
            "";
    }

    private static CodeSnippetTypeMember CreateGenericPropertySnippet(string snippet) => new() { Text = $"{snippet}" };
}
