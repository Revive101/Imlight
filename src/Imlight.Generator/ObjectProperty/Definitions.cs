using Imlight.IO;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using SharpDX;
using Imlight.Common;
using static System.UInt64;

namespace Imlight.Generator.ObjectProperty
{
    internal class Definitions
    {
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
            { "Vector3D",         typeof(Vector3)    },
            { "Euler",            typeof(Vector3)    },
            { "Quaternion",       typeof(Quaternion) },
            { "Matrix3x3",        typeof(Matrix)     },
            { "Color",            typeof(Color3)     },
            { "Rect<float>",      typeof(RectangleF) },
            { "Rect<int>",        typeof(Rectangle)  },
            { "Point<float>",     typeof(Vector2)    },
            { "Point<int>",       typeof(Point)      },
            { "Size<int>",        typeof(Point)      },
            { "SerializedBuffer", typeof(string)     },
            { "SimpleVert",       typeof(string)     }, //@TODO: Find out what this is internally
            { "SimpleFace",       typeof(string)     }, //@TODO: Find out what this is internally

            // Unknown
            { "bui2",             typeof(byte)          },
            { "bui4",             typeof(byte)          },
            { "bui5",             typeof(byte)          },
            { "bui7",             typeof(byte)          },
            { "s24",              typeof(int)           }, // Has some relation to the CSR. 4 byte size so I'm reading it as int.
        };

        internal class ClassDef
        {
            internal string Name { get; set; }
            internal string? BaseName { get; set; }
            internal ClassDef? BaseDef { get; set; }
            internal byte PropertyCount { get; }
            internal ushort ByteSize { get; }
            internal uint Hash { get; }
            internal List<PropertyDef> Properties { get; }
            internal List<ClassDef> SubClasses { get; }

            // ctor
            public ClassDef(XmlNode node)
            {
                this.Properties = new List<PropertyDef>();
                this.SubClasses = new List<ClassDef>();
                var rawName = node.Attributes["Name"].Value;
                var rawBaseName = node.Attributes?["Base"]?.Value;

                // Cleanup the raw name to fit csharp standards.
                rawName = FixMadlibName(rawName);
                rawName = DefinitionUtil.RefactorName(rawName);
                //rawName = DefinitionUtil.ParseScopeName(rawName);
                this.Name = rawName;

                // If the base definition name has been set, we need to clean that too.
                if (rawBaseName is not null)
                {
                    rawBaseName = DefinitionUtil.RefactorName(rawBaseName);
                    //rawBaseName = rawBaseName.Replace('.', '_');
                    this.BaseName = rawBaseName;
                }

                if (!byte.TryParse(node.Attributes["PropertyCount"].Value, out var propCount)) 
                    throw new Exception();
                if (!ushort.TryParse(node.Attributes["Size"].Value, out var size)) 
                    throw new Exception();

                this.PropertyCount = propCount;
                this.ByteSize = size;

                this.Hash = Serializer.HashString(this.Name);

                // Iterate through the Xml child nodes and create a propertyDef.
                // Then, add that PropertyDef to this ClassDef.
                foreach (XmlNode propNode in node.ChildNodes)
                {
                    if (propNode.Name is "Function") continue;

                    var propDef = new PropertyDef(propNode);
                    this.Properties.Add(propDef);
                }
            }

            private static string FixMadlibName(string rawName)
            {
                // MadlibArgT<std::string const>
                if (!rawName.Contains('<')) return rawName;

                // Get the madlib type, which looks like `MadLibArgT<type>`.
                var i1 = rawName.IndexOf('<');
                var i2 = rawName.IndexOf('>');
                var strType = rawName[(i1 + 1)..i2];
                strType = strType.Replace(' ', '_');
                strType = strType.Replace("::", "_");

                // Section out the old type declaration, and replace it with our new one.
                rawName = rawName[0..i1];
                rawName += $"_{strType}";

                return rawName;
            }
        }

        internal class PropertyDef
        {
            internal string Name { get; init; }
            internal string Type { get; set; }
            internal int Flags { get; init; }
            internal string? DefaultValue { get; init; }
            internal uint Hash { get; init; }
            internal EnumDef? Options { get; set; }

            // ctor
            public PropertyDef(XmlNode node)
            {
                try
                {
                    var rawName = node.Attributes["Name"].Value;
                    this.Name = DefinitionUtil.RefactorName(rawName);

                    if (!int.TryParse(node.Attributes["Flags"].Value, out var intFlags)) 
                        throw new Exception();
                    this.Flags = intFlags;

                    var unparsedType = node.Attributes["Type"].Value;
                    var container = node.Attributes["Container"].Value;
                    this.Hash = Serializer.HashPropertyName(Name, unparsedType);

                    if (!TryParseType(unparsedType, container, out var parsedType))
                        throw new Exception();
                    this.Type = parsedType;

                    //@TODO: Set default value.

                    // Check if this property contains a nested node.
                    // If it does, it's an option. If the enumerator is of type `unsigned long`,
                    // it's a flagged enumerator.
                    if (node.ChildNodes.Count > 0 && node.ChildNodes[0]!.Name.StartsWith("Enum"))
                    {
                        var optionsNode = node.ChildNodes[0];
                        this.Options = new EnumDef(optionsNode!);
                    }
                }
                catch (Exception ex)
                {
                    Log.Logger.Error($"Property [{this.Name}] exception met: {ex.Message}");
                }
            }

            private static bool TryParseType(string strType, string strContainer, out string parsedType)
            {
                /*
                * Attribute "Container" is the data structure used for a property.
                * 1. Static | Raw T
                * 2. Vector | List<T>
                * 3. List   | LinkedList<T>
                */

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

                    strType = DefinitionUtil.RefactorName(formattedName);

                    // Check to see if the type is an accepted type.
                    if (_internalTypeTranslationDict.TryGetValue(strType, out var type))
                    {
                        strType = type.ToString();
                        strType = DefinitionUtil.ParseScopeName(strType);
                    }
                }
                else
                {
                    // Check to see if the type is an accepted type.
                    if (!_internalTypeTranslationDict.TryGetValue(strType, out var type))
                        throw new InvalidOperationException($"Type attribute [{strType}] could not be translated!");
                    strType = DefinitionUtil.ParseScopeName(type.ToString());
                }

                // Finally, craft the return string.
                // @TODO: LinkedList support
                var isList = strContainer is "vector" or "list";
                parsedType = (isList) ? $"List<{strType}>" : strType;
                return true;
            }

            public override string ToString()
            {
                var type = Options is { IsDefaultOrBaseClass: false } ? Options.Name : Type;
                var str = $"[Property({Hash}, {Flags})] public {type} {Name}";
                //if (DefaultValue is not "") str += $" = {DefaultValue}";
                str += ';';

                return str;
            }
        }

        internal class EnumDef
        {
            internal string Name { get; set; }
            internal int OptionsCount { get; init; }
            internal EnumOptionDef[] Options { get; set; }
            internal bool IsFlags { get; init; }
            internal bool IsStringValued { get; init; }
            internal bool IsDefaultOrBaseClass { get; init; }
            internal bool DoNotWrite { get; set; }

            // ctor
            internal EnumDef(XmlNode node)
            {
                DoNotWrite = false;

                // Get Xml values. We need exceptions to be thrown incase KI ever changes things.
                var rawName = node.Attributes?["Name"]?.Value
                              ?? throw new NullReferenceException("name");
                var rawOptionCount = node.Attributes?["OptionCount"]?.Value
                              ?? throw new NullReferenceException("OptionCount");

                // Parse and clean the Xml properties.
                if (!int.TryParse(rawOptionCount, out var bResult))
                    throw new InvalidOperationException("Could not parse option count byte");
                this.OptionsCount = bResult;
                this.Name = DefinitionUtil.RefactorName(rawName);

                switch (rawName)
                {
                    // If the enumerator is a string type, we need to change some things.
                    // The value will be a string, which doesn't apply in C#.
                    case "std::string":
                        IsStringValued = true;
                        break;
                    case "unsigned long" or "unsigned int":
                        IsFlags = true;
                        break;
                }

                // Finally, iterate through the child nodes and create a new definition for each.
                var options = new List<EnumOptionDef>();
                for (int i = 0; i < bResult; i++)
                {
                    var cNode = node.ChildNodes[i];
                    var optDef = new EnumOptionDef(cNode!);

                    options.Add(optDef);
                }

                // If an enumerator only contains one value and that one value starts with an underscore, it's a __DEFAULT or __BASECLASS enum.
                // These enums hold no semantic data to us, and can be ignored by the generator.
                if (options.Count() == 1 && options[0].Name.StartsWith('_'))
                    this.IsDefaultOrBaseClass = true;

                this.Options = options
                    .GroupBy(x => x.Name)
                    .Select(x => x.First())
                    .ToArray();
            }

            // ctor
            internal EnumDef(string Name)
            {
                this.Name = Name;
            }
        }

        internal class EnumOptionDef
        {
            internal string Name { get; set; }
            internal string Value { get; set; }

            // ctor
            internal EnumOptionDef(XmlNode node)
            {
                // Get Xml attributes.
                var rawName = node.Attributes?["Name"]?.Value
                              ?? throw new NullReferenceException("Node somehow did not contain a name?");
                var rawValue = node.Attributes?["Value"]?.Value 
                               ?? throw new NullReferenceException("value");
                if (rawName is "") rawName = "None";

                // Refactor the name to fit csharp standards.
                rawName = rawName.Replace(' ', '_');
                rawName = rawName.Replace('-', '_');
                rawName = DefinitionUtil.RefactorName(rawName);
                // Scope the name, if possible.
                if (rawName.Contains('.')) rawName = rawName.Split('.')[^1];

                this.Name = rawName;
                this.Value = rawValue;
            }
        }

        internal class DefinitionUtil
        {
            /// <summary>
            /// Trims off the prefixed 'class' subtext found in some nodes.
            /// </summary>
            /// <param name="name"></param>
            /// <returns></returns>
            /// <exception cref="ArgumentNullException"></exception>
            internal static string RefactorName(string name)
            {
                if (name is null) return name;
                if (string.IsNullOrWhiteSpace(name)) return name;

                // Remove C++ "::"
                name = name.Replace("::", ".");

                // Remove trailing ".m_full"
                name = name.Replace(".m_full", "");

                return !name.StartsWith("class ") && !name.StartsWith("struct ") && !name.StartsWith("enum ")
                    ? name
                    : name.Split(' ')[1];
            }

            internal static string ParseScopeName(string name)
            {
                return name.Split('.')[^1];
            }
        }
    }
}
