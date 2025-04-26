/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Imlight.Director;

internal class ManifestGenerator {

    private const string OUTPUT_DIR = "/manifests/";
    private const string GO_MANIFEST_NAME = "go.manifest";
    private const string ITEM_MANIFEST_NAME = "item.manifest";
    private const string SPELL_MANIFEST_NAME = "spell.manifest";
    private const string NPC_MANIFEST_NAME = "npc.manifest";
    private const string SHOPKEEPER_MANIFEST_NAME = "shopkeeper.manifest";
    private const string CREATURE_MANIFEST_NAME = "creature.manifest";
    private const string DECK_MANIFEST_NAME = "deck.manifest";
    private const int INFORM_ALIVE_INTERVAL = 10000;

    private static readonly string[] s_shopKeeperNameGiveaways = new string[] {
        "shop",
    };
    private static readonly string[] s_explorerNames = new string[] {
        "prospector zeke",
        "eloise merryweather",
        "elik silverfist",
    };

    private readonly StreamWriter _goManifest;
    private readonly StreamWriter _itemManifest;
    private readonly StreamWriter _spellManifest;
    private readonly StreamWriter _npcManifest;
    private readonly StreamWriter _shopkeeperManifest;
    private readonly StreamWriter _creatureManifest;
    private readonly StreamWriter _deckManifest;
    private readonly Dictionary<string, List<string>> _npcDeckMap = new();
    private int _topDeckCharCount;

    internal ManifestGenerator() {
        EnsureOutputFiles();
        _goManifest = OpenManifest(GO_MANIFEST_NAME);
        _itemManifest = OpenManifest(ITEM_MANIFEST_NAME);
        _spellManifest = OpenManifest(SPELL_MANIFEST_NAME);
        _npcManifest = OpenManifest(NPC_MANIFEST_NAME);
        _shopkeeperManifest = OpenManifest(SHOPKEEPER_MANIFEST_NAME);
        _creatureManifest = OpenManifest(CREATURE_MANIFEST_NAME);
        _deckManifest = OpenManifest(DECK_MANIFEST_NAME);

        Generate();
    }

    private void Generate() {
        var templates = CoreObjectFactory.TemplateManifest.m_serializedTemplates;

        var counter = 0;
        foreach (var templateLocation in templates) {
            var id = templateLocation.m_id;
            var path = templateLocation.m_filename.ToString();

            // Kroktopia has inconsistent naming conventions
            if (path.Contains('|')) {
                path = path.Substring(path.LastIndexOf('|') + 1);
            }
            if (path.Contains("ObjectData/Krokotopia")) {
                path = path.Replace("ObjectData/Krokotopia", "ObjectData/KT");
            }

            var template = CoreObjectFactory.GetCoreTemplate(id);
            var obj = CoreObjectFactory.FinalizeCoreObject(id);

            if (template is WizGameObjectTemplate goTemplate) {
                WriteGameObject(id, goTemplate, (WizClientObject) obj);
            } else if (template is WizItemTemplate itemTemplate) {
                WriteItem(id, itemTemplate, (WizClientObjectItem) obj);
            } else if (template is SpellTemplate spellTemplate) {
                WriteSpell(id, path, spellTemplate);
            }

            if (counter >= INFORM_ALIVE_INTERVAL) {
                Logger.Information("Wrote {0} templates", Logger.Args(INFORM_ALIVE_INTERVAL));
                counter = 0;
            }

            counter++;
        }

        WriteSortedDecks();

        Logger.Information("Finished writing manifests");
        _goManifest.Flush();
        _itemManifest.Flush();
        _spellManifest.Flush();
        _npcManifest.Flush();
        _shopkeeperManifest.Flush();
        _creatureManifest.Flush();
        _deckManifest.Flush();
    }

    private void WriteGameObject(ulong id, WizGameObjectTemplate template, WizClientObject obj) {
        if (template.m_behaviors.Any(x => x is not null && x.GetType() == typeof(NPCBehaviorTemplate))) {
            WriteNpc(id, template, obj);
            return;
        }

        var paddedId = id.ToString();
        paddedId = paddedId.ToString().PadRight(20, ' ');

        _goManifest.WriteLine($"{paddedId} | {template.m_objectName}");
    }

    private void WriteNpc(ulong id, WizGameObjectTemplate template, WizClientObject obj) {
        // If this NPC is a shopkeeper, add them to the shopkeeper manifest instead.
        var npcName = template.m_objectName.ToString().ToLower();
        var debugName = obj.m_debugName.ToString().ToLower();
        if (s_shopKeeperNameGiveaways.Any(npcName.Contains) || s_explorerNames.Any(n => debugName == n)) {
            WriteShopkeeper(id, template, obj);
            return;
        }

        // If this template has a DuelistBehavior, it's a creature.
        var duelistBehavior = template.m_behaviors
            .FirstOrDefault(x => x is not null && x.GetType() == typeof(DuelistBehaviorTemplate)) as DuelistBehaviorTemplate;
        if (duelistBehavior is not null) {
            WriteCreature(id, template, obj);
        }

        var paddedId = id.ToString();
        var name = obj.m_debugName == "" ? template.m_objectName : obj.m_debugName;
        paddedId = paddedId.ToString().PadRight(20, ' ');

        _npcManifest.WriteLine($"{paddedId} | {name}");
    }

    private void WriteShopkeeper(ulong id, WizGameObjectTemplate template, WizClientObject obj) {
        var paddedId = id.ToString();
        var name = obj.m_debugName == "" ? template.m_objectName : obj.m_debugName;
        paddedId = paddedId.ToString().PadRight(20, ' ');

        _shopkeeperManifest.WriteLine($"{paddedId} | {name}");
    }

    private void WriteCreature(ulong id, WizGameObjectTemplate template, WizClientObject obj) {
        var paddedId = id.ToString();
        var name = obj.m_debugName == "" ? template.m_objectName : obj.m_debugName;
        paddedId = paddedId.ToString().PadRight(20, ' ');

        _creatureManifest.WriteLine($"{paddedId} | {name}");

        // We'll also write their deck to a manifest. Search for their equipment behavior template.
        var equipmentBehavior = template.m_behaviors
            .FirstOrDefault(x => x is not null && x.GetType() == typeof(EquipmentBehaviorTemplate)) as EquipmentBehaviorTemplate;
        if (equipmentBehavior is not null) {
            foreach (var itemTemplateId in equipmentBehavior.m_itemList) {
                var itemTemplate = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(itemTemplateId);
                if (template is null) {
                    continue;
                }

                // Check if the item has a deck behavior.
                var deckBehaviorTemplate = itemTemplate.m_behaviors
                    .FirstOrDefault(x => x is DeckBehaviorTemplate) as DeckBehaviorTemplate;
                if (deckBehaviorTemplate is not null) {
                    var deckName = deckBehaviorTemplate.m_defaultDeck;

                    // If the deck name is longer than the current top deck name, update it.
                    if (deckName.Length > _topDeckCharCount) {
                        _topDeckCharCount = deckName.Length;
                    }

                    if (!_npcDeckMap.ContainsKey(deckName)) {
                        var newList = new List<string> { name };
                        _npcDeckMap.Add(deckName, newList);
                    }
                    else {
                        _npcDeckMap[deckName].Add(name);
                    }
                }
            }
        }
    }

    private void WriteSortedDecks() {
        foreach (var (deckName, npcList) in _npcDeckMap.OrderBy(x => x.Key)) {
            var deckLine = new StringBuilder();
            deckLine.Append($"{deckName.PadRight(_topDeckCharCount)} | ");
            deckLine.Append(string.Join(", ", npcList.OrderBy(x => x)));
            _deckManifest.WriteLine(deckLine.ToString());
        }
    }

    private void WriteItem(ulong id, WizItemTemplate template, WizClientObjectItem item) {
        var paddedId = id.ToString();
        var itemName = item.m_debugName == "" ? template.m_objectName : item.m_debugName;
        var school = template.m_school == "" ? "NoSchool" : template.m_school.ToString();

        // Ensure that `school` is 8 characters. replace any missing with a space.
        school = school.PadRight(8, ' ');

        // Pad the ID to how many digits can be found in the highest ID.
        paddedId = paddedId.ToString().PadRight(20, ' ');

        _itemManifest.WriteLine($"{paddedId} | {school} | {itemName}");
    }

    private void WriteSpell(ulong id, string path, SpellTemplate template) {
        var paddedId = id.ToString();

        // Pad the ID to how many digits can be found in the highest ID.
        paddedId = paddedId.ToString().PadRight(20, ' ');

        _spellManifest.WriteLine($"{paddedId} | {template.m_name}");
    }

    private StreamWriter OpenManifest(string name) {
        var localPath = Directory.GetCurrentDirectory();
        var outputPath = localPath + OUTPUT_DIR;

        return new StreamWriter(outputPath + name);
    }

    private void EnsureOutputFiles() {
        var localPath = Directory.GetCurrentDirectory();
        var outputPath = localPath + OUTPUT_DIR;

        if (!Directory.Exists(outputPath)) {
            Directory.CreateDirectory(outputPath);
        }

        if (!File.Exists(outputPath + GO_MANIFEST_NAME)) {
            File.Create (outputPath + GO_MANIFEST_NAME);

            Logger.Information("Created go.manifest");
        }

        if (!File.Exists(outputPath + ITEM_MANIFEST_NAME)) {
            File.Create (outputPath + ITEM_MANIFEST_NAME);

            Logger.Information("Created item.manifest");
        }

        if (!File.Exists(outputPath + SPELL_MANIFEST_NAME)) {
            File.Create (outputPath + SPELL_MANIFEST_NAME);

            Logger.Information("Created spell.manifest");
        }
    }

}
