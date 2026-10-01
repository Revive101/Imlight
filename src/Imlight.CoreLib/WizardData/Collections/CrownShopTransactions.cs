using System;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Collections;

public static class CrownShopTransactions {
    /// <summary>Persist debit, item and inventory membership in one RavenDB transaction.</summary>
    public static bool TryPurchase(Wizard wizard, CrownShopDelivery delivery, int currency, int cost) {
        if (delivery == null || cost <= 0 || currency is < 0 or > 1) return false;
        return AccountCollection.WithWriteLane(wizard.AccountId, () =>
            WizardCollection.WithWriteLane(wizard.CharId, () => {
                using var session = PlayerDatabase.Instance.Store.OpenSession();
                var account = session.Query<Account>(collectionName: AccountCollection.CollectionName)
                    .Customize(query => query.WaitForNonStaleResults())
                    .FirstOrDefault(value => value.AccountId == wizard.AccountId);
                var saved = session.Query<Wizard>(collectionName: WizardCollection.CollectionName)
                    .Customize(query => query.WaitForNonStaleResults())
                    .FirstOrDefault(value => value.CharId == wizard.CharId);
                if (account == null || saved == null || saved.AccountId != account.AccountId) return false;
                if (delivery.WorldProgression is { } world && (saved.QuestBehavior == null
                    || saved.MagicSchoolBehavior == null || wizard.IsInDuel || saved.MagicSchoolBehavior.LevelIsLocked != 0
                    || saved.QuestBehavior.GetRegistryValue(world.CompletionKey) > 0)) return false;
                var configuredMax = ConfigurationManager.Settings["Character.MaxInventoryItems"].AsInt();
                var maxItems = (configuredMax > 0 ? configuredMax : 20) + wizard.GameStats.m_extraInventorySpace;
                if (saved.InventoryBehavior.InventoryItemIds.Count + delivery.Items.Count > maxItems
                    || wizard.InventoryBehavior.Items.Count + delivery.Items.Count > maxItems) return false;
                if (delivery.CharacterSlotLimit > 0 && account.PurchasedCharacterSlots >= delivery.CharacterSlotLimit)
                    return false;
                if (delivery.Gold < 0 || delivery.Lunari < 0 || delivery.TourneyTokens < 0) return false;
                account.Crowns ??= 10_000;
                if (currency == 0) {
                    if (saved.GameStats.m_currentGold < cost || wizard.GameStats.m_currentGold < cost) return false;
                    saved.GameStats.m_currentGold -= cost;
                } else {
                    if (account.Crowns < cost) return false;
                    account.Crowns -= cost;
                }
                if ((long) saved.GameStats.m_currentGold + delivery.Gold > wizard.GameStats.m_baseGoldPouch) return false;
                saved.GameStats.m_currentGold = checked(saved.GameStats.m_currentGold + delivery.Gold);
                foreach (var item in delivery.Items) {
                    saved.InventoryBehavior.InventoryItemIds.Add(item.m_globalID);
                    session.Store(item);
                    session.Advanced.GetMetadataFor(item)[Raven.Client.Constants.Documents.Metadata.Collection] = WizardItemCollection.CollectionName;
                }
                // Stack updates share the same database commit as the debit.
                for (var i = 0; i < delivery.Reagents.Count; i++) {
                    var added = delivery.Reagents[i];
                    var existing = session.Query<ClientReagentItem>(collectionName: WizardReagentCollection.CollectionName)
                        .Customize(query => query.WaitForNonStaleResults())
                        .FirstOrDefault(value => value.m_characterId == added.m_characterId && value.m_templateID == added.m_templateID);
                    if (added.m_quantity <= 0 || (existing?.m_quantity ?? 0) + added.m_quantity > 999) return false;
                    if (existing != null) {
                        existing.m_quantity += added.m_quantity;
                        delivery.Reagents[i] = existing;
                    } else {
                        saved.AlchemyBehavior.ReagentItemIds ??= [];
                        saved.AlchemyBehavior.ReagentItemIds.Add(added.m_globalID);
                        session.Store(added);
                        session.Advanced.GetMetadataFor(added)[Raven.Client.Constants.Documents.Metadata.Collection] = WizardReagentCollection.CollectionName;
                    }
                }
                for (var i = 0; i < delivery.Snacks.Count; i++) {
                    var added = delivery.Snacks[i];
                    var existing = session.Query<ClientPetSnackItem>(collectionName: WizardPetSnackCollection.CollectionName)
                        .Customize(query => query.WaitForNonStaleResults())
                        .FirstOrDefault(value => value.m_characterId == added.m_characterId && value.m_templateID == added.m_templateID);
                    if (added.m_quantity <= 0 || (existing?.m_quantity ?? 0) + added.m_quantity > 999) return false;
                    if (existing != null) {
                        existing.m_quantity += added.m_quantity;
                        delivery.Snacks[i] = existing;
                    } else {
                        saved.PetSnackBehavior.SnackItemIds ??= [];
                        saved.PetSnackBehavior.SnackItemIds.Add(added.m_globalID);
                        session.Store(added);
                        session.Advanced.GetMetadataFor(added)[Raven.Client.Constants.Documents.Metadata.Collection] = WizardPetSnackCollection.CollectionName;
                    }
                }
                saved.GameStats.m_currentEventCurrency1 = checked(saved.GameStats.m_currentEventCurrency1 + delivery.Lunari);
                saved.GameStats.m_currentPvPTourneyCurrency = checked(saved.GameStats.m_currentPvPTourneyCurrency + delivery.TourneyTokens);
                if (delivery.CharacterSlotLimit > 0) account.PurchasedCharacterSlots++;
                if (delivery.RefillHealth) saved.GameStats.m_currentHitpoints = wizard.GameStats.m_baseHitpoints;
                if (delivery.RefillMana) saved.GameStats.m_currentMana = wizard.GameStats.m_baseMana;
                if (delivery.RefillEnergy) saved.PetOwnerBehavior.SetEnergy(wizard.GameStats.m_energyMax);
                saved.SpellbookBehavior ??= new();
                foreach (var spell in delivery.Spells) {
                    if (spell.Treasure) saved.SpellbookBehavior.AddTreasureCard(spell.TemplateId);
                    else if (!saved.SpellbookBehavior.LearnedSpellTemplateIds.Contains(spell.TemplateId))
                        saved.SpellbookBehavior.LearnedSpellTemplateIds.Add(spell.TemplateId);
                }
                if (delivery.WorldProgression != null) ApplyWorldProgression(session, saved, wizard, delivery);
                session.SaveChanges();
                if (delivery.WorldProgression != null) {
                    wizard.QuestBehavior.Registry.Clear();
                    foreach (var entry in saved.QuestBehavior.Registry) wizard.QuestBehavior.Registry.Add(entry.Key, entry.Value);
                    wizard.QuestBehavior.CurrentQuestIDs.Clear();
                    wizard.QuestBehavior.CurrentQuestIDs.AddRange(saved.QuestBehavior.CurrentQuestIDs);
                    wizard.QuestBehavior.CurrentQuestInstances.RemoveAll(quest => delivery.CompletedQuestIds.Contains(quest.ID));
                    wizard.MagicSchoolBehavior = saved.MagicSchoolBehavior;
                    wizard.GameStats.Level = saved.GameStats.Level;
                    wizard.GameStats.m_baseHitpoints = saved.GameStats.m_baseHitpoints;
                    wizard.GameStats.m_baseMana = saved.GameStats.m_baseMana;
                    wizard.GameStats.m_powerPipBase = saved.GameStats.m_powerPipBase;
                    wizard.GameStats.m_energyMax = saved.GameStats.m_energyMax;
                    wizard.GameStats.m_currentHitpoints = saved.GameStats.m_currentHitpoints;
                    wizard.GameStats.m_currentMana = saved.GameStats.m_currentMana;
                    wizard.GameStats.m_potionMax = saved.GameStats.m_potionMax;
                }
                wizard.GameStats.m_currentGold = saved.GameStats.m_currentGold;
                wizard.Account.Crowns = account.Crowns;
                wizard.InventoryBehavior.InventoryItemIds.AddRange(delivery.Items.Select(item => item.m_globalID.Full));
                wizard.InventoryBehavior.Items.AddRange(delivery.Items);
                wizard.GameStats.m_currentEventCurrency1 = saved.GameStats.m_currentEventCurrency1;
                wizard.GameStats.m_currentPvPTourneyCurrency = saved.GameStats.m_currentPvPTourneyCurrency;
                wizard.Account.PurchasedCharacterSlots = account.PurchasedCharacterSlots;
                if (delivery.RefillHealth) wizard.GameStats.m_currentHitpoints = saved.GameStats.m_currentHitpoints;
                if (delivery.RefillMana) wizard.GameStats.m_currentMana = saved.GameStats.m_currentMana;
                if (delivery.RefillEnergy) wizard.PetOwnerBehavior.SetEnergy(saved.PetOwnerBehavior.Energy);
                wizard.AlchemyBehavior.Reagents ??= [];
                wizard.AlchemyBehavior.ReagentItemIds ??= [];
                foreach (var reagent in delivery.Reagents) {
                    wizard.AlchemyBehavior.Reagents.RemoveAll(value => value.m_templateID == reagent.m_templateID);
                    wizard.AlchemyBehavior.Reagents.Add(reagent);
                    if (!wizard.AlchemyBehavior.ReagentItemIds.Contains(reagent.m_globalID)) wizard.AlchemyBehavior.ReagentItemIds.Add(reagent.m_globalID);
                }
                wizard.PetSnackBehavior.Snacks ??= [];
                wizard.PetSnackBehavior.SnackItemIds ??= [];
                foreach (var snack in delivery.Snacks) {
                    wizard.PetSnackBehavior.Snacks.RemoveAll(value => value.m_templateID == snack.m_templateID);
                    wizard.PetSnackBehavior.Snacks.Add(snack);
                    if (!wizard.PetSnackBehavior.SnackItemIds.Contains(snack.m_globalID)) wizard.PetSnackBehavior.SnackItemIds.Add(snack.m_globalID);
                }
                wizard.SpellbookBehavior ??= new();
                wizard.SpellbookBehavior.LearnedSpellTemplateIds = saved.SpellbookBehavior.LearnedSpellTemplateIds;
                wizard.SpellbookBehavior.TreasureCardTemplateIds = saved.SpellbookBehavior.TreasureCardTemplateIds;
                return true;
            }));
    }
    private static void ApplyWorldProgression(Raven.Client.Documents.Session.IDocumentSession session,
        Wizard saved, Wizard live, CrownShopDelivery delivery) {
        var plan = delivery.WorldProgression;
        var questNames = plan.Stages.SelectMany(stage => stage.Quests).ToHashSet(StringComparer.Ordinal);
        foreach (var stage in plan.Stages) {
            // Registry resets belong to the original skip and must never replay on an
            // already-completed world. Quest flags themselves are idempotent.
            if (saved.QuestBehavior.GetRegistryValue(stage.CompletionKey) == 0) {
                foreach (var entry in stage.Registry) {
                    var key = entry.m_questRegistry ? entry.m_questName + "_" + entry.m_registryEntryName : entry.m_registryEntryName;
                    if (entry.m_value == 0) saved.QuestBehavior.Registry.Remove(key);
                    else saved.QuestBehavior.Registry[key] = Math.Max(saved.QuestBehavior.GetRegistryValue(key), checked((ulong) entry.m_value));
                }
            }
            foreach (var quest in stage.Quests) saved.QuestBehavior.Registry[quest + "_Complete"] = 1;
        }
        var active = session.Query<QuestInstance>(collectionName: "QuestInstances")
            .Customize(query => query.WaitForNonStaleResults()).Where(quest => quest.OwnerCharId == saved.CharId).ToList();
        delivery.CompletedQuestIds.Clear();
        foreach (var quest in active.Where(quest => questNames.Contains(quest.QuestName))) {
            delivery.CompletedQuestIds.Add(quest.ID);
            saved.QuestBehavior.CurrentQuestIDs.Remove(quest.ID);
            session.Delete(quest);
        }
        // Include in-memory quests whose documents were already removed, if any.
        foreach (var quest in live.QuestBehavior.CurrentQuestInstances.Where(quest => questNames.Contains(quest.QuestName))) {
            if (!delivery.CompletedQuestIds.Contains(quest.ID)) delivery.CompletedQuestIds.Add(quest.ID);
            saved.QuestBehavior.CurrentQuestIDs.Remove(quest.ID);
        }
        _ = MagicLevelsConfig.Instance;
        var oldLevel = saved.MagicSchoolBehavior.Level;
        var newLevel = Math.Max(oldLevel, Math.Min(plan.EndLevel, MagicLevelsConfig.MaxLevel));
        var school = saved.MagicSchoolBehavior.MagicSchool;
        var oldStats = MagicLevelsConfig.GetPlayerLevelInfo(school, oldLevel)
            ?? throw new InvalidOperationException("Missing current-level stats; no currency was charged.");
        var newStats = MagicLevelsConfig.GetPlayerLevelInfo(school, newLevel)
            ?? throw new InvalidOperationException("Missing world-level stats; no currency was charged.");
        saved.MagicSchoolBehavior.Level = newLevel;
        saved.GameStats.Level = newLevel;
        // Equipment-derived stats only exist on the attached wizard, not the DB copy.
        saved.GameStats.m_baseHitpoints = live.GameStats.m_baseHitpoints + newStats.m_hitpoints - oldStats.m_hitpoints;
        saved.GameStats.m_baseMana = live.GameStats.m_baseMana + newStats.m_mana - oldStats.m_mana;
        saved.GameStats.m_powerPipBase = live.GameStats.m_powerPipBase + newStats.m_pipChance - oldStats.m_pipChance;
        saved.GameStats.m_energyMax = live.GameStats.m_energyMax + newStats.m_petEnergy - oldStats.m_petEnergy;
        if (newLevel > oldLevel) {
            for (var level = oldLevel + 1; level <= newLevel; level++)
                saved.MagicSchoolBehavior.TrainingPoints = checked(saved.MagicSchoolBehavior.TrainingPoints
                    + MagicLevelsConfig.GetPlayerLevelInfo(school, level).m_trainingPoints);
            saved.MagicSchoolBehavior.ExperiencePoints = Math.Max(saved.MagicSchoolBehavior.ExperiencePoints,
                MagicLevelsConfig.GetExperiencePointsAtLevel(newLevel));
            saved.GameStats.m_currentHitpoints = saved.GameStats.m_baseHitpoints;
            saved.GameStats.m_currentMana = saved.GameStats.m_baseMana;
        }
        saved.GameStats.m_potionMax = Math.Max(saved.GameStats.m_potionMax, plan.MaxPotions);
    }

}
