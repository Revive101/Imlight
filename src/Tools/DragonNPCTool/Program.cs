/* Copyright (C) Revive101 Development Team - All Rights Reserved
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential.
*/

using Spectre.Console;
using DragonNPCTool.Managers;
using DragonNPCTool.Models;
using Imlight.Common.Caches;
using Imlight.Common.Formats;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;

namespace DragonNPCTool;

public static class Program {
    private const int FuzzyFindThreshold = 20;
    private const string ZoneDataFileName = "gamedata.bin";
    private const string InputDirectory = "input";
    private const string CertificateName = "worlddata.dev.certificate.pfx";
    private const string ShopkeeperNameManifest = "shopkeeper.manifest";

    private static string[] _zoneNames;
    private static Dictionary<ulong, string> s_shopKeeperNames;

    public static void Main() {
        if (!AreAllResourcesAvailable())
            return;
        if (!LoadShopkeeperNames())
            return;

        Console.Write("Connect to Imlight? (y/n) ");
        var userSettingsInput = Console.ReadLine();
        if (userSettingsInput is null)
            return;

        if (userSettingsInput == "y") {
            DragonDatabaseManager.SetRemoteServer("https://a.worlddata.ravendb.community", $"{InputDirectory}/{CertificateName}");
        }
        else {

            Console.WriteLine("Enter the remote database URL, or a local path to an embedded database:");
            var userDatabaseInput = Console.ReadLine();
            if (userDatabaseInput is null)
                return;
            if (userDatabaseInput.StartsWith("http")) {
                Console.WriteLine("Using remote database. Enter the path to your certificate:");
                var userCertificateInput = Console.ReadLine();
                if (userCertificateInput is null)
                    return;
                DragonDatabaseManager.SetRemoteServer(userDatabaseInput, userCertificateInput);
            }
            else {
                DragonDatabaseManager.SetEmbeddedServer(userDatabaseInput);
            }
        }

        AnsiConsole.MarkupLine("\nDragonNPCTool | Revive101\nUse 'help' to see usage.");
        WorkLoop();
    }

    private static void WorkLoop() {
        while (true) {
            AnsiConsole.Markup("> ");
            var input = Console.ReadLine();

            switch (input) {
                case "help":
                    CommandHelp();
                    break;
                case "wad":
                    CommandWad();
                    break;
                case "npc":
                    CommandNpc();
                    break;
                case "exit":
                    return;
                default:
                    break;
            }
        }
    }

    private static void CommandHelp() {
        AnsiConsole.MarkupLine("Commands:");
        AnsiConsole.MarkupLine("\thelp - Display this help message.");
        AnsiConsole.MarkupLine("\twad - Download and display possible shopkeeping NPCs within a zone WAD.");
        AnsiConsole.MarkupLine("\tnpc - Select an NPC template ID to create/modify an inventory for them.");
        AnsiConsole.MarkupLine("\texit - Exit the program.");
    }

    private static void CommandWad() {
        KiWad workingWad;
        FileSerializer fs = new FileSerializer();
        List<CoreObjectInfo> objectList, npcSuspects;

        workingWad = DoWadInput();

        AnsiConsole.MarkupLine($"\nYou are in [bold]{workingWad.Name}[/]. Double check the following IDs!");

        var zoneData = fs.OpenClass<TypeCache.WizZoneData>(workingWad, ZoneDataFileName);
        if (zoneData is not null) {
            // Search the manifest for shopkeepers. If none are found, search the object list.
            objectList = zoneData.m_objectList;
            var npcList = FindShopSuspectObjects(objectList);

            var npcFound = false;
            var printedTemplateIds = new HashSet<ulong>();

            foreach (var obj in objectList)
            {
                if (s_shopKeeperNames.ContainsKey(obj.m_templateID) && !printedTemplateIds.Contains(obj.m_templateID))
                {
                    npcFound = true;
                    AnsiConsole.MarkupLine($"[bold]{obj.m_templateID}[/] -{s_shopKeeperNames[obj.m_templateID]}");
                    printedTemplateIds.Add(obj.m_templateID);
                }
            }

            foreach (var npc in npcList)
            {
                if (!printedTemplateIds.Contains(npc.m_templateID))
                {
                    npcFound = true;
                    AnsiConsole.MarkupLine($"[bold]{npc.m_templateID}[/] - {npc.m_zoneTag}");
                    printedTemplateIds.Add(npc.m_templateID);
                }
            }

            if (!npcFound)
            {
                AnsiConsole.MarkupLine("No shopkeepers found in this zone. Dumping all possible NPCs instead.");

                foreach (var obj in objectList) {
                    AnsiConsole.MarkupLine($"[bold]{obj.m_templateID}[/] - {obj.m_zoneTag}");
                }
            }
        }
    }

    private static void CommandNpc() {
        List<GID> inventory = new List<GID>();

        AnsiConsole.Markup("\nInput the TemplateID of the shopkeeper: ");
        var templateId = Convert.ToUInt64(Console.ReadLine());

        var getInventorySuccess = DragonDatabaseManager.TryGetNpcInventory(templateId, out var existingInventory);
        if (getInventorySuccess) {
            AnsiConsole.MarkupLine($"Inventory for NPC with TemplateID {templateId} already exists. Current inventory:");

            AnsiConsole.MarkupLine("      idx | template ID");
            for (int i = 0; i < existingInventory.Inventory.Count; i++) {
                AnsiConsole.MarkupLine($"\t[bold]{i + 1} | {Convert.ToUInt64(existingInventory.Inventory[i])}[/]");
            }

            AnsiConsole.Markup($"Do you want to modify, overwrite, or skip this inventory? (mod/ow/skip): ");
            var input = Console.ReadLine();

            if (input == "skip") {
                return;
            }

            if (input == "mod") {
                inventory = ModifyInventory(existingInventory.Inventory);
            } else {
                inventory = CreateNewInventory(templateId);
            }
        } else {
            inventory = CreateNewInventory(templateId);
        }

        UpdateInventoryDatabase(templateId, inventory);
    }

    private static List<GID> CreateNewInventory(ulong templateId) {
        List<GID> inventory = new List<GID>();

        AnsiConsole.Markup("\nInput the TemplateID of an item to add to the inventory: ");
        var input = Console.ReadLine();

        if (!ulong.TryParse(input, out var itemID)) {
            AnsiConsole.MarkupLine("Invalid input. Please input a valid TemplateID.");
        } else {
            inventory.Add((GID) itemID);
        }

        while (true) {
            AnsiConsole.Markup("\nInput the next item, type 'undo' to remove the last item, or type 'y' to finalize: ");
            input = Console.ReadLine();

            if (input == "y") break;

            if (input == "undo") {
                inventory.RemoveAt(inventory.Count - 1);
                AnsiConsole.MarkupLine($"Removed previous item {itemID}.");
                continue;
            }

            if (!ulong.TryParse(input, out itemID)) {
                AnsiConsole.MarkupLine("Invalid input. Please input a valid TemplateID.");
            }
            else {
                inventory.Add((GID) itemID);
                AnsiConsole.Markup($"Added item {itemID}.");
            }
        }

        return inventory;
    }

    private static List<GID> ModifyInventory(List<GID> inventory) {
        while (true) {
            AnsiConsole.Markup("Use 'remove <idx>' to remove item, 'add <ID>' to add item, or type 'y' to finalize: ");
            var input = Console.ReadLine();

            if (input == "y") return inventory;

            if (input.Contains("add")) {
                input = input.Replace("add ", "");

                if (!ulong.TryParse(input, out var itemID)) {
                    AnsiConsole.MarkupLine("Invalid input. Please input a valid TemplateID.");
                }
                else {
                    inventory.Add((GID) itemID);
                    AnsiConsole.MarkupLine($"Added item {itemID}.");
                }
            } else {
                input = input.Replace("remove ", "");

                if (!ulong.TryParse(input, out var index)) {
                    AnsiConsole.MarkupLine("Invalid input. Please input a valid index.");
                }
                else {
                    var val = inventory[(int) index];
                    inventory.RemoveAt((int) index - 1);
                    AnsiConsole.MarkupLine($"Removed item {Convert.ToUInt64(val)}. New inventory:");

                    AnsiConsole.MarkupLine("      idx | template ID");
                    for (int i = 0; i < inventory.Count; i++) {
                        AnsiConsole.MarkupLine($"\t[bold]{i + 1} | {Convert.ToUInt64(inventory[i])}[/]");
                    }
                }
            }
        }
    }

    private static void UpdateInventoryDatabase(ulong templateId, List<GID> inventory) {
        var finalInventory = new NPCInventory {
            TemplateID = templateId,
            Inventory = inventory
        };

        var getInventorySuccess = DragonDatabaseManager.TryGetNpcInventory(templateId, out var existingInventory);

        if (getInventorySuccess) {
            DragonDatabaseManager.UpdateNpcInventory(finalInventory);
        }
        else {
            DragonDatabaseManager.AddNpcInventory(finalInventory);
        }

        AnsiConsole.MarkupLine("Database updated successfully!");
    }

    private static bool AreAllResourcesAvailable() {
        try {
            _zoneNames = Managers.AccessPassManager.GetAccessPassZones();
            if (_zoneNames.Length <= 0)
                throw new Exception("No zones found in AccessPass.");
            if (!PatchServerManager.IsPatchServerAvailable())
                throw new Exception("Patch server is not available.");
        }
        catch (Exception ex) {
            Console.WriteLine("Some resource was not available: {0}", ex.Message);
            return false;
        }

        return true;
    }

    private static bool LoadShopkeeperNames() {
        s_shopKeeperNames = new Dictionary<ulong, string>();

        // The manifest is sorted by key | value, where key is the template ID and value is the shopkeeper name.
        var inputFile = $"{InputDirectory}/{ShopkeeperNameManifest}";
        if (!File.Exists(inputFile)) {
            Console.WriteLine("Shopkeeper name manifest not found.");
            return false;
        }

        var lines = File.ReadAllLines(inputFile);
        foreach (var line in lines) {
            var split = line.Split('|');
            if (split.Length != 2) {
                Console.WriteLine("Shopkeeper name manifest is not formatted correctly.");
                return false;
            }

            var templateId = Convert.ToUInt64(split[0]);
            var shopkeeperName = split[1];
            s_shopKeeperNames.Add(templateId, shopkeeperName);
        }

        return true;
    }

    private static KiWad DoWadInput() {
        // Get the zone name from the user. Then, download the WAD.
        var zoneName = GetWadInputString();

        var wad = PatchServerManager.DownloadWad(zoneName);
        wad.Name = zoneName;

        return wad;
    }

    private static string GetWadInputString() {
        // Iterate until we get a valid file.
        while (true) {
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

    private static List<CoreObjectInfo> FindShopSuspectObjects(List<CoreObjectInfo> objectList) {
        List<CoreObjectInfo> shopSuspectObjects = new List<CoreObjectInfo>();

        foreach (var objectInfo in objectList
                                             .Where(info => info != null)
                                             .Where(info => info.m_zoneTag != null)
                                             .Where(info => info is not CombatSigil)
                                             .Where(info => info is not SoundEmitterInfo)
                                             .Where(info => info is not PositionalSoundEmitterInfo)) {
            var name = objectInfo.m_zoneTag.ToString();

            // Check to see if the name contains either "shop" or "npc".
            if (name.Contains("shop", StringComparison.OrdinalIgnoreCase) || name.Contains("npc", StringComparison.OrdinalIgnoreCase)) {
                shopSuspectObjects.Add(objectInfo);
            }
        }

        return shopSuspectObjects;
    }
}
