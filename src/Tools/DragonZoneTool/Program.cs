/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Globalization;
using DragonZoneTool.Managers;
using FuzzySharp;
using Imlight.Common.Formats;
using Imlight.Common.Serializable;
using Imlight.Common.Serializable.Caches;
using Imlight.Common.Serializable.Secrets;
using Serilog;
using Serilog.Core;
using Spectre.Console;

namespace DragonZoneTool;

public static class Program
{
    private const int FuzzyFindThreshold = 20;
    private const string ZoneDataFileName = "gamedata.bin";
    private const string TriggerDataFileName = "triggers.xml";
    
    private static string[] _zoneNames;
    private static Stack<Wad> _wadStack = new();
    
    public static void Main()
    {
        if (!AreAllResourcesAvailable())
            return;

        Console.Write("Connect to Imlight? (y/n) ");
        var userSettingsInput = Console.ReadLine();
        if (userSettingsInput is null)
            return;

        if (userSettingsInput == "y")
        {
            DragonDatabaseManager.SetRemoteServer("https://a.worlddata.ravendb.community", "input/worlddata.dev.certificate.pfx");
        }
        else
        {

            Console.WriteLine("Enter the remote database URL, or a local path to an embedded database:");
            var userDatabaseInput = Console.ReadLine();
            if (userDatabaseInput is null)
                return;
            if (userDatabaseInput.StartsWith("http"))
            {
                Console.WriteLine("Using remote database. Enter the path to your certificate:");
                var userCertificateInput = Console.ReadLine();
                if (userCertificateInput is null)
                    return;
                DragonDatabaseManager.SetRemoteServer(userDatabaseInput, userCertificateInput);
            }
            else
            {
                DragonDatabaseManager.SetEmbeddedServer(userDatabaseInput);
            }
        }
        
        // Start the WAD input process.
        var workingKiwad = DoWadInput();
        _wadStack.Push(workingKiwad);
        
        WorkLoop();
    }

    private static void WorkLoop()
    {
        while (true)
        {
            var workingWad = _wadStack.Peek();
            AnsiConsole.MarkupLine($"You are in [bold]{workingWad.Name}[/].");
            
            // Prompt the user to select a trigger from the current WAD.
            var workingTrigger = DoTriggerInput(workingWad);
            
            if (workingTrigger == null)
                continue;

            var result = RebuildZoneTransferResult(workingWad.Name, workingTrigger.m_triggerName);
            DragonDatabaseManager.AddNewTeleport(workingWad.Name, workingTrigger.m_triggerName, result);
        }
    }

    private static ServerTypeCache.Trigger? DoTriggerInput(Wad wad)
    {
        // Get the triggers contained in this KIWAD, then format them for the user.
        // If a trigger has an existing teleport, mark it with a checkmark.
        var triggers = GetWadTriggers(wad);
        var formatTriggers = FormatTriggers(wad.Name, ref triggers);
        
        // If our wad stack is more than one deep, allow the user to go back.
        if (_wadStack.Count > 1)
            formatTriggers.Add("Crawl back to the previous working zone.");
        
        var rawTriggerSelected = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select a trigger:")
            .PageSize(10)
            .AddChoices(formatTriggers));

        if (rawTriggerSelected == "Crawl back to the previous working zone.")
        {
            _wadStack.Pop();
            return null;
        }
        
        // Split at the first instance of the space character to trim off the prefix we created.
        var idx = rawTriggerSelected.IndexOf(' ');
        rawTriggerSelected = rawTriggerSelected.Substring(idx + 1).Trim();
        var triggerSelected = triggers.FirstOrDefault(x => x.m_triggerName == rawTriggerSelected);

        // If the trigger does not have an existing teleport, return it.
        var existingTeleport 
            = DragonDatabaseManager.GetExistingTeleport(wad.Name, triggerSelected.m_triggerName);
        if (existingTeleport is null)
            return triggerSelected;
        
        // Otherwise, prompt the user to overwrite the existing teleport.
        var overwriteResult = AnsiConsole.Ask<string>($"[italic]This trigger already leads " +
                                                      $"to [bold]{existingTeleport.Teleport.m_destinationZone}[/]. " +
                                                      "Overwrite (y), crawl (c), or cancel (n)?[/]");
        switch (overwriteResult)
        {
            case "n": return null;
            case "y":
                DragonDatabaseManager.DeleteExistingTeleport(wad.Name, triggerSelected.m_triggerName);
                return triggerSelected;
            case "c":
                var pushWad = PatchServerManager.DownloadWad(existingTeleport.Teleport.m_destinationZone);
                pushWad.Name = existingTeleport.Teleport.m_destinationZone;
                _wadStack.Push(pushWad);
                return null;
            default: return null;
        }
    }
    
    private static Wad DoWadInput()
    {
        // Get the zone name from the user. Then, download the WAD.
        var zoneName = GetWadInputString();
        
        // If the wad stack contains this zone, just return that.
        if (_wadStack.Any(x => x.Name == zoneName))
        {
            return _wadStack.First(x => x.Name == zoneName);
        }
        
        var wad = PatchServerManager.DownloadWad(zoneName);
        wad.Name = zoneName;
        
        return wad;
    }
    
    private static string GetWadInputString()
    {
        // Iterate until we get a valid file.
        while (true)
        {
            var zoneName = AnsiConsole
                .Ask<string>("Enter the name of a zone, or use familiar terms to fuzzy find:");
            
            // Return the zone name if the user typed it exactly.
            if (_zoneNames.Contains(zoneName)) 
                return zoneName;

            // If we didn't find a match immediately, fuzzy find instead.
            var closestMatches = Fuzzy.FindClosestMatches(zoneName, _zoneNames);
            Console.WriteLine($"AccessPass does not contain a zone by the name \"{zoneName}\". " + $"Closest matches:");

            // Craft the list of selections.
            var selections = new List<string> { "Retry search" };
            selections.AddRange(closestMatches.Select(match => $"({match.Similarity}%): {match.Option}"));

            zoneName = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select a wizard zone")
                .PageSize(10)
                .AddChoices(selections));

            if (zoneName == "Retry search")
                continue;

            // Refactor the zone name to not include percentage prefix.
            return zoneName.Split(' ')[^1];
        }
    }
    
    private static IEnumerable<ServerTypeCache.Trigger>? GetWadTriggers(Wad wad)
    {
        var fs = new FileSerializer();
        var triggers = fs.OpenClass<ServerTypeCache.WizZoneTriggers>(wad, TriggerDataFileName);

        return triggers?.m_triggers?
            .Where(trigger => trigger.m_results?.m_results != null)
            .Where(trigger => trigger.m_results.m_results.Any(result => result is TypeCache.ResTeleport))
            .ToArray();
    }
    
    private static List<string> FormatTriggers(string zoneName, ref IEnumerable<ServerTypeCache.Trigger> triggers)
    {
        var zoneData = DragonDatabaseManager.GetZoneData(zoneName);
        if (zoneData is null)
            return triggers.Select(x => $"X {x.m_triggerName}").ToList();
        
        var formattedTriggers = new List<string>();
        foreach (var t in triggers)
        {
            var hasTeleport = zoneData.Teleports.Any(x => x.TriggerName == t.m_triggerName);
            var prefix = hasTeleport ? "✔️" : "X";
            formattedTriggers.Add($"{prefix} {t.m_triggerName}");
        }
        
        return formattedTriggers;
    }
    
    private static IEnumerable<TypeCache.LocationTemplate> GetWadLocations(Wad wad)
    {
        var fs = new FileSerializer();
        return fs.OpenClass<TypeCache.WizZoneData>(wad, ZoneDataFileName).m_locationList;
    }
    
    private static ServerTypeCache.ResTeleport RebuildZoneTransferResult(string zoneName, string triggerName)
    {
        AnsiConsole.MarkupLine("\n[underline]Now begins the process of rebuilding the [bold]ResTeleport[/] type.[/]");
        AnsiConsole.MarkupLine("Write the name of the destination zone:");
        var destinationWad = DoWadInput();
        var destinationLocations = GetWadLocations(destinationWad);

        // Prompt the user to select a teleport location in the destination zone.
        var destinationLocation = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a teleport location in the destination zone:")
                .PageSize(10)
                .AddChoices(destinationLocations.Select(x => $"{x.m_locName} @ {x.m_location} dir: {x.m_direction}")));
        // Refactor the selection name to not include the coordinate flavor text.
        var destinationLocationStr = destinationLocation.Split('@')[^1].Replace(",",".");
        var destinationLocationDir = destinationLocation.Split("dir:")[^1].Trim().Replace(",", ".");
        var destinationCoords = $"{ConvertVector3ToWizard(destinationLocationStr)},{destinationLocationDir}";

        // Write the selected information to the console.
        var panel = new Panel(
            $"Source Zone: {zoneName}\n" +
            $"Source Trigger: {triggerName}\n" +
            $"Destination Zone: {destinationWad.Name}\n" +
            $"Destination Location: {destinationCoords}\n");
        panel.Header = new PanelHeader("ResTeleport");
        panel.Border = BoxBorder.Rounded;
        AnsiConsole.Write(panel);

        var result = new ServerTypeCache.ResTeleport
        {
            m_destinationZone = destinationWad.Name,
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
    
    private static bool AreAllResourcesAvailable()
    {
        try
        {
            _zoneNames = AccessPassManager.GetAccessPassZones();
            if (_zoneNames.Length <= 0)
                throw new Exception("No zones found in AccessPass.");
            if (!PatchServerManager.IsPatchServerAvailable())
                throw new Exception("Patch server is not available.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Some resource was not available: {Ex}", ex.Message);
            return false;
        }

        return true;
    }
    
    private static string ConvertVector3ToWizard(string input)
    {
        // Split the input string into individual components
        var components = input.Trim().Split(' ');

        // Extract the numeric values for X, Y, and Z
        var x = ExtractValue(components[0]).ToString().Replace(",",".");
        var y = ExtractValue(components[1]).ToString().Replace(",", ".");
        var z = ExtractValue(components[2]).ToString().Replace(",", ".");

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
}
