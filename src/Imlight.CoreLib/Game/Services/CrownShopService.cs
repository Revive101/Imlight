using System;
using System.Linq;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Services;

internal class CrownShopService(SessionActor sessionActor) : MessageService(sessionActor) {
    private CrownShopCatalog.Entry _lockedItem;
    private string _search = "";
    private int _page = 1;
    private static readonly CrownShopItemSerializer s_itemSerializer = new();

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE _) => SendBalance();

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_LIST_REQUEST))]
    private void ReceiveList(WIZARD_12_PROTOCOL.MSG_PCS_LIST_REQUEST _) => SendCatalog();

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_CACHESEGREQSSUMMARY_REQUEST))]
    private void ReceiveSummary(WIZARD_12_PROTOCOL.MSG_PCS_CACHESEGREQSSUMMARY_REQUEST _) { }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_REQUEST))]
    private void ReceiveSegmentation(WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_REQUEST _) {
        SendBalance();
        var data = new SegmentationInputData {
            m_bIsValidSegmentationData = true,
            m_playerLevel = GetActiveWizard().GameStats.Level,
            m_accountIsMember = 1,
            m_accountNCrownsInWallet = GetActiveAccount().Crowns ?? 0,
        };
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_RESPONSE {
            Success = 1, Data = CrownShopCatalog.SerializeSegmentation(data),
        });
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_CROWNSHOPPAGE))]
    private void ReceivePage(SERVICE_101_PROTOCOL.MSG_CROWNSHOPPAGE message) {
        _page = Math.Max(1, message.Page);
        _search = message.Search ?? "";
        SendCatalog();
    }

    private void SendCatalog() {
        _lockedItem = null;
        SendBalance();
        try {
            var bytes = CrownShopCatalog.Instance.SerializePage(_search, _page, out var pages);
            _page = Math.Min(_page, pages);
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_LIST_RESPONSE { Data = bytes, UpdateID = 5, Error = 0 });
            Logger.Information("Crown Shop catalog page {0}/{1}, query '{2}', {3} bytes.", Logger.Args(_page, pages, _search, bytes.Length));
        }
        catch (InvalidOperationException exception) {
            // A catalog generation/size error must not terminate the player's session.
            Logger.Error("Crown Shop catalog could not be sent: {0}", Logger.Args(exception.Message));
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_LIST_RESPONSE { Error = 1 });
            InformGameClient("The Crown Shop page could not be loaded. Try .crownshop search <item name>.");
        }
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_REQUEST))]
    private void ReceivePrice(WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_REQUEST message) {
        _lockedItem = message.SaleID == 0 && CrownShopCatalog.Instance.TryGet(message.Item, out var entry) ? entry : null;
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_RESPONSE {
            Item = message.Item, CostGold = _lockedItem?.Gold ?? -1,
            CostCrowns = _lockedItem?.Crowns ?? -1, CostTickets = -1, Error = _lockedItem == null ? 1 : 0,
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST))]
    private void ReceivePurchase(WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST message) {
        var entry = _lockedItem;
        _lockedItem = null; // A consumed price lock cannot be replayed.
        var wizard = GetActiveWizard();
        var cost = message.Type == 0 ? entry?.Gold : message.Type == 1 ? entry?.Crowns : null;
        if (entry == null || message.Item != entry.Id || message.Count != 1 || cost == null
            || message.Cost != cost || message.SaleID != 0
            || (message.Recipient != 0 && message.Recipient != wizard.CharId)
            || message.Texture is < 0 or > 31 || message.Decal is < 0 or > 31) {
            Reply(1);
            return;
        }
        var oldLevel = wizard.MagicSchoolBehavior.Level;
        var oldXp = wizard.MagicSchoolBehavior.ExperiencePoints;
        var committed = false;
        try {
            var delivery = CrownShopDelivery.Create((WizItemTemplate) CoreObjectFactory.GetCoreTemplate(entry.Id), wizard, SessionActor.ActorRef, GetActiveGameObject());
            var existingReagents = wizard.AlchemyBehavior.ReagentItemIds?.ToHashSet() ?? [];
            var existingSnacks = wizard.PetSnackBehavior.SnackItemIds?.ToHashSet() ?? [];
            // Verify every new object can be encoded before spending currency.
            foreach (var reagent in delivery.Reagents)
                if (!s_itemSerializer.Serialize(reagent, 27, out _)) throw new InvalidOperationException("Unable to serialize reagent.");
            foreach (var snack in delivery.Snacks)
                if (!s_itemSerializer.Serialize(snack, 24, out _)) throw new InvalidOperationException("Unable to serialize snack.");
            var serialized = new System.Collections.Generic.List<Imcodec.IO.ByteString>();
            foreach (var item in delivery.Items) {
                if (item.m_templateID.Full == entry.Id) {
                    item.m_primaryColor = message.Texture;
                    item.m_secondaryColor = message.Decal;
                }
                if (!s_itemSerializer.Serialize(item, 1, out var bytes)) { Reply(1); return; }
                serialized.Add(bytes);
            }
            if (!CrownShopTransactions.TryPurchase(wizard, delivery, message.Type, cost.Value)) {
                InformGameClient("Purchase declined: check your balance, inventory space, currency caps and stack limits. No currency was charged.");
                Reply(1);
                return;
            }
            committed = true;
            for (var i = 0; i < delivery.Items.Count; i++) {
                var item = delivery.Items[i];
                SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
                    GlobalID = wizard.GameObjectID, SerializedItem = serialized[i],
                });
                SendToSocket(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
                    ItemGlobalID = item.m_globalID, ItemTemplateID = (uint) item.m_templateID.Full, ItemLocation = 1,
                });
            }
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
                Gold = wizard.GameStats.m_currentGold, MaxGold = wizard.GameStats.m_baseGoldPouch,
            });
            foreach (var reagent in delivery.Reagents) {
                if (existingReagents.Contains(reagent.m_globalID.Full))
                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE { GlobalID = wizard.GameObjectID, ItemID = reagent.m_globalID, Quantity = reagent.m_quantity });
                else {
                    s_itemSerializer.Serialize(reagent, 27, out var bytes);
                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTADD { GlobalID = wizard.GameObjectID, Data = bytes });
                }
            }
            foreach (var snack in delivery.Snacks) {
                if (existingSnacks.Contains(snack.m_globalID.Full))
                    SendToSocket(new PET_9_PROTOCOL.MSG_PETSNACKUPDATE { GlobalID = wizard.GameObjectID, ItemID = snack.m_globalID, Quantity = snack.m_quantity });
                else {
                    s_itemSerializer.Serialize(snack, 24, out var bytes);
                    SendToSocket(new PET_9_PROTOCOL.MSG_PETSNACKADD { GlobalID = wizard.GameObjectID, Data = bytes });
                }
            }
            foreach (var spell in delivery.Spells) {
                if (spell.Treasure)
                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDTREASURESPELLTOBOOK { SpellID = (int) spell.SpellId, EnchantmentID = 0 });
                else
                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK { SpellID = (int) spell.TemplateId });
            }
            if (delivery.Lunari > 0)
                SendToSocket(new WIZARD2_53_PROTOCOL.MSG_UPDATEEVENTCURRENCY1 { EventCurrency1 = wizard.GameStats.m_currentEventCurrency1, MaxEventCurrency1 = int.MaxValue });
            if (delivery.TourneyTokens > 0)
                SendToSocket(new WIZARD3_56_PROTOCOL.MSG_UPDATEPVPTOURNEYCURRENCY { PvPTourneyCurrency = wizard.GameStats.m_currentPvPTourneyCurrency, MaxPvPTourneyCurrency = int.MaxValue });
            if (delivery.RefillHealth)
                SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH { CharacterID = wizard.GameObjectID, NewHealth = wizard.GameStats.m_currentHitpoints, NewHealthMax = wizard.GameStats.m_baseHitpoints, DisplayDiff = 1 });
            if (delivery.RefillMana)
                SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA { Mana = wizard.GameStats.m_currentMana, MaxMana = wizard.GameStats.m_baseMana, DisplayDiff = 1 });
            if (delivery.RefillEnergy)
                SendToSocket(new PET_9_PROTOCOL.MSG_PETENERGYTICK { GlobalID = wizard.GameObjectID, Energy = wizard.PetOwnerBehavior.Energy, MaxEnergy = wizard.GameStats.m_energyMax, TickTime = (int) wizard.PetOwnerBehavior.LastEnergyTickEpoch });
            if (delivery.CharacterSlotLimit > 0) InformGameClient("Character slot added. Return to character selection to use it.");
            if (delivery.WorldProgression is { } world) {
                foreach (var questId in delivery.CompletedQuestIds) {
                    SendToSocket(new QUEST_MESSAGES_52_PROTOCOL.MSG_COMPLETEQUEST { QuestID = questId });
                    SendToSocket(new QUEST_MESSAGES_52_PROTOCOL.MSG_REMOVEQUEST { QuestID = questId });
                }
                if (wizard.MagicSchoolBehavior.Level > oldLevel) {
                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_LEVELUP {
                        GlobalID = wizard.GameObjectID, NewLevel = wizard.MagicSchoolBehavior.Level, Data = "0000000000",
                        XP = wizard.MagicSchoolBehavior.ExperiencePoints, TrainingPoints = wizard.MagicSchoolBehavior.TrainingPoints,
                    });
                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEXP {
                        GlobalID = wizard.GameObjectID, OldXP = oldXp, XP = wizard.MagicSchoolBehavior.ExperiencePoints - oldXp,
                    });
                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH {
                        CharacterID = wizard.GameObjectID, NewHealth = wizard.GameStats.m_currentHitpoints,
                        NewHealthMax = wizard.GameStats.m_baseHitpoints, DisplayDiff = 1,
                    });
                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
                        Mana = wizard.GameStats.m_currentMana, MaxMana = wizard.GameStats.m_baseMana, DisplayDiff = 1,
                    });
                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEPOWERPIP { PowerPip = wizard.GameStats.m_powerPipBase });
                    SendToSocket(new PET_9_PROTOCOL.MSG_PETENERGYMAX { MaxEnergy = wizard.GameStats.m_energyMax });
                    TellOtherServices(new GROUP_109_PROTOCOL.MSG_LEVELCHANGED {
                        CharId = wizard.CharId, NewLevel = (uint) wizard.MagicSchoolBehavior.Level,
                    });
                }
                InformGameClient($"Completed through {world.World}. Your wizard is level {wizard.MagicSchoolBehavior.Level}.");
            }
            SendBalance();
            Reply(0);
            if (delivery.WorldProgression is { } completedWorld)
                SessionActor.ActorRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
                    DestinationZone = completedWorld.Destination, DestinationLocation = "Start",
                    SendToClient = true, OwnerCharId = wizard.CharId,
                });
        } catch (Exception error) {
            Logger.Error("Crown Shop purchase failed: {0}", Logger.Args(error));
            if (committed) {
                InformGameClient("Purchase saved. Reconnect to refresh your inventory and balance.");
                Reply(0);
            } else {
                InformGameClient(error is InvalidOperationException ? error.Message : "Purchase failed before saving; no currency was charged.");
                Reply(1);
            }
        }
        void Reply(int error) => SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
            Item = message.Item, Type = message.Type, Cost = cost ?? 0, Count = 1, Error = error,
        });
    }

    private void SendBalance() {
        var wizard = GetActiveWizard();
        var account = GetActiveAccount();
        account.Crowns = AccountCollection.EnsureStartingCrowns(account.AccountId);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_CROWNBALANCE {
            Failure = 0, TotalCrowns = account.Crowns.Value,
            CharacterID = wizard.CharId, CacheBalanceForCSSegmentation = 1,
        });
    }
}
