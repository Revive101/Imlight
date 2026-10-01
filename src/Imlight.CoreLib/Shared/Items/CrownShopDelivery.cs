using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Pet;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Items;

public sealed class CrownShopDelivery {
    public List<WizClientObjectItem> Items { get; } = [];
    public sealed record SpellReward(uint TemplateId, uint SpellId, bool Treasure);
    public List<SpellReward> Spells { get; } = [];
    public WorldElixirProgression.Plan WorldProgression { get; private set; }
    public List<ulong> CompletedQuestIds { get; } = [];
    public int Gold { get; private set; }
    public List<ClientReagentItem> Reagents { get; } = [];
    public List<ClientPetSnackItem> Snacks { get; } = [];
    public int Lunari { get; private set; }
    public int TourneyTokens { get; private set; }
    public int CharacterSlotLimit { get; private set; }
    public bool RefillHealth { get; private set; }
    public bool RefillMana { get; private set; }
    public bool RefillEnergy { get; private set; }

    public static CrownShopDelivery Create(WizItemTemplate template, Wizard wizard, IActorRef actor, CoreObject player) {
        var delivery = new CrownShopDelivery();
        delivery.Expand(template, wizard, actor, player, new HashSet<uint>());
        if (delivery.WorldProgression == null && delivery.Items.Count == 0 && delivery.Gold == 0 && delivery.Reagents.Count == 0
            && delivery.Snacks.Count == 0 && delivery.Lunari == 0 && delivery.TourneyTokens == 0
            && delivery.Spells.Count == 0 && delivery.CharacterSlotLimit == 0 && !delivery.RefillHealth && !delivery.RefillMana && !delivery.RefillEnergy)
            throw new InvalidOperationException("This item's reward data is unavailable; no currency was charged.");
        return delivery;
    }

    private void Expand(WizItemTemplate template, Wizard wizard, IActorRef actor, CoreObject player, HashSet<uint> chain) {
        if (chain.Count >= 16 || !chain.Add(template.m_templateID) || Items.Count > 1000)
            throw new InvalidOperationException("Invalid or recursive shop bundle.");
        try {
            if (template.m_behaviors?.OfType<WorldElixirBehaviorTemplate>().Any() == true) {
                if (WorldProgression != null) throw new InvalidOperationException("A bundle cannot contain multiple world skips.");
                if (wizard.IsInDuel || wizard.MagicSchoolBehavior.LevelIsLocked != 0)
                    throw new InvalidOperationException("Finish combat and unlock your level before using a world elixir; no currency was charged.");
                _ = SpellFactory.Instance;
                WorldProgression = WorldElixirProgression.Instance.CreatePlan(template.m_templateID, wizard.MagicSchoolBehavior.MagicSchool);
                if (wizard.QuestBehavior.GetRegistryValue(WorldProgression.CompletionKey) > 0)
                    throw new InvalidOperationException("You have already completed this world; no currency was charged.");
                foreach (var name in WorldProgression.Stages.Where(stage => wizard.QuestBehavior.GetRegistryValue(stage.CompletionKey) == 0)
                    .SelectMany(stage => stage.SpellNames).Distinct()) {
                    var spell = SpellFactory.GetSpell(name) ?? throw new InvalidOperationException($"Missing progression spell {name}; no currency was charged.");
                    if (!wizard.SpellbookBehavior.LearnedSpellTemplateIds.Contains((uint)spell.m_templateID))
                        Spells.Add(new((uint)spell.m_templateID, spell.m_spellID, false));
                }
                return;
            }
            if (template.m_behaviors?.OfType<LevelUpElixirBehaviorTemplate>().Any() == true)
                throw new InvalidOperationException("Standalone level-up elixirs are not implemented yet; no currency was charged.");
            if (template is GoldAmountTemplate gold) {
                Gold = checked(Gold + gold.m_goldAmount);
                return;
            }
            if (template is ItemBundleTemplate bundle) {
                // Explicit grant lists are authoritative; legacy bundles store their contents in the display list.
                var ids = bundle.m_bundleItemsToGrant.Count > 0
                    ? bundle.m_bundleItemsToGrant
                    : bundle.m_bundleItems.Concat(bundle.m_bundleFreeItems).ToList();
                foreach (var id in ids) ExpandItem(id, wizard, actor, player, chain);
                return;
            }
            if (template is BoosterPackTemplate pack) {
                var tables = pack.m_lootTables.Select(name => name.ToString()).ToArray();
                if (tables.Length == 0 || tables.Any(name => DropTableCollection.GetDropTable(name) == null))
                    throw new InvalidOperationException("This pack is missing reward tables; no currency was charged.");
                var loot = DropTableRoller.Roll(tables, actor, player, wizard);
                if (loot.ExperienceAmount != 0 || loot.TrainingPoints != 0 || loot.GrantsPotionSlot)
                    throw new InvalidOperationException("This pack has unsupported rewards; no currency was charged.");
                Gold = checked(Gold + loot.GoldAmount);
                foreach (var reward in loot.Items) {
                    var id = reward.ItemId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? ulong.Parse(reward.ItemId[2..], NumberStyles.HexNumber) : ulong.Parse(reward.ItemId);
                    if (reward.Quantity is < 1 or > 1000) throw new InvalidOperationException("Invalid pack quantity.");
                    for (var i = 0; i < reward.Quantity; i++) ExpandItem(id, wizard, actor, player, chain);
                }
                return;
            }
            if (template is LunariAmountTemplate lunari) {
                Lunari = checked(Lunari + lunari.m_lunariAmount);
                return;
            }
            if (template is TourneyTokensAmountTemplate tokens) {
                TourneyTokens = checked(TourneyTokens + tokens.m_tourneyTokensAmount);
                return;
            }
            if (template is ReagentItemTemplate) {
                var reagent = (ClientReagentItem) CoreObjectFactory.FinalizeCoreObject(template.m_templateID);
                reagent.m_characterId = wizard.CharId;
                reagent.m_quantity = 1;
                var existing = Reagents.FirstOrDefault(value => value.m_templateID == reagent.m_templateID);
                if (existing == null) Reagents.Add(reagent); else existing.m_quantity++;
                return;
            }
            if (template is PetSnackItemTemplate) {
                var snack = (ClientPetSnackItem) CoreObjectFactory.FinalizeCoreObject(template.m_templateID);
                snack.m_characterId = wizard.CharId;
                snack.m_quantity = 1;
                var existing = Snacks.FirstOrDefault(value => value.m_templateID == snack.m_templateID);
                if (existing == null) Snacks.Add(snack); else existing.m_quantity++;
                return;
            }
            var actions = template.m_behaviors.OfType<ElixirBehaviorTemplate>()
                .Where(behavior => behavior.m_expireTime.ToString() == "0")
                .SelectMany(behavior => behavior.m_equipActionList?.m_results ?? []).ToList();
            if (actions.Count > 0 && actions.All(action => action is ResAddHealth or ResAddMana or ResAddEnergy or ResAddCharacterSlotResult or ResAddReagent or ResAddGold)) {
                foreach (var action in actions) {
                    switch (action) {
                        case ResAddHealth: RefillHealth = true; break;
                        case ResAddMana: RefillMana = true; break;
                        case ResAddEnergy: RefillEnergy = true; break;
                        case ResAddCharacterSlotResult slots:
                            CharacterSlotLimit = Math.Max(CharacterSlotLimit, slots.m_maximumPurchasedCharacterSlots); break;
                        case ResAddGold goldResult: Gold = checked(Gold + goldResult.m_gold); break;
                        case ResAddReagent reagent:
                            if (reagent.m_quantity is < 1 or > 999) throw new InvalidOperationException("Invalid reagent quantity.");
                            for (var i = 0; i < reagent.m_quantity; i++) ExpandItem(reagent.m_templateID, wizard, actor, player, chain);
                            break;
                    }
                }
                return;
            }
            if (template.m_behaviors.OfType<ElixirBehaviorTemplate>().Any())
                throw new InvalidOperationException("This special service needs an effect handler that is not available yet; no currency was charged.");
            var item = PetFactory.IsPetTemplate(template.m_templateID)
                ? PetFactory.CreatePet(wizard.CharId, template.m_templateID)
                : CoreObjectFactory.FinalizeCoreObject(template.m_templateID) as WizClientObjectItem;
            if (item == null) throw new InvalidOperationException("This shop item cannot be created.");
            item.m_characterId = wizard.CharId;
            if (!PetFactory.IsPetTemplate(template.m_templateID))
                CoreObjectFactory.InitializeCoreObjectBehaviors(item, item.m_templateID);
            Items.Add(item);
        } finally { chain.Remove(template.m_templateID); }
    }

    private void ExpandItem(ulong id, Wizard wizard, IActorRef actor, CoreObject player, HashSet<uint> chain) {
        var template = CoreObjectFactory.GetCoreTemplate(id);
        if (template is SpellTemplate spell) {
            Spells.Add(new SpellReward(checked((uint) id), Imcodec.Cryptography.StringHash.Compute(spell.m_name), spell.m_Treasure));
        } else if (template is WizItemTemplate item) {
            Expand(item, wizard, actor, player, chain);
        } else {
            throw new InvalidOperationException($"Missing bundle item {id}; no currency was charged.");
        }
    }
}
