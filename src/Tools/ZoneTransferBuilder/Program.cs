/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

/*
 * Hello to all goons who may use this tool.
 *
 * Inside the build directory will be an /input/ directory. This is where you need to place the
 * `AccessPass.xml`, `serverdata` database, and any KIWAD necessary. When you've added a zone transfer result, it will
 * add it to the `serverdata` database you gave it. There is no output directory.
 */

using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using FuzzySharp;
using LiteDB;
using SharpDX;
using Spectre.Console;
using WizUnraveler.Cache;
using WizUnraveler.Formats;
using WizUnraveler.IO;
using static WizUnraveler.Secrets.ServerTypeCache;

namespace TriggerResultBuilder;

public class MatchResult
{
    public string Option { get; init; }
    public int Similarity { get; init; }
}

public static class Program
{
    private const string ZoneDataFileName = "gamedata.bin";
    private const string TriggerDataFileName = "triggers.xml";
    private const string ResultCollectionName = "zone_triggers";

    private static readonly string inputPath =
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "input");

    private static readonly string accessPassPath = Path.Combine(inputPath, "AccessPass.xml");
    private static readonly string serverDatabasePath = Path.Combine(inputPath, "serverdata");

    private static string[] zoneNames;

    public static void Main()
    {
        // Check if the server database exists and create a new one if not.
        if (!File.Exists(serverDatabasePath))
            Console.WriteLine($"The serverdata database was not found at path \"{serverDatabasePath}\". " +
                              $"A new one will be created.");
        else
            Console.WriteLine("Found serverdata database!");
    
        // Get the list of zone names from the AccessPass.
        zoneNames = GetAccessPassZones();
    
        // Select the zone.
        var zoneName = EnterWadInputSelection();

        var wad = GetWad(zoneName);
        AnsiConsole.MarkupLine($"[italic]Selected zone \"{zoneName}\".[/]");
    
        while (true)
        {
            // Select and handle the trigger.
            var triggerSelected = HandleTriggerSelection(zoneName, wad);
            if (triggerSelected == null) 
                throw new NullReferenceException("Trigger selected was null.");
    
            // Prompt the user to overwrite if this trigger already contains a `ResTeleport`.
            if (TriggerHasTeleportResult(zoneName, triggerSelected.m_triggerName))
            {
                var overwriteResult = AnsiConsole.Ask<string>(
                    "[italic]This trigger already has a zone transfer result! Do you want to overwrite it (y/n)?[/]");
                switch (overwriteResult)
                {
                    case "n":
                        return;
                    case "y":
                        DeleteExistingTeleportResult(zoneName, triggerSelected.m_triggerName);
                        break;
                }
            }
    
            // Begin the process of rebuilding the ResTeleport type.
            var result = RebuildZoneTransferResult(zoneName, triggerSelected.m_triggerName);
            InsertTeleportResult(zoneName, triggerSelected.m_triggerName, result);
        }
    }
    
    // Function to select and handle the trigger selection.
    private static Trigger HandleTriggerSelection(string zoneName, Wad wad)
    {
        AnsiConsole.MarkupLine($"You are in [bold]{zoneName}[/].");
        var triggers = GetWadTriggers(wad);
        var formattedTriggers = new List<string>();
    
        foreach (var t in triggers)
        {
            var hasTeleport = TriggerHasTeleportResult(zoneName, t.m_triggerName);
            var prefix = hasTeleport ? "✓" : "𐄂";
            formattedTriggers.Add($"({prefix}) {t.m_triggerName}");
        }
    
        var triggerSel = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a trigger:")
                .PageSize(10)
                .AddChoices(formattedTriggers));
        // Split at the first instance of the space character to trim off the prefix we created.
        var idx = triggerSel.IndexOf(' ');
        triggerSel = triggerSel.Substring(idx + 1).Trim();
        Console.WriteLine(triggerSel);
    
        return triggers.FirstOrDefault(x => x.m_triggerName == triggerSel);
    }
    
    private static bool TriggerHasTeleportResult(string zoneName, string triggerName)
    {
        var colName = SanitizeColName($"{ResultCollectionName}/{zoneName}/{triggerName}");
        using var db = new LiteDatabase(serverDatabasePath);
        var col= db.GetCollection<TypeCache.Result>(colName);
        return col.FindAll().Any();
    }
    
    private static void DeleteExistingTeleportResult(string zoneName, string triggerName)
    {
        var colName = SanitizeColName($"{ResultCollectionName}/{zoneName}/{triggerName}");
        using var db = new LiteDatabase(serverDatabasePath);
        var col= db.GetCollection<TypeCache.Result>(colName);
        col.DeleteAll();
    }
    
    private static void InsertTeleportResult(string zoneName, string triggerName, TypeCache.Result result)
    {
        var colName = SanitizeColName($"{ResultCollectionName}/{zoneName}/{triggerName}");
        using var db = new LiteDatabase(serverDatabasePath);
        var col= db.GetCollection<TypeCache.Result>(colName);
        col.Insert(result);
    }

    private static ResTeleport RebuildZoneTransferResult(string zoneName, string triggerName)
    {
        AnsiConsole.MarkupLine("\n[underline]Now begins the process of rebuilding the [bold]ResTeleport[/] type.[/]");
        AnsiConsole.MarkupLine("Write the name of the destination zone:");
        var destinationZoneName = EnterWadInputSelection();
        var destinationWad = GetWad(destinationZoneName);
        var destinationLocations = GetWadLocations(destinationWad);

        // Prompt the user to select a teleport location in the destination zone.
        var destinationLocation = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a teleport location in the destination zone:")
                .PageSize(10)
                .AddChoices(destinationLocations.Select(x => $"{x.m_locName} @ {x.m_location} dir: {x.m_direction}")));
        // Refactor the selection name to not include the coordinate flavor text.
        var destinationLocationStr = destinationLocation.Split('@')[^1];
        var destinationLocationDir = destinationLocation.Split("dir:")[^1].Trim();
        var destinationCoords = $"{ConvertVector3ToWizard(destinationLocationStr)},{destinationLocationDir}";

        // Write the selected information to the console.
        var panel = new Panel(
            $"Source Zone: {zoneName}\n" +
            $"Source Trigger: {triggerName}\n" +
            $"Destination Zone: {destinationZoneName}\n" +
            $"Destination Location: {destinationCoords}\n");
        panel.Header = new PanelHeader("ResTeleport");
        panel.Border = BoxBorder.Rounded;
        AnsiConsole.Write(panel);

        var result = new ResTeleport
        {
            m_destinationZone = destinationZoneName,
            m_destinationLoc = destinationCoords
        };

        while (true)
        {
            var confirmPrompt = AnsiConsole.Ask<string>("Type (y) to continue or (n) to restart.");
            switch (confirmPrompt)
            {
                case "y":
                    return result;
                case "n":
                    return RebuildZoneTransferResult(zoneName, triggerName);
                default:
                    continue;
            }
        }
    }
    
    private static string ConvertVector3ToWizard(string input)
    {
        // Split the input string into individual components
        var components = input.Trim().Split(' ');

        // Extract the numeric values for X, Y, and Z
        var x = ExtractValue(components[0]);
        var y = ExtractValue(components[1]);
        var z = ExtractValue(components[2]);

        return $"{x},{y},{z}";
    }

    private static float ExtractValue(string component)
    {
        // Split the component string by ':'
        var parts = component.Split(':');

        // Parse the value as a float using InvariantCulture to handle the decimal separator properly
        var value = float.Parse(parts[^1], CultureInfo.InvariantCulture);
        return value;
    }

    private static string EnterWadInputSelection()
    {
        var zoneName = AnsiConsole.Ask<string>("Enter the name of a zone, or use familiar terms to fuzzy find:");
        if (zoneNames.Contains(zoneName))
            return zoneName;

        // If we didn't find a match immediately, fuzzy find instead.
        var closestMatches = FindClosestMatches(zoneName, zoneNames);
        Console.WriteLine($"AccessPass does not contain a zone by the name \"{zoneName}\". " +
                          $"Closest matches:");
        zoneName = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a wizard zone")
                .PageSize(10)
                .AddChoices(closestMatches.Select(x => $"({x.Similarity}%): {x.Option}")));

        // Refactor the zone name to not include percentage prefix.
        zoneName = zoneName.Split(' ')[^1];
        
        // Check to see if the selected KIWAD exists in the input directory.
        var properWadName = zoneName.Replace('/', '-');
        var checkPath = $"{inputPath}/{properWadName}.wad";
        if (File.Exists(checkPath)) 
            return zoneName;
        
        throw new Exception($"There is no KIWAD by name \"{checkPath}\" in your input directory.");
    }

    private static Wad GetWad(string wadName)
    {
        wadName = wadName.Replace('/', '-');
        var path = $"{inputPath}/{wadName}.wad";
        if (!File.Exists(path))
        {
            Console.WriteLine($"Could not find KIWAD in the input directory by name \"{wadName}\".");
            return null;
        }

        var fs = File.ReadAllBytes(path);
        var ms = new MemoryStream(fs);
        return new Wad(ms);
    }

    private static IEnumerable<MatchResult> FindClosestMatches(string userInput, IEnumerable<string> options)
    {
        const int threshold = 20; // Adjust the similarity threshold as needed (0-100).
        var closestMatches = new List<MatchResult>();

        foreach (var option in options)
        {
            var similarity = Fuzz.PartialRatio(userInput, option);
            if (similarity >= threshold)
                closestMatches.Add(new MatchResult { Option = option, Similarity = similarity });
        }

        closestMatches.Sort((x, y) => y.Similarity.CompareTo(x.Similarity));
        return closestMatches.Take(200).ToList();
    }

    private static Trigger EnterTriggerInputSelection(Trigger[] triggers)
    {
        var formattedTriggers = new List<string>();
        foreach (var t in triggers)
        {
            // Create a check or X prefix declaring if this trigger has a `ResTeleport` already.
            var hasTeleport = t.m_results.m_results.Any(x => x is ResTeleport);
            var prefix = hasTeleport ? "✓" : "𐄂";
            formattedTriggers.Add($"({prefix}) {t.m_triggerName}");
        }
        
        var triggerSel = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a trigger")
                .PageSize(10)
                .AddChoices(formattedTriggers));
        // Narrow our trigger selection to exclude the prefix we created for it.
        triggerSel = triggerSel.Split(" ")[^1].Trim();

        return triggers.FirstOrDefault(x => x.m_triggerName == triggerSel);
    }

    private static string[] GetAccessPassZones()
    {
        var stream = GetFileStream(accessPassPath);
        if (stream is null)
            throw new NullReferenceException($"AccessPass.xml was not found at path {accessPassPath}.");

        var zoneList = new List<string>();
        var zoneCounter = 0;
        var doc = new XmlDocument();
        doc.Load(stream);

        foreach (XmlNode zoneNode in doc.GetElementsByTagName("Zone"))
        {
            var zoneName = zoneNode.InnerText;
            zoneList.Add(zoneName);
            zoneCounter++;
        }

        Console.WriteLine($"Loaded {zoneCounter} zones.");

        return zoneList.ToArray();
    }

    private static MemoryStream GetFileStream(string path)
    {
        if (!File.Exists(path))
            return null;

        var fs = File.ReadAllBytes(path);
        var ms = new MemoryStream(fs);
        ms.Position = 0;

        return ms;
    }

    private static Trigger[] GetWadTriggers(Wad wad)
    {
        var fs = new FileSerializer();
        var triggers = fs.OpenClass<WizZoneTriggers>(wad, TriggerDataFileName);

        return triggers?.m_triggers?
            .Where(trigger => trigger.m_results?.m_results != null)
            .Where(trigger => trigger.m_results.m_results.Any(result => result is TypeCache.ResTeleport))
            .ToArray();
    }

    private static IEnumerable<TypeCache.LocationTemplate> GetWadLocations(Wad wad)
    {
        var fs = new FileSerializer();
        return fs.OpenClass<TypeCache.WizZoneData>(wad, ZoneDataFileName).m_locationList;
    }
    
    private static string SanitizeColName(string colName)
    {
        // Use regular expression to remove any character that isn't an alphabet character or an underscore.
        return Regex.Replace(colName, @"[^a-zA-Z_]", "");
    }
}