using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Raven.Client.Documents;

static class WorldElixirChecks {
    public static void Run(Imcodec.Wad.Archive wad, List<WizItemTemplate> templates) {
        var serializer = new BindSerializer();
        Check(serializer.Deserialize<MagicXPConfig>(wad.OpenFile("MagicXPConfig.xml").Value.ToArray(), 1, out var xp), "XP fixture missing");
        var worlds = templates.Where(t => t.m_behaviors.OfType<WorldElixirBehaviorTemplate>().Any()).DistinctBy(t => t.m_templateID).ToArray();
        var schools = new[] { MagicSchool.Fire, MagicSchool.Ice, MagicSchool.Storm, MagicSchool.Myth, MagicSchool.Life, MagicSchool.Death, MagicSchool.Balance };
        ulong nextId = 30000;
        foreach (var school in schools) foreach (var world in worlds) {
            var plan = WorldElixirProgression.Instance.CreatePlan(world.m_templateID, school);
            var startLevel = school == MagicSchool.Ice ? 175 : 1;
            var health = xp.m_classInfo.Single(c => c.m_className == school.ToString()).m_classLevelInfo[startLevel].m_hitpoints;
            var account = new Account("WorldElixirTest" + nextId, "", "test-only") { Crowns = 50000 };
            var wizard = new Wizard {
                CharId = nextId++, AccountId = account.AccountId, Account = account,
                InventoryBehavior = new() { Items = [], InventoryItemIds = [] },
                AlchemyBehavior = new() { Reagents = [], ReagentItemIds = [] },
                PetSnackBehavior = new() { Snacks = [], SnackItemIds = [] }, PetOwnerBehavior = new(),
                QuestBehavior = new(), SpellbookBehavior = new(),
                MagicSchoolBehavior = new() { MagicSchool = school, Level = startLevel, ExperiencePoints = startLevel > 1 ? xp.m_levelInfo[startLevel - 1].m_xpToLevel + 123 : 0, TrainingPoints = 3 },
                GameStats = new(school, startLevel) { m_currentGold = 500000, m_baseGoldPouch = 1000000,
                    m_baseHitpoints = health + 17, m_baseMana = xp.m_levelInfo[startLevel].m_mana + 11,
                    m_energyMax = xp.m_levelInfo[startLevel].m_petEnergy, m_powerPipBase = xp.m_levelInfo[startLevel].m_pipChance },
            };
            var removed = new QuestInstance { ID = nextId++, OwnerCharId = wizard.CharId, QuestName = plan.Stages[0].Quests.First() };
            var kept = new QuestInstance { ID = nextId++, OwnerCharId = wizard.CharId, QuestName = "GH-unrelated-sidequest" };
            var otherWizardQuest = new QuestInstance { ID = nextId++, OwnerCharId = 999999, QuestName = removed.QuestName };
            wizard.QuestBehavior.AddQuest(removed);
            wizard.QuestBehavior.AddQuest(kept);
            wizard.QuestBehavior.Registry["UnrelatedUnlock"] = 9;
            using (var session = PlayerDatabase.Instance.Store.OpenSession()) {
                Store(session, account, AccountCollection.CollectionName);
                Store(session, wizard, WizardCollection.CollectionName);
                foreach (var quest in new[] { removed, kept, otherWizardQuest }) Store(session, quest, QuestInstanceCollection.CollectionName);
                session.SaveChanges();
            }
            var delivery = CrownShopDelivery.Create(world, wizard, null, null);
            Check(delivery.WorldProgression != null && delivery.Items.Count == 0, "World elixir became inventory item");
            Check(delivery.Spells.Count > 0, "School spells not resolved");
            Check(!CrownShopTransactions.TryPurchase(wizard, delivery, 1, 50001), "Unaffordable world purchase succeeded");
            Check(wizard.QuestBehavior.Registry.Count == 1 && wizard.MagicSchoolBehavior.Level == startLevel, "Failed purchase changed live state");
            CrownShopCatalog.Instance.TryGet(world.m_templateID, out var entry);
            var cost = school == MagicSchool.Fire ? entry.Gold : entry.Crowns;
            var currency = school == MagicSchool.Fire ? 0 : 1;
            var oldXp = wizard.MagicSchoolBehavior.ExperiencePoints;
            Check(CrownShopTransactions.TryPurchase(wizard, delivery, currency, cost), $"World purchase failed: {world.m_objectName}/{school}");
            var level = Math.Max(startLevel, plan.EndLevel);
            Check(wizard.MagicSchoolBehavior.Level == level && wizard.GameStats.Level == level, "Incorrect world-ending level");
            Check(wizard.MagicSchoolBehavior.ExperiencePoints == (startLevel > plan.EndLevel ? oldXp : xp.m_levelInfo[level - 1].m_xpToLevel), "Incorrect world XP");
            Check(wizard.MagicSchoolBehavior.TrainingPoints == 3 + xp.m_levelInfo.Skip(startLevel + 1).Take(Math.Max(0, level - startLevel)).Sum(l => l.m_trainingPoints), "Level-up training points wrong");
            var expectedHp = xp.m_classInfo.Single(c => c.m_className == school.ToString()).m_classLevelInfo[level].m_hitpoints + 17;
            Check(wizard.GameStats.m_baseHitpoints == expectedHp, "Health boost lost equipment bonus");
            Check(wizard.QuestBehavior.Registry["UnrelatedUnlock"] == 9, "Unrelated registry was changed");
            Check(plan.Stages.SelectMany(stage => stage.Quests).All(wizard.QuestBehavior.HasCompletedQuest), "Skipped quest missing completion flag");
            Check(wizard.QuestBehavior.CurrentQuestIDs.SequenceEqual(new[] { kept.ID }) && wizard.QuestBehavior.CurrentQuestInstances.Single().ID == kept.ID, "Active quest cleanup incorrect");
            Check(delivery.CompletedQuestIds.SequenceEqual(new[] { removed.ID }), "Client quest removals incorrect");
            Check(wizard.GameStats.m_currentGold == 500000 - (currency == 0 ? cost : 0) && wizard.Account.Crowns == 50000 - (currency == 1 ? cost : 0), "Incorrect debit");
            Check(!CrownShopTransactions.TryPurchase(wizard, delivery, currency, cost), "Replay charged a second time");
            using (var session = PlayerDatabase.Instance.Store.OpenSession()) {
                var saved = session.Query<Wizard>(collectionName: WizardCollection.CollectionName).Customize(q => q.WaitForNonStaleResults()).Single(w => w.CharId == wizard.CharId);
                Check(saved.QuestBehavior.GetRegistryValue(plan.CompletionKey) == 1 && saved.MagicSchoolBehavior.Level == level, "Progression did not persist");
                Check(saved.SpellbookBehavior.LearnedSpellTemplateIds.Count == delivery.Spells.Count, "Spell grants did not persist");
                var quests = session.Query<QuestInstance>(collectionName: QuestInstanceCollection.CollectionName).Customize(q => q.WaitForNonStaleResults()).Where(q => q.ID == removed.ID || q.ID == kept.ID || q.ID == otherWizardQuest.ID).ToList();
                Check(quests.Count == 2 && quests.Any(q => q.ID == otherWizardQuest.ID), "Deleted unrelated/other wizard quest");
            }
            var repeated = false;
            try { CrownShopDelivery.Create(world, wizard, null, null); }
            catch (InvalidOperationException ex) { repeated = ex.Message.Contains("already"); }
            Check(repeated, "Completed world purchase was offered again");
            if (school == MagicSchool.Fire && plan.World == "Celestia") {
                // Later purchases must not replay first-arc registry resets or school spell grants.
                wizard.QuestBehavior.Registry["QT-DS-ACAD-C01-005"] = 7;
                using (var session = PlayerDatabase.Instance.Store.OpenSession()) {
                    var saved = session.Query<Wizard>(collectionName: WizardCollection.CollectionName).Customize(q => q.WaitForNonStaleResults()).Single(w => w.CharId == wizard.CharId);
                    saved.QuestBehavior.Registry["QT-DS-ACAD-C01-005"] = 7;
                    session.SaveChanges();
                }
                var later = worlds.Single(t => t.m_objectName == "Elixir-World-Novus");
                var next = CrownShopDelivery.Create(later, wizard, null, null);
                Check(next.Spells.Count == 1, "Later world re-granted first-arc spells");
                Check(CrownShopTransactions.TryPurchase(wizard, next, 1, 4500), "Successive world skip failed");
                Check(wizard.MagicSchoolBehavior.Level == 160 && wizard.QuestBehavior.Registry["QT-DS-ACAD-C01-005"] == 7, "Later skip reset earlier progression");
                Check(wizard.MagicSchoolBehavior.TrainingPoints == 3 + xp.m_levelInfo.Skip(2).Take(159).Sum(l => l.m_trainingPoints), "Successive skip duplicated training points");
            }
        }
        Console.WriteLine($"PASS: {worlds.Length * schools.Length} world purchases, all seven schools, gold/crowns, cumulative quests, school spells, level/XP boosts, higher-level preservation, persistence, quest ownership, insufficient funds and replay protection.");
    }
    static void Store(Raven.Client.Documents.Session.IDocumentSession session, object value, string collection) {
        session.Store(value);
        session.Advanced.GetMetadataFor(value)[Raven.Client.Constants.Documents.Metadata.Collection] = collection;
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
