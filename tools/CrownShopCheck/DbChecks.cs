using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Imcodec.ObjectProperty.TypeCache;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

static class DbChecks {
    public static void Run(string url, Imcodec.Wad.Archive wad, List<WizItemTemplate> templates, string spiralPath) {
        var name = "CrownShopCheck-" + Guid.NewGuid().ToString("N");
        using var admin = new DocumentStore { Urls = [url], Database = name }.Initialize();
        var config = Path.GetTempFileName();
        File.WriteAllText(config, $$"""
        [Logging]
        LogLevel=ERROR
        LogPath={{config}}.log
        LogFormat={Message}{NewLine}
        SeqSinkUrl=http://127.0.0.1:5341
        [Database]
        SpiralDBLocalPath={{spiralPath}}
        SpiralDBDisableRemote=true
        PlayerDatabaseName={{name}}
        PlayerDatabaseUrl={{url}}
        PlayerDatabaseCertificatePath=/nonexistent-crownshop-test-certificate
        DatabaseMaxNumberOfRequestsPerSession=100
        DatabaseRequestTimeoutInSeconds=30
        DatabaseWaitForNonStaleResultsTimeout=30
        [Character]
        MaxLevel=200
        MaxInventoryItems=2
        MaxCharacters=6
        PetEnergyTickInSeconds=60
        """);
        ConfigurationManager.Initialize(config);
        admin.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(name)));
        try {
            var account = new Account("CrownShopTest", "", "test-only") { Crowns = null };
            var wizard = new Wizard {
                CharId = 12345, AccountId = account.AccountId, Account = account,
                InventoryBehavior = new() { Items = [], InventoryItemIds = [] },
                AlchemyBehavior = new() { Reagents = [], ReagentItemIds = [] },
                PetSnackBehavior = new() { Snacks = [], SnackItemIds = [] },
                PetOwnerBehavior = new(),
                GameStats = new(default, 1) { m_currentGold = 1000, m_baseGoldPouch = 10000, m_baseHitpoints = 500, m_baseMana = 100, m_energyMax = 50 }
            };
            using (var session = PlayerDatabase.Instance.Store.OpenSession()) {
                session.Store(account);
                session.Advanced.GetMetadataFor(account)[Raven.Client.Constants.Documents.Metadata.Collection] = AccountCollection.CollectionName;
                session.Store(wizard);
                session.Advanced.GetMetadataFor(wizard)[Raven.Client.Constants.Documents.Metadata.Collection] = WizardCollection.CollectionName;
                session.SaveChanges();
            }
            Check(AccountCollection.EnsureStartingCrowns(account.AccountId) == 10000, "Legacy starting grant");
            Check(AccountCollection.EnsureStartingCrowns(account.AccountId) == 10000, "Repeated grant changed balance");
            var item = Delivery(100);
            Check(!CrownShopTransactions.TryPurchase(wizard, item, 0, 1001), "Overspending permitted");
            Check(!CrownShopTransactions.TryPurchase(wizard, item, 2, 10), "Invalid currency permitted");
            Check(!CrownShopTransactions.TryPurchase(wizard, item, 0, -10), "Negative price permitted");
            Check(CrownShopTransactions.TryPurchase(wizard, item, 0, 100), "Gold purchase failed");
            Check(wizard.GameStats.m_currentGold == 900 && wizard.InventoryBehavior.Items.Count == 1, "Gold purchase state");
            Check(CrownShopTransactions.TryPurchase(wizard, Delivery(101), 1, 10000), "Crown purchase failed");
            Check(AccountCollection.EnsureStartingCrowns(account.AccountId) == 0, "Zero balance incorrectly topped up");
            Check(!CrownShopTransactions.TryPurchase(wizard, Delivery(102), 0, 10), "Full inventory permitted purchase");
            Check(wizard.GameStats.m_currentGold == 900, "Failed purchase charged gold");
            var reagentDelivery = new CrownShopDelivery();
            reagentDelivery.Reagents.Add(new ClientReagentItem { m_globalID = 200UL, m_templateID = 300UL, m_characterId = wizard.CharId, m_quantity = 998 });
            Check(CrownShopTransactions.TryPurchase(wizard, reagentDelivery, 0, 10), "Reagent purchase failed");
            var reagent2 = new CrownShopDelivery();
            reagent2.Reagents.Add(new ClientReagentItem { m_globalID = 201UL, m_templateID = 300UL, m_characterId = wizard.CharId, m_quantity = 1 });
            Check(CrownShopTransactions.TryPurchase(wizard, reagent2, 0, 10), "Reagent merge failed");
            Check(wizard.AlchemyBehavior.Reagents.Single().m_quantity == 999, "Reagent stack wrong");
            var overflow = new CrownShopDelivery();
            overflow.Reagents.Add(new ClientReagentItem { m_globalID = 202UL, m_templateID = 300UL, m_characterId = wizard.CharId, m_quantity = 1 });
            Check(!CrownShopTransactions.TryPurchase(wizard, overflow, 0, 10), "Reagent overflow permitted");
            var snacks = new CrownShopDelivery();
            snacks.Snacks.Add(new ClientPetSnackItem { m_globalID = 250UL, m_templateID = 350UL, m_characterId = wizard.CharId, m_quantity = 5 });
            Check(CrownShopTransactions.TryPurchase(wizard, snacks, 0, 10), "Snack purchase failed");
            var gold = CrownShopDelivery.Create(new GoldAmountTemplate { m_templateID = 400, m_goldAmount = 50 }, wizard, null, null);
            Check(CrownShopTransactions.TryPurchase(wizard, gold, 0, 10), "Gold bundle failed");
            using (var session = PlayerDatabase.Instance.Store.OpenSession()) {
                var saved = session.Query<Wizard>(collectionName: WizardCollection.CollectionName).Customize(q => q.WaitForNonStaleResults()).Single();
                Check(saved.GameStats.m_currentGold == 910, "Persisted balance wrong");
                Check(saved.InventoryBehavior.InventoryItemIds.Count == 2, "Persisted inventory wrong");
                Check(saved.AlchemyBehavior.ReagentItemIds.Count == 1 && saved.PetSnackBehavior.SnackItemIds.Count == 1, "Persisted stacks wrong");
                Check(session.Query<ClientReagentItem>(collectionName: "WizardReagents").Customize(q => q.WaitForNonStaleResults()).Single().m_quantity == 999, "Persisted reagent quantity wrong");
            }
            var spellRewards = new CrownShopDelivery();
            spellRewards.Spells.Add(new(42, 4242, true));
            spellRewards.Spells.Add(new(43, 4343, false));
            Check(CrownShopTransactions.TryPurchase(wizard, spellRewards, 0, 10), "Spell reward purchase failed");
            Check(wizard.SpellbookBehavior.TreasureCardTemplateIds.Contains(42) && wizard.SpellbookBehavior.LearnedSpellTemplateIds.Contains(43), "Spell rewards were not applied");
            var elixir = CrownShopDelivery.Create(new WizItemTemplate {
                m_templateID = 700,
                m_behaviors = [new ElixirBehaviorTemplate { m_expireTime = "0", m_equipActionList = new ResultList { m_results = [new ResAddHealth(), new ResAddMana(), new ResAddEnergy(), new ResAddCharacterSlotResult { m_maximumPurchasedCharacterSlots = 1 }] } }]
            }, wizard, null, null);
            Check(CrownShopTransactions.TryPurchase(wizard, elixir, 0, 10), "Instant elixir failed");
            Check(wizard.GameStats.m_currentHitpoints == 500 && wizard.GameStats.m_currentMana == 100 && wizard.PetOwnerBehavior.Energy == 50 && account.PurchasedCharacterSlots == 1, "Elixir effects not applied");
            Check(!CrownShopTransactions.TryPurchase(wizard, elixir, 0, 10), "Character slot limit ignored");
            var lunari = CrownShopDelivery.Create(new LunariAmountTemplate { m_templateID = 701, m_lunariAmount = 25 }, wizard, null, null);
            Check(CrownShopTransactions.TryPurchase(wizard, lunari, 0, 10) && wizard.GameStats.m_currentEventCurrency1 == 25, "Lunari grant failed");
            if (!string.IsNullOrEmpty(spiralPath)) AssetChecks(wad, templates, wizard);
            Console.WriteLine("PASS: atomic gold/crown purchases, one-time starting grant, insufficient funds, full bags, invalid currency/prices, stack merges/overflow, gold bundles, spell rewards, instant elixirs, character slots and persisted balances.");
        } finally {
            try { PlayerDatabase.Instance.Store.Dispose(); }
            finally { admin.Maintenance.Server.Send(new DeleteDatabasesOperation(name, hardDelete: true)); }
            File.Delete(config);
        }
    }
    static void AssetChecks(Imcodec.Wad.Archive wad, List<WizItemTemplate> templates, Wizard wizard) {
        // Supply the fixture archive directly so the test never touches the running server's cache.
        typeof(CrownShopCatalog).Assembly.GetType("Imlight.CoreLib.Shared.Resources.RootArchiveLoader")!
            .GetField("s_rootWad", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, wad);
        // Run the real production startup path before the first shop request.
        // Previously it constructed resources outside their singleton, so the
        // first world purchase attempted to fill the spell dictionary twice.
        _ = new Imlight.Director.ResourceContainer();
        _ = new Imlight.Director.ResourceContainer(); // Repeated discovery must reuse every resource.
        Console.WriteLine("PASS: production resource startup and repeated discovery reuse the same singleton instances.");
        _ = Imlight.CoreLib.Shared.Resources.CoreObjectFactory.Instance;
        Imlight.CoreLib.WizardData.SpiralDB.Load();
        Check(CrownShopCatalog.Instance.Entries.Count > 0, "Manifest-backed catalog empty");
        WorldElixirChecks.Run(wad, templates);
        var packs = templates.OfType<BoosterPackTemplate>().Where(CrownShopCatalog.IsSupported).ToList();
        var available = packs.Where(p => p.m_lootTables.Count > 0 && p.m_lootTables.All(t => DropTableCollection.GetDropTable(t) != null)).ToList();
        var successful = 0;
        var failures = new Dictionary<string, int>();
        foreach (var pack in available) {
            try {
                var result = CrownShopDelivery.Create(pack, wizard, null, wizard.GameObject);
                if (result.Items.Count + result.Snacks.Count + result.Reagents.Count + result.Spells.Count > 0 || result.Gold > 0) successful++;
            } catch (InvalidOperationException ex) {
                failures[ex.Message] = failures.GetValueOrDefault(ex.Message) + 1;
            }
        }
        Console.WriteLine($"Asset check: {CrownShopCatalog.Instance.Entries.Count} manifest entries, {available.Count}/{packs.Count} packs have complete loot tables, {successful} packs produced a deliverable roll.");
        foreach (var failure in failures.OrderByDescending(x => x.Value).Take(8)) Console.WriteLine($"Pack limitation ({failure.Value}): {failure.Key}");

    }
    static CrownShopDelivery Delivery(ulong id) {
        var delivery = new CrownShopDelivery();
        delivery.Items.Add(new WizClientObjectItem { m_globalID = id, m_templateID = 500UL, m_characterId = 12345UL });
        return delivery;
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
