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
    private const string CreatureSpellbookManifest = "deck.manifest";

    private static string[] _zoneNames;
    private static Dictionary<ulong, string> s_shopKeeperNames;
    private static Dictionary<string, List<string>> s_creatureSpellbookNames;

    public static void Main() {
        if (!AreAllResourcesAvailable())
            return;
        if (!LoadShopkeeperNames())
            return;
        if (!LoadCreatureDeckNames())
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
                case "vendor":
                    CommandVendor();
                    break;
                case "trainer":
                    CommandTrainer();
                    break;
                case "creature":
                    CommandCreature();
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
        AnsiConsole.MarkupLine("\tvendor - Select an NPC template ID to create/modify an inventory for them.");
        AnsiConsole.MarkupLine("\ttrainer - Select an NPC template ID to create/modify a spell inventory for them.");
        AnsiConsole.MarkupLine("\tcreature - Select an enemy/mob/boss deck to create/modify a spellbook for it.");
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
                if (obj is null) {
                    continue;
                }

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

    private static void CommandVendor() {
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

            if (input == "mod") {
                inventory = ModifyInventory(existingInventory.Inventory);

                UpdateInventoryDatabase(templateId, inventory);
            }

            if (input == "ow") {
                inventory = CreateNewInventory(templateId);

                UpdateInventoryDatabase(templateId, inventory);
            }
        } else {
            inventory = CreateNewInventory(templateId);

            UpdateInventoryDatabase(templateId, inventory);
        }
    }

    private static void CommandTrainer() {
        List<NPCSpellEntry> spellInventory = new List<NPCSpellEntry>();

        AnsiConsole.Markup("\nInput the TemplateID of the trainer: ");
        var input = Console.ReadLine();

        if (!uint.TryParse(input, out var templateId)) {
            AnsiConsole.MarkupLine("Invalid input. Please input a valid TemplateID.");
            return;
        }

        var getSpellInventorySuccess = DragonDatabaseManager.TryGetNpcSpellInventory(templateId, out var existingSpellInventory);
        if (getSpellInventorySuccess) {
            AnsiConsole.MarkupLine($"Spell inventory for NPC with TemplateID {templateId} already exists. Current inventory:");

            AnsiConsole.MarkupLine("\tidx | template ID");
            for (int i = 0; i < existingSpellInventory.Spells.Count; i++) {
                AnsiConsole.MarkupLine($"\t[bold]  {i + 1} | {existingSpellInventory.Spells[i].TemplateID}[/]");
                AnsiConsole.MarkupLine($"\t\t[bold]Required Spell ID: {existingSpellInventory.Spells[i].RequiredSpellID}[/]");
                AnsiConsole.MarkupLine($"\t\t[bold]Required Level: {existingSpellInventory.Spells[i].Level}[/]");
            }

            AnsiConsole.Markup($"Do you want to modify, overwrite, or skip this inventory? (mod/ow/skip): ");
            input = Console.ReadLine();

            if (input == "mod") {
                spellInventory = ModifySpellInventory(existingSpellInventory);

                UpdateSpellInventoryDatabase(templateId, spellInventory);
            }

            if (input == "ow") {
                spellInventory = CreateNewSpellInventory(templateId);

                UpdateSpellInventoryDatabase(templateId, spellInventory);
            }
        }
        else {
            spellInventory = CreateNewSpellInventory(templateId);

            UpdateSpellInventoryDatabase(templateId, spellInventory);
        }
    }

    public static void CommandCreature() {
        var spellList = new List<uint>();

        AnsiConsole.Markup("\nInput the name of the creature: ");
        var input = Console.ReadLine();

        if (input is null) {
            return;
        }

        if (s_creatureSpellbookNames.TryGetValue(input, out var decks)) {
            if (decks.Count == 0) {
                AnsiConsole.MarkupLine("No decks found for this creature.");
                return;
            }

            AnsiConsole.MarkupLine($"Available decks for creatures with name {input}:");
            AnsiConsole.MarkupLine("\tidx | deck names");
            for (int i = 0; i < decks.Count; i++) {
                AnsiConsole.MarkupLine($"\t[bold]  {i + 1} | {decks[i]}[/]");
            }

            AnsiConsole.Markup("\nInput the index of the deck to modify: ");
            input = Console.ReadLine();

            if (!int.TryParse(input, out var deckIndex)) {
                AnsiConsole.MarkupLine("Invalid input. Please input a valid index.");
                return;
            }

            if (deckIndex - 1 >= decks.Count || deckIndex - 1 <= 0) {
                AnsiConsole.MarkupLine("Invalid input. Index out of range.");
                return;
            }

            var deckName = decks[deckIndex - 1];
            DragonDatabaseManager.TryGetCreatureSpellbook(deckName, out var deck);

            if (deck is null) {
                AnsiConsole.MarkupLine($"Deck '{deckName}' not found in the database. Creating creature deck..");
                spellList = CreateCreatureSpellbook(deckName);

                UpdateCreatureSpellbookDatabase(deckName, spellList);
            }
            else {
                AnsiConsole.MarkupLine($"Current spellbook for {deckName}:");
                AnsiConsole.MarkupLine("\tidx | spell ID");
                for (int i = 0; i < deck.SpellTemplateIds.Length; i++) {
                    AnsiConsole.MarkupLine($"\t[bold]  {i + 1} | {deck.SpellTemplateIds[i]}[/]");
                }

                AnsiConsole.Markup($"Do you want to modify, overwrite, or skip this creature deck? (mod/ow/skip): ");
                input = Console.ReadLine();

                if (input == "mod") {
                    spellList = ModifyCreatureSpellbook(deck.SpellTemplateIds.ToList());

                    UpdateCreatureSpellbookDatabase(deckName, spellList);
                }

                if (input == "ow") {
                    spellList = CreateCreatureSpellbook(deckName);

                    UpdateCreatureSpellbookDatabase(deckName, spellList);
                }
            }
        }
        else {
            AnsiConsole.MarkupLine($"No creature with name '{input}' found.\n");
        }
    }

    #region VendorInventory
    private static List<GID> CreateNewInventory(ulong templateId) {
        List<GID> inventory = new List<GID>();

        while (true) {
            AnsiConsole.Markup("\nInput the item TemplateID, type 'undo' to remove the last item, or type 'y' to finalize: ");
            var input = Console.ReadLine();

            if (input == "y") {
                return inventory;
            }

            if (input == "undo") {
                inventory.RemoveAt(inventory.Count - 1);
                AnsiConsole.MarkupLine($"Removed previous item.");
                continue;
            }

            if (!ulong.TryParse(input, out var itemID)) {
                AnsiConsole.MarkupLine("Invalid input. Please input a valid TemplateID.");
            }
            else {
                inventory.Add((GID) itemID);
                AnsiConsole.Markup($"Added item {itemID}.");
            }
        }
    }

    private static List<GID> ModifyInventory(List<GID> inventory) {
        while (true) {
            AnsiConsole.Markup("\nUse 'add <ID>' to add item, 'remove <idx>' to remove item, or type 'y' to finalize: ");
            var input = Console.ReadLine();

            if (input == "y") return inventory;

            if (input.Contains("add")) {
                input = input.Replace("add ", "");

                if (!ulong.TryParse(input, out var itemID)) {
                    AnsiConsole.MarkupLine("Invalid input. Please input a valid TemplateID.");
                    continue;
                }

                inventory.Add((GID) itemID);
                AnsiConsole.MarkupLine($"Added item {itemID}.");
            } else {
                input = input.Replace("remove ", "");

                if (!int.TryParse(input, out var index)) {
                    AnsiConsole.MarkupLine("Invalid input. Please input a valid index.");
                    continue;
                }
                if (index - 1 >= inventory.Count()) {
                    AnsiConsole.MarkupLine("Invalid input. Index out of range.");
                    continue;
                }

                var val = inventory[(int) index - 1];
                inventory.RemoveAt((int) index - 1);
                AnsiConsole.MarkupLine($"Removed item {Convert.ToUInt64(val)}. New inventory:");

                AnsiConsole.MarkupLine("      idx | template ID");
                for (int i = 0; i < inventory.Count; i++) {
                    AnsiConsole.MarkupLine($"\t[bold]{i + 1} | {Convert.ToUInt64(inventory[i])}[/]");
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
    #endregion

    #region SpellInventory
    private static List<NPCSpellEntry> CreateNewSpellInventory(ulong templateId) {
        List<NPCSpellEntry> inventory = new List<NPCSpellEntry>();

        while (true) {
            AnsiConsole.Markup("\nType 'undo' to remove the last item, or type 'y' to finalize: ");
            AnsiConsole.Markup("\nInput the TemplateID of a spell to add to the trainer: ");
            var input = Console.ReadLine();

            if (input == "y") break;

            if (input == "undo") {
                inventory.RemoveAt(inventory.Count - 1);
                AnsiConsole.MarkupLine($"Removed previous spell.");
                continue;
            }

            if (!ulong.TryParse(input, out var spellID)) {
                AnsiConsole.MarkupLine("Invalid input. Please input a valid TemplateID.");
                continue;
            }

            var newEntry = CreateNewSpellEntry(spellID);
            inventory.Add(newEntry);
        }

        return inventory;
    }

    private static NPCSpellEntry CreateNewSpellEntry(ulong templateID) {
        NPCSpellEntry entry = new NPCSpellEntry();
        entry.TemplateID = templateID;

        AnsiConsole.Markup("== Input the TemplateID of the required spell to learn this spell (0 if none): ");
        var input = Console.ReadLine();
        if (!ulong.TryParse(input, out var reqSpellID)) {
            AnsiConsole.MarkupLine("Invalid input. Please input a valid integer.");
            entry.RequiredSpellID = 0;
        }
        else {
            entry.RequiredSpellID = reqSpellID;
        }

        AnsiConsole.Markup("== Input the level required to learn this spell: ");
        input = Console.ReadLine();
        if (!int.TryParse(input, out var levelReq)) {
            AnsiConsole.MarkupLine("Invalid input. Please input a valid integer.");
            entry.Level = -1;
        }
        else {
            entry.Level = levelReq;
        }

        return entry;
    }

    private static List<NPCSpellEntry> ModifySpellInventory(NPCSpellInventory spellInventory) {
        while (true) {
            AnsiConsole.Markup("\nUse 'add <ID>' to add spell, 'mod <idx>' to modify a spell, " +
                "'remove <idx>' to remove spell, 'swap <idx> <idx>' to swap spell position, or type 'y' to finalize: ");
            var input = Console.ReadLine();

            if (input == "y") return spellInventory.Spells;

            if (input.Contains("add")) {
                input = input.Replace("add ", "");

                if (!ulong.TryParse(input, out var spellID)) {
                    AnsiConsole.MarkupLine("Invalid input. Please input a valid TemplateID.");
                    continue;
                }

                var newSpell = CreateNewSpellEntry(spellID);
                spellInventory.Spells.Add(newSpell);
                continue;
            }

            if (input.Contains("mod")) {
                input = input.Replace("mod ", "");

                if (!int.TryParse(input, out var index)) {
                    AnsiConsole.MarkupLine("Invalid input. Please input a valid index.");
                    continue;
                }
                if (index - 1 >= spellInventory.Spells.Count() || index - 1 <= 0) {
                    AnsiConsole.MarkupLine("Invalid input. Index out of range.");
                    continue;
                }

                spellInventory.Spells[index - 1] = CreateNewSpellEntry(spellInventory.Spells[index - 1].TemplateID);
                continue;
            }

            if (input.Contains("swap")) {
                input = input.Replace("swap ", "");
                var split = input.Split(' ');

                if (split.Length != 2) {
                    AnsiConsole.MarkupLine("Invalid input. Please input two valid indices.");
                    continue;
                }
                if (!int.TryParse(split[0], out var index1) || !int.TryParse(split[1], out var index2)) {
                    AnsiConsole.MarkupLine("Invalid input. Please input two valid indices.");
                    continue;
                }
                if (index1 > spellInventory.Spells.Count()
                    || index2 > spellInventory.Spells.Count()
                    || index1 - 1 <= 0
                    || index2 - 1 <= 0) {
                    AnsiConsole.MarkupLine("Invalid input. Index out of range.");
                    continue;
                }

                var temp = spellInventory.Spells[index1 - 1];
                spellInventory.Spells[index1 - 1] = spellInventory.Spells[index2 - 1];
                spellInventory.Spells[index2 - 1] = temp;
                continue;
            }

            if (input.Contains("remove")) {
                input = input.Replace("remove ", "");

                if (!int.TryParse(input, out var index)) {
                    AnsiConsole.MarkupLine("Invalid input. Please input a valid index.");
                    continue;
                }
                if (index > spellInventory.Spells.Count()) {
                    AnsiConsole.MarkupLine("Invalid input. Index out of range.");
                    continue;
                }

                var val = spellInventory.Spells[(int) index - 1];
                spellInventory.Spells.RemoveAt((int) index - 1);
                AnsiConsole.MarkupLine($"Removed spell {val.TemplateID}. New spell inventory:");

                AnsiConsole.MarkupLine("\tidx | template ID");
                for (int i = 0; i < spellInventory.Spells.Count; i++) {
                    AnsiConsole.MarkupLine($"\t[bold]  {i + 1} | {spellInventory.Spells[i].TemplateID}[/]");
                    AnsiConsole.MarkupLine($"\t\t[bold]Required Spell ID: {spellInventory.Spells[i].RequiredSpellID}[/]");
                    AnsiConsole.MarkupLine($"\t\t[bold]Required Level: {spellInventory.Spells[i].Level}[/]");
                }
            }
        }
    }

    private static void UpdateSpellInventoryDatabase(ulong templateId, List<NPCSpellEntry> inventory) {
        var finalInventory = new NPCSpellInventory {
            TemplateID = templateId,
            Spells = inventory
        };

        var getInventorySuccess = DragonDatabaseManager.TryGetNpcSpellInventory(templateId, out var existingInventory);

        if (getInventorySuccess) {
            DragonDatabaseManager.UpdateNpcSpellInventory(finalInventory);
        }
        else {
            DragonDatabaseManager.AddNpcSpellInventory(finalInventory);
        }

        AnsiConsole.MarkupLine("Database updated successfully!");
    }
    #endregion

    #region CreatureSpellbook
    private static List<uint> CreateCreatureSpellbook(string deckName) {
        var spellList = new List<uint>();

        while (true) {
            AnsiConsole.Markup("Input the TemplateID of a spell to add to the creature's spellbook, 'undo' to remove the last spell, or 'y' to finalize: ");
            var input = Console.ReadLine();

            if (input == "y") {
                return spellList;
            }

            if (input == "undo") {
                spellList.RemoveAt(spellList.Count - 1);
                AnsiConsole.MarkupLine($"Removed previous spell.");
                continue;
            }

            if (!uint.TryParse(input, out var spellID)) {
                AnsiConsole.MarkupLine("Invalid input. Please input a valid TemplateID.");
            } else {
                spellList.Add(spellID);
                AnsiConsole.MarkupLine($"Added spell {input}.");
            }
        }
    }

    private static List<uint> ModifyCreatureSpellbook(List<uint> spellList) {
        while (true) {
            AnsiConsole.Markup("\nUse 'add <ID>' to add spell, 'remove <idx>' to remove spell, or type 'y' to finalize: ");
            var input = Console.ReadLine();

            if (input == "y") return spellList;

            if (input.Contains("add")) {
                input = input.Replace("add ", "");

                if (!uint.TryParse(input, out var spellID)) {
                    AnsiConsole.MarkupLine("Invalid input. Please input a valid TemplateID.");
                    continue;
                }

                spellList.Add(spellID);
                AnsiConsole.MarkupLine($"Added spell {spellID}.");
            }
            else {
                input = input.Replace("remove ", "");

                if (!int.TryParse(input, out var index)) {
                    AnsiConsole.MarkupLine("Invalid input. Please input a valid index.");
                    continue;
                }
                if (index - 1 >= spellList.Count()) {
                    AnsiConsole.MarkupLine("Invalid input. Index out of range.");
                    continue;
                }

                var val = spellList[index - 1];
                spellList.RemoveAt(index - 1);
                AnsiConsole.MarkupLine($"Removed spell {val}. New inventory:");

                AnsiConsole.MarkupLine("      idx | template ID");
                for (int i = 0; i < spellList.Count; i++) {
                    AnsiConsole.MarkupLine($"\t[bold]{i + 1} | {spellList[i]}[/]");
                }
            }
        }
    }

    private static void UpdateCreatureSpellbookDatabase(string deckName, List<uint> spellList) {
        var getInventorySuccess = DragonDatabaseManager.TryGetCreatureSpellbook(deckName, out var creatureSpellbook);

        var finalSpellbook = new CreatureSpellbook(deckName, spellList.ToArray());

        if (getInventorySuccess) {
            DragonDatabaseManager.UpdateCreatureSpellbook(finalSpellbook);
        }
        else {
            DragonDatabaseManager.AddCreatureSpellbook(finalSpellbook);
        }

        AnsiConsole.MarkupLine("Database updated successfully!");
    }
    #endregion

    #region Setup Methods
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

    private static bool LoadCreatureDeckNames () {
        s_creatureSpellbookNames = new Dictionary<string, List<string>>();

        var inputFile = $"{InputDirectory}/{CreatureSpellbookManifest}";
        if (!File.Exists(inputFile)) {
            Console.WriteLine("Creature deck manifest not found.");
            return false;
        }
        
        var lines = File.ReadAllLines(inputFile);
        foreach (var line in lines) {
            var split = line.Split('|');
            if (split.Length != 2) {
                Console.WriteLine("Creature deck manifest is not formatted correctly.");
                return false;
            }

            var deckName = split[0].Trim();
            var creatureNames = split[1].Split(',');
            creatureNames = creatureNames.Select(name => name.Trim()).ToArray();

            foreach (var name in creatureNames) {
                if (s_creatureSpellbookNames.ContainsKey(name)) {
                    var deckList = s_creatureSpellbookNames[name];

                    if (deckList.Contains(deckName)) {
                        continue;
                    }

                    s_creatureSpellbookNames[name].Add(deckName);
                }
                else {
                    s_creatureSpellbookNames.Add(name, new List<string>() { deckName });
                }
            }
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
                                             .Where(info => info is not CombatSigilObjectInfo)
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
    #endregion
}
