/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

﻿/* Copyright (C) Revive101 Development Team - All Rights Reserved
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential.
*/

using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Serilog;
using Serilog.Core;

namespace CacheGenerator.PropertyClass;

public static class PropertyClassGenerator {
    // The end goal is to use the Wizard101 type dump to generate C# classes.
    // You can find this dump by handing the `-x <file_name>` argument to the `WizardGraphicalClient.exe`.
    // The `csr.wad` *must* be present for proper authority.

    private const string NamespaceName = "Imlight.Common.Caches";
    private const string TypesClassName = "TypeCache";
    private const string DispatchTabs = "\t\t\t\t";
    private const string PropertyTabs = "\t\t\t";

    private static readonly string InputFolderPath = $"{Directory.GetCurrentDirectory()}/Input";
    private static readonly string OutputPath = $"{Directory.GetCurrentDirectory()}/Output";

    private static List<Definitions.ClassDef> _classDefinitions = new();
    private static List<Definitions.EnumDef> _enumDefinitions = new();
    private static List<Definitions.EnumDef> _standaloneEnumDefinitions = new();
    private static readonly List<string> HazardEnumNames = new()
    {
        "std.string",
        "@string",
        "string",
        "unsigned long",
        "unsigned int",
        "int"
    };

    private static readonly List<string> Imports = new()
    {
        "System",
        "SharpDX",
        "System.Collections.Generic",
        "Imlight.Common.ObjectProperty.PropertyReflection",
        "Imlight.Common.IO",
    };
    private static Logger Log { get; } = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateLogger();

    public static void Generate(XmlDocument wizardClientDefinitions) {
        Log.Warning("Beginning generation on PropertyClasses! This may take many moments");

        // Begin by grabbing and validating the dump xml from the Wizard101 client.
        SetClassDefs(wizardClientDefinitions);
        Log.Information("Successfully created all class definitions");

        // By this point, the generator has all Informationrmation needed. Initialize the CodeDom
        // and generate these classes into files.
        Log.Information("Starting CodeDom generation..");
        var compilers = CreateCodeDom();
        Log.Information("CodeDom generation complete!");

        Log.Information("Writing each class to file..");
        WriteCodeDomToDisk(compilers);
        Log.Information("CodeDom written to disk!");

        Log.Information("Generation complete!");
        Log.Warning("These classes might not be entirely valid. It's highly recommended to check" +
                           " the integrity of these files before using them practically");
    }

    private static void SetClassDefs(XmlDocument doc) {
        // There's only one base node here, labeled `ClassList`. Every child node will be a class definition.
        // We're going to create a ClassDef from each of these nodes.
        var classesList = doc.ChildNodes?[0]?.ChildNodes
                          ?? throw new Exception("Could not find class node. Are you sure this is the right file?");

        // Iterate through each of the nodes and create a ClassDef from it.
        var classes = new HashSet<Definitions.ClassDef>();
        for (var i = 0; i < classesList.Count; i++) {
            var xmlNode = classesList[i] ?? throw new NullReferenceException();
            var def = new Definitions.ClassDef(xmlNode);

            classes.Add(def);
        }

        _classDefinitions = classes.ToList();

        // All class definitions have been created. Now we must iterate through each one again
        // to set the more difficult properties. It's unreliable to do this prior, as some properties
        // rely on other class definitions already existing.
        foreach (var def in classes) {
            if (def.Name == "PropertyClass") {
                continue;
            }

            if (def.BaseName is null or "") {
                def.BaseName = "PropertyClass";
            }

            // Find and set the actual parent definition for this class.
            var parentDef = classes.FirstOrDefault(x => x.Name == def.BaseName);
            if (parentDef is not null) {
                def.BaseDef = parentDef;
            }
            else {
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
        _classDefinitions = _classDefinitions
            .GroupBy(x => x.Name)
            .Select(x => x.First())
            .ToList();
    }

    private static void RemoveOverriddenPropertiesFromDef(Definitions.ClassDef def) {
        // We're going to go up the inhertiance tree until we find the root.
        // Then, we'll take each property from those parent classes and compare them
        // to the current. If a property is shared between the class and any of it's parent classes,
        // it will be removed from the class.
        var parentProperties = new List<Definitions.PropertyDef>();
        var lastDef = def;
        while (true) {
            if (lastDef.BaseName is "" or null) {
                break;
            }

            var thisParentDef = _classDefinitions.FirstOrDefault(x => x.Name == lastDef.BaseName);
            if (thisParentDef is null) {
                break;
            }

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

    private static void SetOptionEnums() {
        // Iterate through every property of every class and find if it's an enum.
        // We'll add these enums to a list to then generate later.
        var enumDefs = new List<Definitions.EnumDef>();
        foreach (var classDef in _classDefinitions) {
            foreach (var propDef in classDef.Properties.Where(propDef => propDef.Options is not null)) {
                enumDefs.Add(propDef.Options!);

                // In some cases, an enum will not have a proper name. It will usually be a
                // primitive data type, such as `std::string` or `unsigned long`. If an enum
                // doesn't have a proper name, we can create a name for it using the name of the base class + property.
                //@TODO: Move this to the enum definition class.
                if (!HazardEnumNames.Contains(propDef.Options!.Name)) {
                    continue;
                }

                var scopedName = $"{classDef.Name}.{propDef.Name}";

                // Clean the name if possible.
                if (scopedName.Contains("m_")) {
                    // ex: ElixirBenefitEffectTemplate.m_flags
                    var idx = scopedName.IndexOf("m_", StringComparison.Ordinal);
                    scopedName = scopedName.Replace("m_", "");

                    // Capitalize the following char.
                    var letters = scopedName.ToCharArray();
                    letters[idx] = char.ToUpper(letters[idx]);
                    scopedName = new string(letters);
                }

                propDef.Options.Name = scopedName;

                Log.Warning("Renamed hazard enumerator in [{ClassDefName}] to: {ScopedName}", classDef.Name, scopedName);
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

        foreach (var item in enumDefs) {
            if (duplicateEnums.Any(x => x.Name == item.Name)) {
                item.DoNotWrite = true;
            }
        }

        _standaloneEnumDefinitions = duplicateEnums;
        _enumDefinitions = enumDefs;
    }

    private static void NestSubclasses(ref HashSet<Definitions.ClassDef> classDefs) {
        // Some classes will be named to their parent class. They must be declared inside their parent class definition.
        // For example: `MapInformationManager.MapInformation`

        var renameCollection = new List<Definitions.ClassDef>();
        foreach (var classDef in classDefs
                     .Where(classDef => classDef.Name.Contains('.'))
                     .ToList()) {
            // Get the parent class name.
            var idx = classDef.Name.LastIndexOf('.');
            var parentName = classDef.Name[..idx];
            var lastScopedDef = _classDefinitions.FirstOrDefault(x => x.Name == parentName);

            // If we're unable to find the parent class, it means the parent class does not inherit PropertyClass.
            // In such cases, we will rename the class to use underscores instead of dot notation.
            if (lastScopedDef is null) {
                renameCollection.Add(classDef);
                continue;
            }

            // Now that we've used the name, we can scope the class name to it's proper scope.
            // Rename the class to the proper scope.
            classDef.Name = classDef.Name.Split('.')[^1];

            // Add it to it's parent class definition and remove it from our definition list.
            lastScopedDef.SubClasses.Add(classDef);
            _classDefinitions.Remove(classDef);
        }

        // Do the same thing for the enumerators.
        foreach (var enumDef in _enumDefinitions.Where(enumDef => enumDef.Name.Contains('.'))) {
            var idx = enumDef.Name.LastIndexOf('.');
            var parentName = enumDef.Name[..idx];
            var lastScopedDef = _classDefinitions.FirstOrDefault(x => x.Name == parentName);

            //if (lastScopedDef == null) throw new Exception();

            // Rename the enum to the proper scope
            enumDef.Name = enumDef.Name.Split('.')[^1];
        }

        foreach (var entry in renameCollection) {
            var oldName = entry.Name;
            entry.Name = entry.Name.Replace('.', '_');
            Log.Warning($"Renamed hazardous class definition [{oldName}] to: {entry.Name}");
        }
    }

    private static void VerifyPropertyTypes(ref HashSet<Definitions.ClassDef> classDefs) {
        // Essay inbound. This is for future Jay who never wants to come back to this. Consider it a heritage gift.

        // This method is intended to be called in the last stage of class definition generation.
        // Some properties have their data type set to a meaningless type that no longer exists because:
        // a. We renamed it.
        // b. The original enum is not defined by Wizard101's type dump.
        // In such case, this method is intended to fix that.

        // To check the integrity of a data type:
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

        foreach (var def in classDefs.Where(x => x.Properties.Count > 0)) {
            foreach (var prop in def.Properties.Where(x => x.Type.Contains('.'))
                         .GroupBy(x => x.Type)
                         .Select(x => x.First())
                         .ToList()) {
                var workingTypeName = prop.Type;
                var isList = prop.Type.Contains("List<");
                if (isList) {
                    var idx1 = workingTypeName.IndexOf('<');
                    var idx2 = workingTypeName.IndexOf('>');
                    workingTypeName = workingTypeName[(idx1 + 1)..idx2];
                }

                var idx = workingTypeName.LastIndexOf(".", StringComparison.Ordinal);
                var typeName = workingTypeName[(idx + 1)..];
                var scopedTypeName = workingTypeName[..idx];
                var scopedTypeDef = _classDefinitions.FirstOrDefault(y => y.Name == scopedTypeName);

                // Failsafe; if a class is not found, replace '.' with '_'.
                if (scopedTypeDef == null) {
                    var found = false;
                    for (var i = 0; i < scopedTypeName.Count(c => c == '.') + 1; i++) {
                        var regx = new Regex(Regex.Escape("."));
                        var t = regx.Replace(scopedTypeName, "_", i + 1);
                        scopedTypeDef = _classDefinitions.FirstOrDefault(y => y.Name == t);
                        if (scopedTypeDef != null) {
                            found = true;
                            prop.Type = isList ? $"List<{typeName}>" : typeName;
                            break;
                        }
                    }
                    // If a parent class is not found, we're going to rename the type
                    // to match our naming conentions.
                    if (!found) {
                        workingTypeName = workingTypeName.Replace('.', '_');
                        prop.Type = isList ? $"List<{workingTypeName}>" : workingTypeName;
                        prop.Options = new Definitions.EnumDef(typeName);
                        continue;
                    }
                }

                // If the parent class has a definition already defined, skip it.
                if (scopedTypeDef!.Properties
                    .Where(x => x.Options != null)
                    .Any(p => p.Options.Name == typeName)) {
                    continue;
                }

                if (scopedTypeDef!.SubClasses
                    .Any(x => x.Name == typeName)) {
                    continue;
                }

                // If this enum isn't defined anywhere, remove the property that uses it.
                def.Properties.Remove(prop);
            }
        }
    }

    private static CodeMemberMethod GenerateDispatcherMethod() {
        if (_classDefinitions is null) {
            throw new NullReferenceException($"{nameof(_classDefinitions)} cannot be null.");
        }

        var codeMethod = new CodeMemberMethod() {
            Name = "Dispatch",
            ReturnType = new CodeTypeReference(typeof(Imlight.Common.ObjectProperty.PropertyReflection.PropertyClass)),
            Attributes = MemberAttributes.Static | MemberAttributes.Public,
        };
        var hashParam = new CodeParameterDeclarationExpression(typeof(uint), "hash");
        codeMethod.Parameters.Add(hashParam);

        // Create the start of this method. Then, iterate through the class definitions to create a case for each hash.
        var sb = new StringBuilder();
        sb.Append("switch (hash) {\n");
        foreach (var classDef in _classDefinitions.Where(classDef => classDef.Name != "PropertyClass")) {
            sb.Append($"{DispatchTabs}case {classDef.Hash}: return new {classDef.Name}();\n");
        }
        sb.Append($"{DispatchTabs}default: return null;\n");
        sb.Append($"            }}");

        var codeSwitchSnippet = new CodeSnippetExpression(sb.ToString());
        codeMethod.Statements.Add(codeSwitchSnippet);
        return codeMethod;
    }

    private static CodeCompileUnit CreateCodeDom() {
        // Initialize CodeDom.
        CodeCompileUnit codeCompileUnit = new();
        CodeNamespace codeNamespace = new(NamespaceName);

        // Declare code declaration. This will be the sealed base class in each file.
        // Every PropertyClass will be a subclass of this one.
        CodeTypeDeclaration fileBaseClassDecl = new(TypesClassName) {
            IsPartial = true,
            IsClass = true,
            TypeAttributes = TypeAttributes.Public
                             | TypeAttributes.Sealed
                             | TypeAttributes.BeforeFieldInit,
            Attributes = MemberAttributes.Public,
        };

        // Add imports to the namespace.
        foreach (var importDecl in Imports
                     .Select(import => new CodeNamespaceImport(import))) {
            codeNamespace.Imports.Add(importDecl);
        }

        // Add CodeDom together.
        codeCompileUnit.Namespaces.Add(codeNamespace);
        codeNamespace.Types.Add(fileBaseClassDecl);

        foreach (var classDef in _classDefinitions) {
            // We don't need to generate this, as it's manually typed.
            if (classDef.Name == "PropertyClass") {
                continue;
            }

            var classDecl = CreateDeclarationFromDefinition(classDef);
            fileBaseClassDecl.Members.Add(classDecl);
        }

        foreach (var enumDef in _standaloneEnumDefinitions) {
            var enumDecl = CreateDeclarationFromDefinition(enumDef);
            fileBaseClassDecl.Members.Add(enumDecl);
        }

        // Create dispatcher method.
        Log.Information("Creating dispatcher method..");
        var dispatch = GenerateDispatcherMethod();
        fileBaseClassDecl.Members.Add(dispatch);

        return codeCompileUnit;
    }

    private static void WriteCodeDomToDisk(CodeCompileUnit compiler) {
        var domProvider = CodeDomProvider.CreateProvider("CSharp");
        var outputPath = $"{GetOrCreateOutputDirectory()}/{TypesClassName}.cs";
        var options = new CodeGeneratorOptions {
            BracingStyle = "C",
            IndentString = "\t",
            ElseOnClosing = false,
        };
        using var writer = new StreamWriter(outputPath);
        domProvider.GenerateCodeFromCompileUnit(compiler, writer, options);

        // Find the line "public sealed partial class TypeCache" and add a static keyword. A limitation of CodeDom:
        // you cannot create static classes.
        var file = File.ReadAllLines(outputPath);
        var idx = file.ToList().FindIndex(x => x.Contains("public sealed partial class TypeCache"));
        file[idx] = file[idx].Replace("sealed", "static");
        File.WriteAllLines(outputPath, file);

        Log.Information($"Generated partial class to file {TypesClassName}.cs");
    }

    private static CodeTypeDeclaration CreateDeclarationFromDefinition(Definitions.ClassDef def, bool isSub = false) {
        var codeDecl = new CodeTypeDeclaration(def.Name);

        // Determine how many tabs must be applied, if this is a subclass.
        var bonusTabs = isSub ? "    " : "";

        // Add a base class, if one exists.
        if (def.BaseName is not null) {
            var properBaseName = def.BaseName;
            if (properBaseName == "Search.ResultItem") {
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
        foreach (var subDef in def.SubClasses) {
            var subDecl = CreateDeclarationFromDefinition(subDef, true);
            codeDecl.Members.Add(subDecl);
        }

        // Iterate through the properties.
        foreach (var propDef in def.Properties) {
            var snippet = CreateGenericPropertySnippet($"{PropertyTabs}{bonusTabs}{propDef}");
            codeDecl.Members.Add(snippet);

            // If this property has an attached enumerator, generate it here.
            if (propDef.Options is null
                || propDef.Options.DoNotWrite
                || propDef.Options.IsDefaultOrBaseClass) {
                continue;
            }

            var enumDef = CreateDeclarationFromDefinition(propDef.Options, true);
            codeDecl.Members.Add(enumDef);
        }

        return codeDecl;
    }

    private static CodeTypeDeclaration CreateDeclarationFromDefinition(Definitions.EnumDef def, bool isSub = false) {
        var enumDecl = new CodeTypeDeclaration(def.Name) {
            IsEnum = true
        };

        // Determine how many tabs must be applied, if this is a subclass.
        var bonusTabs = isSub ? "    " : "";

        // Add the [Flags] attribute if needed
        if (def.IsFlags) {
            var flagsRef = new CodeTypeReference(typeof(FlagsAttribute));
            var flagsAttr = new CodeAttributeDeclaration(flagsRef);
            enumDecl.CustomAttributes.Add(flagsAttr);
        }

        // Iterate through the options of this enum and add them as a field.
        if (def.Options == null) {
            return enumDecl;
        }

        foreach (var enumOptionDef in def.Options) {
            var snippet = (def.IsStringValued)
                ? CreateGenericPropertySnippet(
                    $"{PropertyTabs}{bonusTabs}{enumOptionDef.Name},")
                : CreateGenericPropertySnippet(
                    $"{PropertyTabs}{bonusTabs}{enumOptionDef.Name} = {enumOptionDef.Value},");

            enumDecl.Members.Add(snippet);
        }

        return enumDecl;
    }

    private static string GetOrCreateOutputDirectory() {
        if (!Directory.Exists(OutputPath)) {
            Directory.CreateDirectory(OutputPath);
        }

        return OutputPath;
    }

    private static CodeSnippetTypeMember CreateGenericPropertySnippet(string snippet) => new() { Text = snippet };

}
