/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * GIFTING SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Handles the functionality of gifting
 * 
 * 
 * USAGE EXAMPLE:
 * 
 * 
 * NOTE:
 * 
 * 
 * TODO:
 * - There are 3 types of possible ways for "gifts"
 * 
 * Created by: Phill030
 * Version: KALI 1.0
 * Last Updated: 21.10.2026
 */
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.Game.Pet;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Linq;
using System.Text;

namespace Imlight.CoreLib.Game.Services;

internal class GiftingService(SessionActor sessionActor) : MessageService(sessionActor) {

    private static readonly uint s_maxEmotes
        = ConfigurationManager.Settings["Character.MaxEmotesAllowed"].AsUInt();
    private static readonly uint s_maxTeleportEffects
        = ConfigurationManager.Settings["Character.MaxTeleportsAllowed"].AsUInt();

    private static readonly CoreObjectSerializer s_coreObjectSerializer = new(behaviors: SerializerFlags.None);

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new GiftingService(parentActor));

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();

        if (wizard.Account?.GiftMailbox != null && wizard.Account.GiftMailbox.Count > 0) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_NEW_MAIL {
                AccountMail = 0,
                CharacterID = wizard.CharId,
                MailType = 1
            });
        }
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_RETRIEVE_MAIL))]
    private void ReceiveRetrieveMail(GAME_5_PROTOCOL.MSG_RETRIEVE_MAIL message) {
        SendMailData();
    }

    private void SendMailData() {
        var wizard = GetActiveWizard();
        var mailBox = wizard.Account.GiftMailbox ?? [];

        var mailData = new MailList { m_messages = mailBox };
        var mailSerializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.Compress);
        if (!mailSerializer.Serialize(mailData, 5, out var serializedData)) {
            Logger.Error("Failed to serialize MailList");
            return;
        }

        SendToSocket(new GAME_5_PROTOCOL.MSG_MAIL_DATA {
            Data = serializedData
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REDEEM_MAIL_GIFT))]
    private void ReceiveRedeemGift(GAME_5_PROTOCOL.MSG_REDEEM_MAIL_GIFT message) {
        var wizard = GetActiveWizard();
        if (wizard.Account.GiftMailbox == null) {
            SendRedeemFailure(message.MailId);
            return;
        }

        var selectedMail = wizard.Account.GiftMailbox.SingleOrDefault(m => m.m_mailId == message.MailId, null);
        if (selectedMail == null) {
            SendRedeemFailure(message.MailId);
            return;
        }

        // How could that even happen??
        if (selectedMail.m_recipientId != wizard.AccountId) {
            SendRedeemFailure(message.MailId);
            return;
        }

        if (selectedMail.m_expireDuration > 0 && ((ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds() > selectedMail.m_timeStamp + selectedMail.m_expireDuration)) {
            // Expired, remove the item from the Giftlist
            wizard.Account.RemoveMail(selectedMail.m_mailId);
            InformGameClient("This item expired and you cannot claim it anymore! This item gets removed from the list.");
            SendMailData();
            return;
        }

        if (!TryGetGiftRedemption(selectedMail, out var giftRedemption)) {
            SendRedeemFailure(message.MailId);
            return;
        }

        var itemTemplate = CoreObjectFactory.GetCoreTemplate(giftRedemption.m_itemId);

        // Pre-check inventory space before modifying any state or removing the mail.
        var requiredInventorySlots = CalculateRequiredInventorySlots(itemTemplate, (int) giftRedemption.m_itemCount);
        if (requiredInventorySlots > 0 && !wizard.InventoryBehavior.HasInventorySpace(requiredInventorySlots)) {
            Logger.Warning("Player {0} tried to redeem gift {1}, but inventory is full.",
                Logger.Args(wizard.CharId, message.MailId));
            InformGameClient("Your backpack is full! Please make space before redeeming this gift.");

            SendRedeemFailure(message.MailId);
            return;
        }

        // Check currency pouch limits before modifying state.
        if (itemTemplate is GoldAmountTemplate goldPrecheck) {
            if (wizard.GameStats.m_currentGold + (goldPrecheck.m_goldAmount * (int) giftRedemption.m_itemCount) > wizard.GameStats.m_baseGoldPouch) {
                InformGameClient("You cannot carry any more gold! Please spend some before redeeming this gift.");
                SendRedeemFailure(message.MailId);
                return;
            }
        }
        else if (itemTemplate is LunariAmountTemplate lunariPrecheck) {
            if (wizard.GameStats.m_currentEventCurrency1 + (lunariPrecheck.m_lunariAmount * (int) giftRedemption.m_itemCount) > wizard.GameStats.m_baseEventCurrency1Pouch) {
                InformGameClient("You cannot carry any more Lunari! Please spend some before redeeming this gift.");
                SendRedeemFailure(message.MailId);
                return;
            }
        }

        switch (itemTemplate) {
            case BoosterPackTemplate:
                for (uint i = 0; i < giftRedemption.m_itemCount; i++) {
                    PackManager.OpenPack(SessionActor.ActorRef, wizard, giftRedemption.m_itemId);
                }
                break;

            case GoldAmountTemplate goldItem:
                wizard.AddGold(goldItem.m_goldAmount * (int) giftRedemption.m_itemCount);
                SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
                    Gold = wizard.GameStats.m_currentGold,
                    MaxGold = wizard.GameStats.m_baseGoldPouch
                });
                break;

            case LunariAmountTemplate lunariTemplate:
                wizard.AddLunari(lunariTemplate.m_lunariAmount * (int) giftRedemption.m_itemCount);
                SendToSocket(new WIZARD2_53_PROTOCOL.MSG_UPDATEEVENTCURRENCY1 {
                    EventCurrency1 = wizard.GameStats.m_currentEventCurrency1,
                    MaxEventCurrency1 = wizard.GameStats.m_baseEventCurrency1Pouch
                });
                break;

            case TourneyTokensAmountTemplate:
                // TODO: Tournament currency once implemented
                break;

            // Can pet snacks even be bought?
            case PetSnackItemTemplate petSnackTemplate:
                if (!TryGrantPetSnack(wizard, petSnackTemplate.m_templateID, (int) giftRedemption.m_itemCount)) {
                    SendRedeemFailure(message.MailId);
                    return;
                }
                break;

            case ReagentItemTemplate reagentTemplate:
                if (!TryGrantReagent(wizard, reagentTemplate.m_templateID, (int) giftRedemption.m_itemCount)) {
                    SendRedeemFailure(message.MailId);
                    return;
                }
                break;

            case ItemBundleTemplate itemBundle:
                for (uint i = 0; i < giftRedemption.m_itemCount; i++) {
                    foreach (var bundleItemTemplateId in itemBundle.m_bundleItems) {
                        var bundleSubTemplate = CoreObjectFactory.GetCoreTemplate(bundleItemTemplateId);
                        if (bundleSubTemplate is PetSnackItemTemplate) {
                            if (!TryGrantPetSnack(wizard, bundleItemTemplateId, 1)) {
                                SendRedeemFailure(message.MailId);
                                return;
                            }
                        }
                        else if (bundleSubTemplate is ReagentItemTemplate) {
                            if (!TryGrantReagent(wizard, bundleItemTemplateId, 1)) {
                                SendRedeemFailure(message.MailId);
                                return;
                            }
                        }
                        else {
                            if (!TryGrantItemOrPet(wizard, bundleItemTemplateId)) {
                                SendRedeemFailure(message.MailId);
                                return;
                            }
                        }
                    }
                }
                break;

            default:
                // Check if the item is emote or teleport effect
                var customEmote = itemTemplate?.m_behaviors?.OfType<CustomEmoteBehaviorTemplate>().FirstOrDefault();
                if (customEmote != null && customEmote.m_bitFieldNumber >= 0) {
                    if (customEmote.m_emoteType == CustomEmoteType.CE_Teleport) {
                        if (wizard.CountTeleportEffectsUnlocked() >= s_maxTeleportEffects) {
                            InformGameClient("You have already unlocked the maximum number of teleport effects.");
                            SendRedeemFailure(message.MailId);
                            return;
                        }

                        wizard.UnlockCustomTeleportEffect(customEmote.m_bitFieldNumber);
                    }
                    else if (customEmote.m_emoteType == CustomEmoteType.CE_Emote) {
                        if (wizard.CountEmotesUnlocked() >= s_maxEmotes) {
                            InformGameClient("You have already unlocked the maximum number of emotes.");
                            SendRedeemFailure(message.MailId);
                            return;
                        }

                        wizard.UnlockCustomEmote(customEmote.m_bitFieldNumber);
                    }
                }

                for (uint i = 0; i < giftRedemption.m_itemCount; i++) {
                    if (!TryGrantItemOrPet(wizard, giftRedemption.m_itemId)) {
                        SendRedeemFailure(message.MailId);
                        return;
                    }
                }
                break;
        }

        // Only remove the mail after the items have been successfully redeemed!
        wizard.Account.RemoveMail(selectedMail.m_mailId);

        SendToSocket(new GAME_5_PROTOCOL.MSG_REDEEM_MAIL_GIFT_RESPONSE {
            ErrorCode = 0,
            MailId = message.MailId,
            TemplateId = giftRedemption.m_itemId
        });

        SendMailData();
    }

    private static int CalculateRequiredInventorySlots(CoreTemplate itemTemplate, int count) {
        if (itemTemplate is null ||
            itemTemplate is BoosterPackTemplate ||
            itemTemplate is GoldAmountTemplate ||
            itemTemplate is LunariAmountTemplate ||
            itemTemplate is TourneyTokensAmountTemplate ||
            itemTemplate is PetSnackItemTemplate ||
            itemTemplate is ReagentItemTemplate) {
            return 0;
        }

        if (itemTemplate is ItemBundleTemplate itemBundle) {
            var slotsPerBundle = 0;
            foreach (var templateId in itemBundle.m_bundleItems) {
                var subTemplate = CoreObjectFactory.GetCoreTemplate(templateId);
                if (subTemplate is not PetSnackItemTemplate &&
                    subTemplate is not ReagentItemTemplate &&
                    subTemplate is not BoosterPackTemplate &&
                    subTemplate is not GoldAmountTemplate &&
                    subTemplate is not LunariAmountTemplate) {
                    slotsPerBundle++;
                }
            }
            return slotsPerBundle * (int) count;
        }

        return (int) count;
    }

    private bool TryGrantItemOrPet(Wizard wizard, ulong itemTemplateId) {
        WizClientObjectItem itemCoreObject;
        if (PetFactory.IsPetTemplate((uint) itemTemplateId)) {
            if (!wizard.AddHatchedPetToInventory((uint) itemTemplateId, out itemCoreObject)) {
                Logger.Warning("Could not add hatched pet {0} to inventory.", Logger.Args(itemTemplateId));
                return false;
            }
        }
        else {
            if (!wizard.AddItemToInventory(itemTemplateId, out itemCoreObject)) {
                Logger.Warning("Could not add item {0} to inventory.", Logger.Args(itemTemplateId));
                return false;
            }
        }

        if (!s_coreObjectSerializer.Serialize(itemCoreObject, 24, out var serializedItem)) {
            Logger.Warning("Failed to serialize core object {0}.", Logger.Args(itemTemplateId));
            return false;
        }

        SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = wizard.GameObjectID,
            SerializedItem = serializedItem
        });

        return true;
    }

    private void SendRedeemFailure(ulong mailId) {
        SendToSocket(new GAME_5_PROTOCOL.MSG_REDEEM_MAIL_GIFT_RESPONSE {
            ErrorCode = 1,
            MailId = mailId,
        });
        SendMailData();
    }

    private bool TryGrantPetSnack(Wizard wizard, ulong snackTemplateId, int count) {
        ClientPetSnackItem snackObj = null;
        for (var i = 0; i < count; i++) {
            if (!wizard.AddSnack(snackTemplateId, out snackObj)) {
                Logger.Warning("Could not add pet snack {0} to snack bag.", Logger.Args(snackTemplateId));
                return false;
            }
        }

        if (snackObj is null || !s_coreObjectSerializer.Serialize(snackObj, 24, out var serializedSnackObj)) {
            Logger.Warning("Failed to serialize pet snack core object {0}.", Logger.Args(snackTemplateId));
            return false;
        }

        SendToSocket(new PET_9_PROTOCOL.MSG_PETSNACKADD {
            GlobalID = wizard.GameObjectID,
            Data = serializedSnackObj
        });

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = snackObj.m_globalID,
            ItemTemplateID = (uint) snackObj.m_templateID,
            ItemLocation = 1
        });

        SendToSocket(new PET_9_PROTOCOL.MSG_PETSNACKUPDATE {
            GlobalID = wizard.GameObjectID,
            ItemID = snackObj.m_globalID,
            Quantity = snackObj.m_quantity
        });

        return true;
    }

    private bool TryGrantReagent(Wizard wizard, ulong reagentTemplateId, int count) {
        ClientReagentItem reagentObj = null;
        for (var i = 0; i < count; i++) {
            if (!wizard.AddReagent(reagentTemplateId, out reagentObj)) {
                Logger.Warning("Could not add reagent {0} to inventory.", Logger.Args(reagentTemplateId));
                return false;
            }
        }

        if (reagentObj is null || !s_coreObjectSerializer.Serialize(reagentObj, 27, out var serializedReagentObj)) {
            Logger.Warning("Failed to serialize reagent core object {0}.", Logger.Args(reagentTemplateId));
            return false;
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTADD {
            GlobalID = wizard.GameObjectID,
            Data = serializedReagentObj
        });

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = reagentObj.m_globalID,
            ItemTemplateID = (uint) reagentObj.m_templateID,
            ItemLocation = 1
        });

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE {
            GlobalID = wizard.GameObjectID,
            ItemID = reagentObj.m_globalID,
            Quantity = reagentObj.m_quantity
        });

        return true;
    }

    public static bool TryGetGiftRedemption(Mail mail, out CrownShopGiftRedemption giftRedemption) {
        giftRedemption = null;

        if (string.IsNullOrEmpty(mail.m_messageData)) {
            return false;
        }

        byte[] itemBytes = Encoding.Latin1.GetBytes(mail.m_messageData);
        var serializer = new ObjectSerializer(Versionable: true, Behaviors: SerializerFlags.None);
        return serializer.Deserialize(itemBytes, 31, out giftRedemption);
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_DELIVER_GIFT))]
    private void ReceiveDeliverGift(SERVICE_101_PROTOCOL.MSG_DELIVER_GIFT message) {
        var account = GetActiveAccount();

        if (account is null) {
            return;
        }
        var wizard = GetActiveWizard();

        account.GiftMailbox ??= [];
        account.GiftMailbox.Add(message.Mail);

        SendToSocket(new GAME_5_PROTOCOL.MSG_NEW_MAIL {
            AccountMail = 0,
            CharacterID = wizard.CharId,
            MailType = 1
        });
    }

}
