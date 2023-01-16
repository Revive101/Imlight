using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using Imlight.IO;
using Imlight.Common;
using SharpDX.Mathematics.Interop;

namespace Imlight.Generator.ObjectProperty
{
    internal class Old_PropertyClassGenerator
    {
        private const string NamespaceName = "Imlight.Internals";
        private const string TypesClassName = "Types";
        private const string PropertyTabs = "            ";

        private static readonly string _inputFolderPath = $"{Directory.GetCurrentDirectory()}/Input";
        private static readonly string _outputPath = $"{Directory.GetCurrentDirectory()}/output/propertyclass";
        private static readonly IReadOnlyDictionary<string, Type> _internalTypeTranslationDict = new Dictionary<string, Type>()
        {
            // Primitive
            { "int",              typeof(int)           },
            { "unsigned int",     typeof(int)           },
            { "short",            typeof(short)         },
            { "unsigned short",   typeof(ushort)        },
            { "std::string",      typeof(string)        },
            { "std::wstring",     typeof(string)        },
            { "long",             typeof(long)          },
            { "unsigned long",    typeof(ulong)         },
            { "float",            typeof(float)         },
            { "bool",             typeof(bool)          },
            { "double",           typeof(double)        },
            { "char",             typeof(char)          },
            { "wchar_t",          typeof(char)          },
            { "unsigned char",    typeof(byte)          },
            { "unsigned __int64", typeof(ulong)         },

            // Internal
            { "gid",              typeof(ulong)         },
            { "Vector3D",         typeof(RawVector3)    },
            { "Euler",            typeof(RawVector3)    },
            { "Quaternion",       typeof(RawQuaternion) },
            { "Matrix3x3",        typeof(RawMatrix)     },
            { "Color",            typeof(RawColor3)     },
            { "Rect<float>",      typeof(RawRectangleF) },
            { "Rect<int>",        typeof(RawRectangle)  },
            { "Point<float>",     typeof(RawVector2)    },
            { "Point<int>",       typeof(RawPoint)      },
            { "Size<int>",        typeof(RawPoint)      },
            { "SerializedBuffer", typeof(string)        },
            //{ "SimpleVert",       typeof(null)          }, //@TODO: Find out what this is internally
            //{ "SimpleFace",       typeof(null)          }, //@TODO: Find out what this is internally

            // Unknown
            { "bui2",             typeof(byte)          },
            { "bui4",             typeof(byte)          },
            { "bui5",             typeof(byte)          },
            { "bui7",             typeof(byte)          },
            { "s24",              typeof(int)           }, // Has some relation to the CSR. 4 byte size so I'm reading it as int.
        };

        private ushort _outputFileCount;
        private Hashtable _optionEnums;

        /// <summary>
        /// Generate csharp classes from Wizard101's type dump xml. The output will be split into partial classes equal to the parameter.
        /// </summary>
        /// <param name="outputFileCount">The amount of files to generate.</param>
        public void Generate(ushort outputFileCount, GeneratorOptions generatorOptions)
        {
            if (outputFileCount <= 0) throw new ArgumentOutOfRangeException(nameof(outputFileCount));
            this._outputFileCount = outputFileCount;
            this._optionEnums = new Hashtable();

            Log.Logger.Information("Starting generation of Property Classes.");
            Log.Logger.Information($"Outputting [{_outputFileCount}] partial classes.");

            var propDump = GetPropDumpXml();
            Log.Logger.Information("Wizard101 dump found!");

            GeneratePropertyClasses(propDump, generatorOptions);
        }

        /// <summary>
        /// Prints each unique data type from Wizard101's type dump to the console. Used for testing purposes.
        /// </summary>
        /// <exception cref="NullReferenceException"></exception>
        public void PrintUniquePropTypes()
        {
            var propDump = GetPropDumpXml();

            // There's only one base node here, labeled `ClassList`. Every child node will be a class definition.
            var classesList = propDump.ChildNodes[0]?.ChildNodes 
                              ?? throw new NullReferenceException("propDump.ChildNodes?[0]?.ChildNodes");

            var uniquePropTypes = new HashSet<string>();

            foreach (XmlNode classNode in classesList)
            {
                foreach (XmlNode propNode in classNode.ChildNodes)
                {
                    // Skip functions
                    if (propNode.Name == "Function") continue;

                    // Get type attribute
                    var strType = propNode.Attributes?["Type"]?.Value 
                                  ?? throw new NullReferenceException("propNode.Attributes?[\"Type\"]?.Value");
                    if (strType.StartsWith("class SharedPointer")
                        || strType.StartsWith("enum")
                        || strType.StartsWith("struct")) continue;
                    if (!uniquePropTypes.Add(strType)) continue;
                }
            }

            foreach (var strType in uniquePropTypes)
            {
                Log.Logger.Information(strType);
            }
        }

        /// <summary>
        /// Generates csharp classes from Wizard101's type dump xml.
        /// </summary>
        /// <param name="xmlDoc">The XmlDocument of Wizard101's type dump.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NullReferenceException"></exception>
        private void GeneratePropertyClasses(XmlDocument xmlDoc, GeneratorOptions generatorOptions)
        {
            if (xmlDoc is null) throw new ArgumentNullException(nameof(xmlDoc));

            // There's only one base node here, labeled `ClassList`. Every child node will be a class definition.
            var classesList = xmlDoc.ChildNodes?[0]?.ChildNodes 
                              ?? throw new NullReferenceException("xmlDoc.ChildNodes?[0]?.ChildNodes");

            var seenClasses = new HashSet<string>();

            // There are thousands of types. It's best if we don't store that all in one file.
            // Instead, we'll create x amount of partial classes.
            var classesAllowedPerFileCount = classesList.Count / _outputFileCount;
            var currentClassIndex = 0;

            Log.Logger.Debug($"Allowing [{classesAllowedPerFileCount}] classes per file.");

            // Iterate through each file and add the partial class.
            for (var i = 0; i < _outputFileCount; i++)
            {
                // Initialize CodeDom.
                CodeCompileUnit codeCompileUnit = new();
                CodeNamespace codeNamespace = new(NamespaceName);

                // Declare code declaration and all it's properties.
                CodeTypeDeclaration codeClass = new(TypesClassName)
                {
                    IsPartial = true,
                    IsClass = true,
                    TypeAttributes = TypeAttributes.Public
                                   | TypeAttributes.Sealed
                                   | TypeAttributes.BeforeFieldInit,
                    Attributes = MemberAttributes.Public,
                };

                // Add System import.
                CodeNamespaceImport codeClassSystemImport = new("System");
                codeNamespace.Imports.Add(codeClassSystemImport);

                // Iterate through classes in the dump until we reach the maximum amount of classes per file.
                // Create a C# class for each Xml class, and add it as a subclass.
                for (var x = currentClassIndex; x < (classesAllowedPerFileCount + currentClassIndex); x++)
                {
                    var xmlNode = classesList[x];
                    if (xmlNode is null) continue;
                    if (xmlNode.Name != "Class") continue;

                    // Make sure this isn't a duplicate class.
                    var name = xmlNode.Attributes?["Name"]?.Value 
                               ?? throw new NullReferenceException("xmlNode.Attributes?[\"Name\"]?.Value");
                    if (!seenClasses.Add(name)) continue;

                    AddClassFromXml(ref codeClass, xmlNode);
                }

                currentClassIndex += classesAllowedPerFileCount;

                // Finally, generate the CodeCompileUnit to a file.
                codeNamespace.Types.Add(codeClass);
                codeCompileUnit.Namespaces.Add(codeNamespace);
                var domProvider = CodeDomProvider.CreateProvider("CSharp");
                var outputPath = $"{GetOrCreateOutputDirectory()}/{TypesClassName}_{i+1}.cs";
                var options = new CodeGeneratorOptions
                {
                    BracingStyle = generatorOptions.CurlyBraceNewline ? "C" : "Block",
                    IndentString = generatorOptions.IndentString,
                    ElseOnClosing = false,
                };
                using var writer = new StreamWriter(outputPath);
                domProvider.GenerateCodeFromCompileUnit(codeCompileUnit, writer, options);

                Log.Logger.Information($"Generated partial class to file {TypesClassName}_{i+1}.cs");
            }
        }

        /// <summary>
        /// Generates a subclass from an Xml node and it's child properties.
        /// </summary>
        /// <param name="r_parentCodeClass">The reference to the parent CodeDom class.</param>
        /// <param name="xmlNode">The Xml node to generate the class from.</param>
        /// <exception cref="NullReferenceException"></exception>
        private void AddClassFromXml(ref CodeTypeDeclaration r_parentCodeClass, XmlNode xmlNode)
        {
            // Get Xml attributes.
            var name = xmlNode.Attributes?["Name"]?.Value ?? throw new NullReferenceException("xmlNode.Attributes[\"Name\"].Value");
            var baseClass = xmlNode.Attributes?["Base"]?.Value ?? null;

            // Initialize CodeDom Class.
            var formattedClassName = TrimName(name);
            CodeTypeDeclaration codeClass = new(formattedClassName);

            // Add the base class found in the Xml attributes.
            if (baseClass is not null)
            {
                var formattedBaseClassName = TrimName(baseClass);
                CodeTypeReference codeTypeReference = new(formattedBaseClassName);
                codeClass.BaseTypes.Add(codeTypeReference);
            }

            // Create hash field. This is to implement the PropertyClass interface.
            var classHash = Serializer.HashString(name);
            var hashField = CreateGenericPropertySnippet($"{PropertyTabs}public override uint GetHash() => 0x{classHash};");
            codeClass.Members.Add(hashField);
            // Add whitespace buffer
            codeClass.Members.Add(CreateGenericPropertySnippet(""));

            AddPropsToClassFromXml(ref codeClass, xmlNode);

            r_parentCodeClass.Members.Add(codeClass);
        }
        
        /// <summary>
        /// Generates csharp fields to a CodeDom class from Wizard101's type dump xml.
        /// </summary>
        /// <param name="r_codeClass">The reference to the CodeDom class.</param>
        /// <param name="xmlNode">The Xml node to generate from.</param>
        private void AddPropsToClassFromXml(ref CodeTypeDeclaration r_codeClass, XmlNode xmlNode)
        {
            // Iterate through the XML properties and add them to the codeClass.
            for (var i = 0; i < xmlNode.ChildNodes!.Count; i++)
            {
                var propNode = xmlNode.ChildNodes[i] ?? throw new NullReferenceException("xmlNode.ChildNodes[i]");
                if (propNode.Name != "Property") continue;

                var propName = propNode.Attributes?["Name"]?.Value ?? throw new NullReferenceException();

                // Craft PropertyAttribute from xml data.
                var hash = GetPropertyHashFromXml(propNode);
                var bitFlags = GetBitflagsFromXml(propNode);
                var propAttr = $"[Property(0x{hash}, {bitFlags})]";

                // Get property data type.
                var type = GetTypeDeclarationFromXml(propNode);

                // Get default value
                var defaultVal = GetDefaultValueFromXml(propNode, type);

                // Property types that are enumerators will sometimes identify themselves as a child node. This is called an option.
                // This is a class that needs to be generated later.
                if (xmlNode.ChildNodes.Count > 0 && xmlNode.ChildNodes?[0]?.Name == "Enum")
                {
                    // If the enum contains one option, it's either __BASECLASS or __DEFAULT. Both of which can be ignored.
                    var enumNode = xmlNode.ChildNodes?[0] ?? throw new NullReferenceException();
                    var countAttrStr = enumNode.Attributes?["OptionCount"]?.Value
                                    ?? throw new NullReferenceException();
                    int.TryParse(countAttrStr, out var optionCount);

                    if (optionCount > 1) _optionEnums.Add(propName, enumNode);
                }

                // Finally, craft the property snippet.
                var snippetStr = $"{PropertyTabs}{propAttr} public {type} {propName}";
                if (defaultVal is not null) snippetStr += defaultVal;
                snippetStr += ';';

                var snippet = CreateGenericPropertySnippet(snippetStr);
                r_codeClass.Members.Add(snippet);
            }
        }

        /// <summary>
        /// Gets Wizard101's type dump xml from the input directory.
        /// </summary>
        /// <returns>An XmlDocument translation of the raw file data.</returns>
        /// <exception cref="Exception">Thrown if the file isn't found.</exception>
        private static XmlDocument GetPropDumpXml()
        {
            var dumpFilePath = Path.Combine(_inputFolderPath, "PropertyClassDump.xml");
            if (!File.Exists(dumpFilePath))
                throw new Exception($"PropertyClassDump.xml is not present in the directory \"{_inputFolderPath}\"!");

            Log.Logger.Information("PropertyClassDump found!");

            // Load PropertyClassDump.xml as document.
            XmlDocument xmlDoc = new();
            using StreamReader reader = new(dumpFilePath, Encoding.UTF8);
            xmlDoc.Load(reader);

            return xmlDoc;
        }

        /// <summary>
        /// Generates a property hash from the xml property.
        /// </summary>
        /// <param name="node">The Xml node to generate the hash from.</param>
        /// <returns>The hash generated.</returns>
        /// <exception cref="NullReferenceException"></exception>
        private static uint GetPropertyHashFromXml(XmlNode node)
        {
            var propName = node.Attributes?["Name"]?.Value ?? throw new NullReferenceException("propNode.Attributes[\"Name\"].Value");
            var strType = node.Attributes["Type"]?.Value ?? throw new NullReferenceException("propNode.Attributes[\"Type\"].Value");
            var propHash = Serializer.HashPropertyName(propName, strType);

            return propHash;
        }

        /// <summary>
        /// Gets the bitflags from a property Xml node.
        /// </summary>
        /// <param name="node">The property's xml node.</param>
        /// <returns>The parsed integer from the Xml node's attributes.</returns>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        private static int GetBitflagsFromXml(XmlNode node)
        {
            // Get serializer bit flags and parse them as integer.
            var strBits = node.Attributes?["Flags"]?.Value ?? throw new NullReferenceException("propNode.Attributes[\"Flags\"].Value");
            if (!int.TryParse(strBits, out var bitFlags)) throw new InvalidCastException($"Could not cast bit flags [{strBits}].");

            return bitFlags;
        }

        /// <summary>
        /// Dictates the csharp data type used from an xml property.
        /// </summary>
        /// <param name="node">The xml node in question.</param>
        /// <returns>A string of the data type.</returns>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        private static string GetTypeDeclarationFromXml(XmlNode node)
        {
            /*
            * Getting the type is complex, so I'll be writing some notes here.
            *
            * Attribute "Container" is the data structure used for a property.
            * 1. Static | Raw T
            * 2. Vector | List<T>
            * 3. List   | LinkedList<T>
            */

            // Get raw Xml attributes as strings.
            var strType = node.Attributes?["Type"]?.Value
                          ?? throw new NullReferenceException("propNode.Attributes[\"Type\"].Value");
            var strContainer = node.Attributes?["Container"]?.Value?.ToLower()
                          ?? throw new NullReferenceException("propNode.Attributes[\"Container\"].Value");

            // @TODO: LinkedList support
            var isList = strContainer is "vector" or "list";

            // If the type is a class, struct, or enum, we don't need to validate it as it will be generated.
            if (strType.StartsWith("class ") 
                || strType.StartsWith("struct ") 
                || strType.StartsWith("enum "))
            {
                var formattedName = strType;

                // Check for pointers (which don't apply in C#).
                if (strType.Contains("SharedPointer"))
                {
                    // Type="class SharedPointer<class PropertyClass>"
                    var i1 = strType.IndexOf('<');
                    var i2 = strType.IndexOf('>');
                    formattedName = strType[(i1 + 1)..i2];
                }
                else if (strType.Contains('*'))
                    formattedName = strType.Replace("*", string.Empty);

                strType = TrimName(formattedName);
            }
            else
            {
                // Check to see if the type is an accepted type.
                if (!_internalTypeTranslationDict.TryGetValue(strType, out var type))
                    throw new InvalidOperationException($"Type attribute [{strType}] could not be translated!");
                strType = TrimTypeName(type.ToString());
            }

            // Finally, craft the return string.
            return (isList) ? $"List<{strType}>" : strType;
        }

        /// <summary>
        /// Dictates the default value as a string. The typeDeclaration is meant to be the one generated, NOT the raw one from Xml.
        /// </summary>
        /// <param name="node">The Xml node in question.</param>
        /// <param name="typeDeclaration">The type declaration generated from GetTypeDeclarationFromXml().</param>
        /// <returns>The string value of the value, including the '='.</returns>
        private static string? GetDefaultValueFromXml(XmlNode node, string typeDeclaration)
        {
            var defaultVal = node.Attributes?["Default"]?.Value ?? null;
            if (defaultVal is null) return null;

            // Easy values
            switch (typeDeclaration)
            {
                case "Int32":
                case "Int16":
                case "UInt16":
                case "Int64":
                case "UInt64":
                case "Byte":
                    return $" = {defaultVal}";
                case "String":
                    return $" = \"{defaultVal}\"";
                case "char":
                    return $" = \'{defaultVal}\'";
                case "Double":
                case "Single":
                    return $" = {defaultVal}f";
                case "Boolean":
                    return (defaultVal == "1") ? " = true" : " = false";
            }

            // If the above switches are not set, that means creating a default value will be a bit more complicated.
            //@TODO: Create complex default values.
            return null;
        }

        /// <summary>
        /// Trims off the prefixed 'class' subtext found in some nodes.
        /// </summary>
        /// <param name="className"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private static string TrimName(string className)
        {
            if (className is null) throw new ArgumentNullException(nameof(className));
            if (string.IsNullOrWhiteSpace(className))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(className));

            return !className.StartsWith("class ") && !className.StartsWith("struct ") && !className.StartsWith("enum ")
                ? className
                : className.Split(' ')[1];
        }

        private static string TrimTypeName(string typeName)
        {
            return typeName.Split('.')[^1];
        }

        private static string GetOrCreateOutputDirectory()
        {
            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);

            return _outputPath;
        }

        private static CodeSnippetTypeMember CreateGenericPropertySnippet(string snippet)
            => new CodeSnippetTypeMember() { Text = snippet };
    }
}