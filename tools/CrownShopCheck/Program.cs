using Imcodec.Wad;
using Imcodec.IO;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.CoreObject;
using Imlight.CoreLib.Shared.Items;

if (args.Length == 0) throw new ArgumentException("Usage: CrownShopCheck <Root.wad> [RavenDB URL for isolated transaction tests] [SpiralDB directory for pack checks]");
var wad = ArchiveParser.Parse(File.OpenRead(args[0]));
if (args.Contains("--inspect")) {
    foreach (var name in wad.Files.Keys.Where(x => x.Contains("Shop", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".swf", StringComparison.OrdinalIgnoreCase))) Console.WriteLine(name);
    return;
}
var serializer = new BindSerializer();
var items = new List<WizItemTemplate>();
foreach (var name in wad.Files.Keys.Where(x => x.StartsWith("ObjectData/"))) {
    if (serializer.Deserialize(wad.OpenFile(name).Value.ToArray(), out CoreTemplate template) && template is WizItemTemplate item)
        items.Add(item);
}
if (args.Contains("--world-data")) {
    Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(items.Where(item => item.m_behaviors.OfType<LevelUpElixirBehaviorTemplate>().Any()), Newtonsoft.Json.Formatting.Indented));
    return;
}
TransportChecks.Run();
var catalog = new CrownShopCatalog(items);
if (args.Contains("--inspect-categories")) {
    foreach (var item in items.Where(CrownShopCatalog.IsSupported).Where(item => CrownShopSections.Classify(item) is 84 or 70).GroupBy(item => CrownShopSections.Classify(item)).SelectMany(group => group.Take(20)))
        Console.WriteLine($"{CrownShopSections.Classify(item)} {item.m_objectName}: {string.Join(',', item.m_adjectiveList)}");
    return;
}
Check(catalog.Entries.Count > 0, "Catalog is empty");
var progressionElixirs = items.Where(CrownShopCatalog.IsSupported)
    .Where(item => item.m_behaviors.OfType<LevelUpElixirBehaviorTemplate>().Any()).ToList();
Check(progressionElixirs.Any(item => item.m_behaviors.OfType<WorldElixirBehaviorTemplate>().Any()), "Missing world-elixir regression fixtures");
foreach (var elixir in progressionElixirs.Where(item => !item.m_behaviors.OfType<WorldElixirBehaviorTemplate>().Any())) {
    var rejected = false;
    try { CrownShopDelivery.Create(elixir, null, null, null); }
    catch (InvalidOperationException error) {
        rejected = error.Message.Contains("not implemented") && error.Message.Contains("no currency was charged");
    }
    Check(rejected, $"Progression elixir reached reward/inventory code: {elixir.m_objectName}");
}
var progression = new WorldElixirProgression(items);
var schools = new[] { Imlight.CoreLib.Shared.Behaviors.MagicSchool.Fire, Imlight.CoreLib.Shared.Behaviors.MagicSchool.Ice,
    Imlight.CoreLib.Shared.Behaviors.MagicSchool.Storm, Imlight.CoreLib.Shared.Behaviors.MagicSchool.Myth,
    Imlight.CoreLib.Shared.Behaviors.MagicSchool.Life, Imlight.CoreLib.Shared.Behaviors.MagicSchool.Death,
    Imlight.CoreLib.Shared.Behaviors.MagicSchool.Balance };
foreach (var id in progression.TemplateIds) foreach (var school in schools) {
    var plan = progression.CreatePlan(id, school);
    Check(plan.Stages.Count == (plan.EndLevel - 50) / 10 + 1, "World skip omitted earlier worlds");
    Check(plan.Stages.Last().CompletionKey == plan.CompletionKey, "World skip ends in wrong world");
    Check(plan.Stages[0].Quests.All(quest => !quest.StartsWith("GH-")), "First arc skip includes Grizzleheim");
}
Console.WriteLine($"PASS: cumulative progression plans for {progression.TemplateIds.Count} world elixirs across all seven schools; standalone level-up elixirs still safely decline.");
var shared = ArchiveParser.Parse(File.OpenRead(Path.Combine(Path.GetDirectoryName(args[0])!, "_Shared-WorldData.wad")));
var layout = CrownShopSections.CreateLayout();
var shopStrings = System.Text.Encoding.Unicode.GetString(wad.OpenFile("Locale/en-US/CrownShopSWF.lang").Value.Span).Split('\n');
var shopKeys = new HashSet<string>();
for (var i = 1; i + 2 < shopStrings.Length; i += 3) shopKeys.Add("CrownShopSWF_" + shopStrings[i].TrimEnd('\r'));
foreach (var key in layout.m_tabs.SelectMany(tab => new[] { tab.m_name.ToString(), tab.m_description.ToString() })
    .Concat(layout.m_categories.SelectMany(category => new[] { category.m_name.ToString(), category.m_description.ToString() }))) {
    Check(key.StartsWith("CrownShopSWF_") && key.Length > "CrownShopSWF_".Length,
        "Literal shop label crashes native purchase confirmation when it strips the locale prefix");
    Check(shopKeys.Contains(key), $"Missing Crown Shop localized caption: {key}");
}
foreach (var icon in layout.m_tabs.Select(tab => tab.m_iconResource.ToString()).Concat(layout.m_categories.Select(category => category.m_iconResource.ToString())))
    Check(shared.Files.ContainsKey(icon), $"Missing native section icon: {icon}");
Check(catalog.Entries.Take(CrownShopCatalog.PageSize).Select(entry => entry.CategoryId).Distinct().Count()
    == catalog.Entries.Select(entry => entry.CategoryId).Distinct().Count(), "First page does not represent every populated section");
foreach (var group in catalog.Entries.GroupBy(entry => entry.CategoryId))
    Console.WriteLine($"Section {CrownShopSections.Sections.Single(section => section.Id == group.Key).Name}: {group.Count()} items");
var maxBytes = 0;
var seen = new HashSet<ulong>();
var wire = new ObjectSerializer(false, SerializerFlags.SerializeFlags | SerializerFlags.Compress);
for (var page = 1; page <= (catalog.Entries.Count + CrownShopCatalog.PageSize - 1) / CrownShopCatalog.PageSize; page++) {
    var bytes = catalog.SerializePage("", page, out var pageCount);
    maxBytes = Math.Max(maxBytes, bytes.Length);
    CheckCatalogPacket(bytes);
    Check(wire.Deserialize(bytes, 5, out CrownShopData data) && data.m_items.Count > 0, "Catalog roundtrip failed");
    Check(data.m_crownShopLayout.m_tabs.Count == 9, "Missing original shop tabs");
    foreach (var category in data.m_crownShopLayout.m_categories) {
        Check(data.m_crownShopLayout.m_tabs.Single(t => t.m_ID == category.m_parentTabID).m_categoryIDs.Contains(category.m_ID), "Category not in parent tab");
        var recommendations = data.m_recomendedItems.m_categoryData.Single(c => c.m_id == category.m_ID);
        var members = data.m_items.Where(item => item.m_displayPriority.ToString().Split(',').Any(pair => pair.StartsWith(category.m_ID + ":")));
        Check(members.Select(item => unchecked((int)item.m_itemTemplateId.Full)).Order().SequenceEqual(recommendations.m_items.Select(item => item.m_templateID).Order()), "Category recommendation map differs from membership");
    }
    foreach (var item in data.m_items) {
        var membership = item.m_displayPriority.ToString().Split(',').Select(pair => pair.Split(':')).ToList();
        Check(membership.Count > 0 && membership.All(pair => pair.Length == 2
            && int.TryParse(pair[0], out var category) && data.m_crownShopLayout.m_categories.Any(c => c.m_ID == category)
            && uint.TryParse(pair[1], out _)), "Item has no valid client category membership");
        Check(data.m_recomendedItems?.m_categoryData.Any(c => c.m_id == 1) == true, "Missing recommended category map");
        Check(item.m_crownsCost > 0 && item.m_goldCost == checked(item.m_crownsCost * 10), "Gold must cost exactly 10 times crowns");
        Check(membership.Count == 2 && membership.Any(pair => pair[0] != "1"), "Missing specific section");
        Check(seen.Add(item.m_itemTemplateId), "Duplicate catalog entry");
    }
}
Check(seen.Count == catalog.Entries.Count, "Catalog pages omitted items");
var first = catalog.Entries[0];
Check(wire.Deserialize(catalog.SerializePage(first.Id.ToString(), 99, out _), 5, out CrownShopData search)
    && search.m_items.Any(item => item.m_itemTemplateId.Full == first.Id), "ID search failed");
Check(wire.Deserialize(catalog.SerializePage("nonexistent-item-xyz-012", -1, out _), 5, out CrownShopData empty)
    && empty.m_items.Count == 0, "Empty search failed");
var eligibility = new SegmentationInputData { m_bIsValidSegmentationData = true, m_playerLevel = 1, m_accountNCrownsInWallet = 10000 };
var eligibilityBytes = CrownShopCatalog.SerializeSegmentation(eligibility);
Check(BitConverter.ToUInt32((byte[]) eligibilityBytes, 0) == eligibility.GetHash(), "Eligibility data incorrectly begins with flags/compression instead of the type hash");
var eligibilityReader = new ObjectSerializer(false, SerializerFlags.None);
Check(eligibilityReader.Deserialize(eligibilityBytes, 24, out SegmentationInputData decodedEligibility)
    && decodedEligibility.m_bIsValidSegmentationData && decodedEligibility.m_accountNCrownsInWallet == 10000,
    "Client eligibility wire contract failed");
var coreSerializer = new CrownShopItemSerializer();
var snack = new ClientPetSnackItem { m_globalID = 54321UL, m_templateID = 12345UL, m_quantity = 3 };
Check(coreSerializer.Serialize(snack, 24, out var snackBytes)
    && coreSerializer.Deserialize(snackBytes, 24, out ClientPetSnackItem decodedSnack)
    && decodedSnack.m_quantity == 3 && decodedSnack.m_templateID == snack.m_templateID, "Snack serialization failed");
Console.WriteLine($"PASS: {seen.Count} catalog entries, all gold-priced at exactly 10x crowns, all pages decode; maximum payload {maxBytes} bytes. Localized purchase labels, full network packet framing, category membership, recommendation data, eligibility framing, search and snack serialization passed.");
if (args.Length > 1) DbChecks.Run(args[1], wad, items, args.Length > 2 ? args[2] : null);

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

static void CheckCatalogPacket(ByteString bytes) {
    var response = new WIZARD_12_PROTOCOL.MSG_PCS_LIST_RESPONSE { Data = bytes, UpdateID = 5, Error = 0 };
    var packet = MessageEncoder.Encode(response);
    // Independently inspect the wire envelope, not just the compressed object.
    Check(BitConverter.ToUInt16(packet, 0) == 0xf00d, "Bad packet magic");
    var length = BitConverter.ToUInt16(packet, 2);
    Check((length & 0x8000) == 0, "Shop response uses the incompatible large-packet path");
    Check(length == packet.Length - 4, "Packet length excludes part of the response");
    Check(packet[4] == 0 && packet[8] == response.ServiceId && packet[9] == response.MessageOrder, "Bad message header");
    Check(BitConverter.ToUInt16(packet, 10) == packet.Length - 9, "Bad DML record length");
    var reader = new BitReader(packet[12..^1]);
    var decoded = new WIZARD_12_PROTOCOL.MSG_PCS_LIST_RESPONSE();
    decoded.Decode(reader);
    Check(((byte[])decoded.Data).SequenceEqual((byte[])bytes) && decoded.UpdateID == 5 && decoded.Error == 0,
        "PCS response fields failed network roundtrip");
    Check(reader.BitPos() == (packet.Length - 13) * 8, "PCS response left unread or truncated fields");
}
