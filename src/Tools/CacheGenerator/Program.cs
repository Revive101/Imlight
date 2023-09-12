

using System.Xml;
using CacheGenerator.Network;
using Imlight.Common.Formats;
using Serilog;
using Serilog.Core;

namespace CacheGenerator;

internal static class Program
{
    public static Logger Log { get; } = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateLogger();
    
    private const string RootWadName = "Root.wad";
    private const string TypeDumpName = "ClientTypeDump.xml";
    private const string ProtocolFileExtension = "Messages.xml";
    private const string NetworkMessageOutputPath = "NetworkMessages.cs";
    
    static void Main(string[] args)
    {
        if (!EnsureRootWadFromArguments(args))
            return;
        //if (!EnsureTypeDumpFromArguments(args))
        //    return;
        
        // Open a stream to the Root.wad file.
        var wadPath = args[0];
        var wadStream = GetFileStream(wadPath);
        if (wadStream is null)
            return;

        // Try to unpack the Root.wad
        var rootWad = GetRootWad(wadStream);
        if (rootWad is null)
            return;
        
        DoNetworkCacheGeneration(rootWad);
    }

    private static bool EnsureRootWadFromArguments(IReadOnlyList<string> args)
    {
        // The user should provide the path to the Root.wad file in the first argument.
        // Ensure that they've done that.
        if (args.Count >= 1)
        {
            var wadPath = args[0];
            if (File.Exists(wadPath))
                return true;

            Log.Fatal("Could not find file {File} by path {Path}!", RootWadName, wadPath);
            return false;
        }
        
        Log.Fatal("Please provide the path to the {File} file as the first argument", RootWadName);
        return false;
    }

    private static bool EnsureTypeDumpFromArguments(IReadOnlyList<string> args)
    {
        // The user should provide the path to the ClientTypeDump.xml file in the second argument.
        // Ensure that they've done that.
        if (args.Count >= 1)
        {
            var typeDumpPath = args[1];
            if (File.Exists(typeDumpPath))
                return true;

            Log.Fatal("Could not find file {File} by path {Path}!", TypeDumpName, typeDumpPath);
            return false;
        }
        
        Log.Fatal("Please provide the path to the {File} file as the first argument", TypeDumpName);
        return false;
    }

    private static void DoNetworkCacheGeneration(Wad rootWad)
    {
        Log.Information("Starting network cache generation...");
        
        var messageFileRecords = rootWad.Files
            .Where(x => x.Key.EndsWith(ProtocolFileExtension));

        if (!messageFileRecords.Any())
        {
            Log.Error("Could not find any files with the {Extension} extension in the {RootFile}!", 
                ProtocolFileExtension, RootWadName);
            return;
        }

        var xmlFiles = new List<XmlDocument>();
        foreach (var (name, protocol) in messageFileRecords)
        {
            Log.Debug("Opening file {File}...", name);
            
            var record = rootWad.OpenFile(name);
            if (record is null)
            {
                Log.Error("Could not open file {File} in the {RootFile}",
                    name, RootWadName);
                continue;
            }
            
            // Create an XML document from the file.
            var xmlDocument = new XmlDocument();
            xmlDocument.Load(record);
            xmlFiles.Add(xmlDocument);
        }
        
        // Generate C# classes from the XML document.
        Log.Information("Starting C# class generation of {FileCount} protocols...", xmlFiles.Count);

        // Create the output directory if it does not exist.
        Directory.CreateDirectory($"{Directory.GetCurrentDirectory()}/Output");
        var outputPath = $"{Directory.GetCurrentDirectory()}/Output/{NetworkMessageOutputPath}";
        var success = NetworkMessageGenerator.GenerateCSharpFromXmlProtocols(xmlFiles.ToArray(), outputPath);
        
        if (success)
            Log.Information("Finished C# class generation of {FileCount} protocols!", xmlFiles.Count);
        else
            Log.Error("Could not generate C# classes from the XML document!");
    }

    private static Stream? GetFileStream(string path)
    {
        if (!File.Exists(path))
        {
            Log.Fatal("Could not find file by path {Path}!", path);
            return null;
        }

        var stream = File.Open(path, FileMode.Open);
        return stream;
    }
    
    private static Wad? GetRootWad(Stream stream)
    {
        // Try to unpack the Root.wad
        Wad? rootWad;
        try
        {
            rootWad = new Wad(stream);
        }
        catch (Exception ex)
        {
            Log.Error("Could not unpack the {File} file! {Ex}", RootWadName, ex.Message);
            return null;
        }

        return rootWad;
    }
}