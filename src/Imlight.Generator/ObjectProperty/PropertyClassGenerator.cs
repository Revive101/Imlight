using Imlight.Common;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Text.RegularExpressions;
using Imlight.Internals;

namespace Imlight.Generator.ObjectProperty
{
    internal class PropertyClassGenerator
    {

        // The end goal is to use the Wizard101 type dump to generate C# classes.
        // You can find this dump by handing the `-x <file_name>` argument to the `WizardGraphicalClient.exe`.
        // The `csr.wad` *must* be present for proper authority.

        private const string NamespaceName = "Imlight.Internals";
        private const string TypesClassName = "Types";
        private const string DispatchTabs = "                ";
        private const string PropertyTabs = "            ";

        private static readonly string InputFolderPath = $"{Directory.GetCurrentDirectory()}/Input";
        private static readonly string OutputPath = $"{Directory.GetCurrentDirectory()}/output/propertyclass";

        private readonly PropertyClassGeneratorOptions _options;
        private List<Definitions.ClassDef> _classes;
        private List<Definitions.EnumDef> _enums;
        private List<Definitions.EnumDef> _standaloneEnums;
        private readonly List<string> _hazardEnumNames = new()
        {
            "std.string",
            "@string",
            "string",
            "unsigned long",
            "unsigned int",
            "int"
        };

        // ctor
        public PropertyClassGenerator(PropertyClassGeneratorOptions options)
        {
            this._options = options;

            Log.Logger.Information($"PropertyClassGenerator created with options:" +
                     $"\nInputFileDir: {_options.InputName}");
        }

        public void Generate()
        {
            Log.Logger.Warning("Beginning generation on PropertyClasses! This may take many moments.");

            // Begin by grabbing and validating the dump xml from the Wizard101 client.
            var xmlDoc = GetPropDumpXml(_options.InputName);
            if (xmlDoc is null) throw new NullReferenceException($"{_options.InputName} could not be found!");
            SetClassDefs(xmlDoc);
            Log.Logger.Information("Successfully created all class definitions.");

            // By this point, the generator has all Informationrmation needed. Initialize the CodeDom
            // and generate these classes into files.
            Log.Logger.Information("Starting CodeDom generation..");
            var compilers = CreateCodeDom();
            Log.Logger.Information("CodeDom generation complete!");

            Log.Logger.Information("Writing each class to file..");
            WriteCodeDomToDisk(compilers);
            Log.Logger.Information("CodeDom written to disk!");

            Log.Logger.Information("Generation complete!");
            Log.Logger.Warning("These classes might not be entirely valid. It's highly recommended to check" +
                     " the integrity of these files before using them practically.");
        }

        private void SetClassDefs(XmlDocument doc)
        {
            // There's only one base node here, labeled `ClassList`. Every child node will be a class definition.
            // We're going to create a ClassDef from each of these nodes.
            var classesList = doc.ChildNodes?[0]?.ChildNodes
                              ?? throw new Exception("Could not find class node. Are you sure this is the right file?");

            // Iterate through each of the nodes and create a ClassDef from it.
            var classes = new HashSet<Definitions.ClassDef>();
            for (int i = 0; i < classesList.Count; i++)
            {
                var xmlNode = classesList[i] ?? throw new NullReferenceException();
                var def = new Definitions.ClassDef(xmlNode);

                classes.Add(def);
            }

            this._classes = classes.ToList();

            // All class definitions have been created. Now we must iterate through each one again
            // to set the more difficult properties. It's unreliable to do this prior, as some properties
            // rely on other class definitions already existing.
            foreach (var def in classes)
            {
                if (def.Name == "PropertyClass") continue;
                if (def.BaseName is null or "")
                {
                    def.BaseName = "PropertyClass";
                }

                // Find and set the actual parent definition for this class.
                var parentDef = classes.FirstOrDefault(x => x.Name == def.BaseName);
                if (parentDef is not null)
                {
                    def.BaseDef = parentDef;
                }
                else
                {
                    // If the name is set but we can't find the class definition, it means the parent class
                    // does not inherit PropertyClass. In such case, we must rename the parent.
                    def.BaseName = def.BaseName!.Replace('.', '_');
                    continue;
                }

                RemoveOverriddenPropertiesFromDef(def);
            }

            SetOptionEnums();
            NestSubclasses(ref classes);
            VerifyPropertyTypes(ref classes);

            // Remove potential class def duplicates by name.
            this._classes = this._classes
                .GroupBy(x => x.Name)
                .Select(x => x.First())
                .ToList();
        }

        private void RemoveOverriddenPropertiesFromDef(Definitions.ClassDef def)
        {
            // We're going to go up the inhertiance tree until we find the root.
            // Then, we'll take each property from those parent classes and compare them
            // to the current. If a property is shared between the class and any of it's parent classes,
            // it will be removed from the class.
            var parentProperties = new List<Definitions.PropertyDef>();
            var lastDef = def;
            while (true)
            {
                if (lastDef.BaseName is "" or null) break;

                var thisParentDef = _classes.FirstOrDefault(x => x.Name == lastDef.BaseName);
                if (thisParentDef is null) break;

                parentProperties.AddRange(thisParentDef.Properties);
                lastDef = thisParentDef;
            }

            // Remove duplicate properties between the parent definitions.
            parentProperties = parentProperties
                .Distinct()
                .ToList();

            // Remove a property from the current definition if that property is found in any
            // of it's parent classes.
            def.Properties
                .RemoveAll(x => parentProperties.Any(y => y.Name == x.Name));
            parentProperties.Clear();
        }

        private void SetOptionEnums()
        {
            // Iterate through every property of every class and find if it's an enum.
            // We'll add these enums to a list to then generate later.
            var enumDefs = new List<Definitions.EnumDef>();
            foreach (var classDef in _classes)
            {
                foreach (var propDef in classDef.Properties.Where(propDef => propDef.Options is not null))
                {
                    enumDefs.Add(propDef.Options!);

                    // In some cases, an enum will not have a proper name. It will usually be a
                    // primitive data type, such as `std::string` or `unsigned long`. If an enum
                    // doesn't have a proper name, we can create a name for it using the name of the base class + property.
                    //@TODO: Move this to the enum definition class.
                    if (!_hazardEnumNames.Contains(propDef.Options!.Name)) continue;
                    var scopedName = $"{classDef.Name}.{propDef.Name}";

                    // Clean the name if possible.
                    if (scopedName.Contains("m_"))
                    {
                        // ex: ElixirBenefitEffectTemplate.m_flags
                        var idx = scopedName.IndexOf("m_", StringComparison.Ordinal);
                        scopedName = scopedName.Replace("m_", "");

                        // Capitalize the following char.
                        var letters = scopedName.ToCharArray();
                        letters[idx] = char.ToUpper(letters[idx]);
                        scopedName = new string(letters);
                    }

                    propDef.Options.Name = scopedName;

                    Log.Logger.Warning($"Renamed hazard enumerator in [{classDef.Name}] to: {scopedName}");
                }
            }

            // Now we've created all enumerators. We're going to find the duplicate enumerators
            // and instead generate one *outside* it's parent class to avoid code duplication.
            var x = enumDefs.GroupBy(x => x.Options);
            var duplicateEnums = enumDefs
                .Where(x => !x.IsDefaultOrBaseClass && !x.Name.Contains('.'))
                .GroupBy(x => x.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.First())
                .ToList();

            foreach (var item in enumDefs)
            {
                if (duplicateEnums.Any(x => x.Name == item.Name))
                {
                    item.DoNotWrite = true;
                }
            }

            this._standaloneEnums = duplicateEnums;
            this._enums = enumDefs;
        }

        private void NestSubclasses(ref HashSet<Definitions.ClassDef> classDefs)
        {
            // Some classes will be named to their parent class. They must be declared inside their parent class definition.
            // For example: `MapInformationManager.MapInformation`

            var nestDefs = new Dictionary<int, List<Definitions.ClassDef>>();
            var renameCollection = new List<Definitions.ClassDef>();
            foreach (var classDef in classDefs
                         .Where(classDef => classDef.Name.Contains('.'))
                         .ToList())
            {
                // If the class is a double or more nest, save it to another list to perform later, as the previous
                // class may not yet be generated.
                // For example: `MapInformationManager.MapInformation.DoodleData`
                var nestCount = classDef.Name.Count(x => x == '.');
                if (nestCount > 1)
                {
                    var key = nestDefs.GetOrCreate(nestCount);
                    key.Add(classDef);
                }

                // Get the parent class name.
                var idx = classDef.Name.LastIndexOf('.');
                var parentName = classDef.Name[..idx];
                var lastScopedDef = _classes.FirstOrDefault(x => x.Name == parentName);

                // If we're unable to find the parent class, it means the parent class does not inherit PropertyClass.
                // In such cases, we will rename the class to use underscores instead of dot notation.
                if (lastScopedDef is null)
                {
                    renameCollection.Add(classDef);
                    continue;
                }

                // Now that we've used the name, we can scope the class name to it's proper scope.
                // Rename the class to the proper scope.
                classDef.Name = classDef.Name.Split('.')[^1];

                // Add it to it's parent class definition and remove it from our definition list.
                lastScopedDef.SubClasses.Add(classDef);
                _classes.Remove(classDef);
            }

            // Do the same thing for the enumerators.
            foreach (var enumDef in _enums.Where(enumDef => enumDef.Name.Contains('.')))
            {
                var idx = enumDef.Name.LastIndexOf('.');
                var parentName = enumDef.Name[..idx];
                var lastScopedDef = _classes.FirstOrDefault(x => x.Name == parentName);

                //if (lastScopedDef == null) throw new Exception();

                // Rename the enum to the proper scope
                enumDef.Name = enumDef.Name.Split('.')[^1];
            }

            foreach (var entry in renameCollection)
            {
                var oldName = entry.Name;
                entry.Name = entry.Name.Replace('.', '_');
                Log.Logger.Warning($"Renamed hazardous class definition [{oldName}] to: {entry.Name}");
            }
        }

        private void VerifyPropertyTypes(ref HashSet<Definitions.ClassDef> classDefs)
        {
            // Essay imbound. This is for future Chi who never wants to come back to this. Consider it a hertiage gift.

            // This method is intended to be called in the last stage of class definition generation.
            // Some properties have their data type set to a meaningless type that no longer exists because:
            // a. We renamed it.
            // b. The original enum is not defined by Wizard101's type dump.
            // In such case, this method is intended to fix that.

            // To check the integry of a data type:
            //  1. Check if the type contains a '.'
            //  2. Scope the type to it's defining class.
            //  2. Search the defining class definition:
            //      1. If the parent class exists, check if the type is defined there. If it isn't, generate an empty enum.
            //      2. If one doesn't exist, replace each dot notation in order until the class is found.
            //          1. Replace each dot with an underscore once found.
            //          2. Do step 1.

            // Case A: The data type did not adhere to the class definition name change.
            //  1. Replace the first '.' with '_' to see if we can find it.
            // Case B: Wizard101 simply doesn't define a definition in the dump.
            //  1. Create an empty enum with respective name.

            foreach (var def in classDefs.Where(x => x.Properties.Count > 0))
            {
                foreach (var prop in def.Properties.Where(x => x.Type.Contains('.'))
                    .GroupBy(x => x.Type)
                    .Select(x => x.First())
                    .ToList())
                {
                    var idx = prop.Type.LastIndexOf(".");
                    var typeName = prop.Type[(idx + 1)..];
                    var scopedTypeName = prop.Type[..idx];
                    var scopedTypeDef = _classes.FirstOrDefault(y => y.Name == scopedTypeName);

                    // Failsafe; if a class is not found, replace '.' with '_'.
                    if (scopedTypeDef == null)
                    {
                        bool found = false;
                        for (int i = 0; i < scopedTypeName.Count(c => c == '.') + 1; i++)
                        {
                            var regx = new Regex(Regex.Escape("."));
                            var t = regx.Replace(scopedTypeName, "_", i+1);
                            scopedTypeDef = _classes.FirstOrDefault(y => y.Name == t);
                            if (scopedTypeDef != null)
                            {
                                found = true;
                                prop.Type = t;
                                break;
                            }
                        }
                        // If a parent class is not found, we're going to rename the type
                        // to match our naming conentions.
                        if (!found)
                        {
                            prop.Type = prop.Type.Replace('.', '_');
                            prop.Options = new Definitions.EnumDef(typeName);
                            continue;
                        }
                    }
                    
                    // If the parent class has a definition already defined, skip it.
                    if (scopedTypeDef!.Properties
                        .Where(x => x.Options != null)
                        .Any(p => p.Options.Name == typeName)) continue;
                    if (scopedTypeDef!.SubClasses
                        .Any(x => x.Name == typeName)) continue;

                    // Otherwise, create a new empty enum definition on the property.
                    // Change the rest of the occurences of this data type in this class def.
                    string check = prop.Type;
                    for (int i = 0; i < def.Properties.Count; i++)
                    {
                        if (def.Properties[i].Type == check)
                        {
                            def.Properties[i].Type = typeName;
                        }
                    }
                    prop.Options = new Definitions.EnumDef(typeName);

                    Log.Logger.Warning($"Created empty enum under class definition [{scopedTypeDef.Name}] at name [{typeName}].");
                }
            }
        }

        private CodeMemberMethod GenerateDispatcherMethod()
        {
            if (this._classes is null) throw new NullReferenceException($"{nameof(_classes)} cannot be null.");

            var codeMethod = new CodeMemberMethod()
            {
                Name = "Dispatch",
                ReturnType = new CodeTypeReference(typeof(PropertyClass))
            };
            var hashParam = new CodeParameterDeclarationExpression(typeof(uint), "hash");
            codeMethod.Parameters.Add(hashParam);

            // Create the start of this method. Then, iterate through the class definitions to create a case for each hash.
            var sb = new StringBuilder();
            sb.Append("switch (hash) {\n");
            foreach (var classDef in this._classes)
            {
                if (classDef.Name == "PropertyClass") continue;
                sb.Append($"{DispatchTabs}case {classDef.Hash}: return new {classDef.Name}();\n");
            }
            sb.Append($"{DispatchTabs}default: return null;\n");
            sb.Append($"            }}");

            var codeSwitchSnippet = new CodeSnippetExpression(sb.ToString());
            codeMethod.Statements.Add(codeSwitchSnippet);
            return codeMethod;
        }

        private CodeCompileUnit CreateCodeDom()
        {
            // Initialize CodeDom.
            CodeCompileUnit codeCompileUnit = new();
            CodeNamespace codeNamespace = new(NamespaceName);

            // Declare code declaration. This will be the sealed base class in each file.
            // Every PropertyClass will be a subclass of this one.
            CodeTypeDeclaration fileBaseClassDecl = new(TypesClassName)
            {
                IsPartial = true,
                IsClass = true,
                TypeAttributes = TypeAttributes.Public
                                 | TypeAttributes.Sealed
                                 | TypeAttributes.BeforeFieldInit,
                Attributes = MemberAttributes.Public,
            };

            // Add imports to the namespace.
            CodeNamespaceImport codeClassSystemImport = new("System");
            CodeNamespaceImport codeClassSharpDxImport = new("SharpDX");
            codeNamespace.Imports.Add(codeClassSystemImport);
            codeNamespace.Imports.Add(codeClassSharpDxImport);

            // Add CodeDom together.
            codeCompileUnit.Namespaces.Add(codeNamespace);
            codeNamespace.Types.Add(fileBaseClassDecl);

            foreach (var classDef in this._classes)
            {
                // We don't need to generate this, as it's manually typed.
                if (classDef.Name == "PropertyClass") continue;

                var classDecl = CreateDeclarationFromDefinition(classDef);
                fileBaseClassDecl.Members.Add(classDecl);
            }

            foreach (var enumDef in this._standaloneEnums)
            {
                var enumDecl = CreateDeclarationFromDefinition(enumDef);
                fileBaseClassDecl.Members.Add(enumDecl);
            }

            // Create dispatcher method.
            Log.Logger.Information("Creating dispatcher method..");
            var dispatch = GenerateDispatcherMethod();
            fileBaseClassDecl.Members.Add(dispatch);

            return codeCompileUnit;
        }

        private void WriteCodeDomToDisk(CodeCompileUnit compiler)
        {
            var domProvider = CodeDomProvider.CreateProvider("CSharp");
            var outputPath = $"{GetOrCreateOutputDirectory()}/{TypesClassName}.cs";
            var options = new CodeGeneratorOptions
            {
                BracingStyle = _options.CurlyBraceNewline ? "C" : "Block",
                IndentString = _options.IndentString,
                ElseOnClosing = false,
            };
            using var writer = new StreamWriter(outputPath);
            domProvider.GenerateCodeFromCompileUnit(compiler, writer, options);

            Log.Logger.Information($"Generated partial class to file {TypesClassName}.cs");
        }

        private static CodeTypeDeclaration CreateDeclarationFromDefinition(Definitions.ClassDef def, bool isSub = false)
        {
            var codeDecl = new CodeTypeDeclaration(def.Name);

            // Determine how many tabs must be applied, if this is a subclass.
            var bonusTabs = isSub ? "    " : "";

            // Add a base class, if one exists.
            if (def.BaseName is not null)
            {
                var properBaseName = def.BaseName;
                if (properBaseName == "Search.ResultItem")
                {
                    properBaseName = properBaseName.Replace('.', '_');
                }
                codeDecl.BaseTypes.Add(new CodeTypeReference(properBaseName));

                // Create hash field. This implements the PropertyClass interface;
                var hashField =
                    CreateGenericPropertySnippet(
                        $"{PropertyTabs}{bonusTabs}public override uint GetHash() => {def.Hash};");
                codeDecl.Members.Add(hashField);
            }

            // Iterate through sub classes and create a new, nested CodeDom class.
            foreach (var subDef in def.SubClasses)
            {
                var subDecl = CreateDeclarationFromDefinition(subDef, true);
                codeDecl.Members.Add(subDecl);
            }

            // Iterate through the properties.
            foreach (var propDef in def.Properties)
            {
                var snippet = CreateGenericPropertySnippet($"{PropertyTabs}{bonusTabs}{propDef}");
                codeDecl.Members.Add(snippet);

                // If this property has an attached enumerator, generate it here.
                if (propDef.Options is null 
                    || propDef.Options.DoNotWrite 
                    || propDef.Options.IsDefaultOrBaseClass) 
                    continue;
                var enumDef = CreateDeclarationFromDefinition(propDef.Options, true);
                codeDecl.Members.Add(enumDef);
            }

            return codeDecl;
        }

        private static CodeTypeDeclaration CreateDeclarationFromDefinition(Definitions.EnumDef def, bool isSub = false)
        {
            var enumDecl = new CodeTypeDeclaration(def.Name)
            {
                IsEnum = true
            };

            // Determine how many tabs must be applied, if this is a subclass.
            var bonusTabs = isSub ? "    " : "";

            // Add the [Flags] attribute if needed
            if (def.IsFlags)
            {
                var flagsRef = new CodeTypeReference(typeof(FlagsAttribute));
                var flagsAttr = new CodeAttributeDeclaration(flagsRef);
                enumDecl.CustomAttributes.Add(flagsAttr);
            }

            // Iterate through the options of this enum and add them as a field.
            if (def.Options == null) return enumDecl;
            foreach (var enumOptionDef in def.Options)
            {
                var snippet = (def.IsStringValued)
                    ? CreateGenericPropertySnippet(
                        $"{PropertyTabs}{bonusTabs}{enumOptionDef.Name},")
                    : CreateGenericPropertySnippet(
                        $"{PropertyTabs}{bonusTabs}{enumOptionDef.Name} = {enumOptionDef.Value},");

                enumDecl.Members.Add(snippet);
            }

            return enumDecl;
        }

        /// <summary>
        /// Gets Wizard101's type dump xml from the input directory.
        /// </summary>
        /// <returns>An XmlDocument translation of the raw file data.</returns>
        /// <exception cref="Exception">Thrown if the file isn't found.</exception>
        private static XmlDocument GetPropDumpXml(string fileName)
        {
            var dumpFilePath = Path.Combine(InputFolderPath, fileName);
            if (!File.Exists(dumpFilePath))
                throw new Exception($"{fileName} is not present in the directory \"{InputFolderPath}\"!");

            Log.Logger.Information($"{fileName} found!");

            // Load PropertyClassDump.xml as document.
            XmlDocument xmlDoc = new();
            using StreamReader reader = new(dumpFilePath, Encoding.UTF8);
            xmlDoc.Load(reader);

            return xmlDoc;
        }

        private static string GetOrCreateOutputDirectory()
        {
            if (!Directory.Exists(OutputPath))
                Directory.CreateDirectory(OutputPath);

            return OutputPath;
        }

        private static CodeSnippetTypeMember CreateGenericPropertySnippet(string snippet)
            => new CodeSnippetTypeMember() { Text = snippet };

    }
}
