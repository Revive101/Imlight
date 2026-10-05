using Akka.Actor;
using Akka.Dispatch;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static ICSharpCode.SharpZipLib.Zip.ExtendedUnixData;

namespace Imlight.CoreLib.Game.Services;

internal class GiftingService(SessionActor sessionActor) : MessageService(sessionActor) {

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
        var wizard = GetActiveWizard();

        var mailBox = wizard.Account.GiftMailbox;
        if (mailBox == null || mailBox.Count <= 0) {
            return;
        }

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

        // 1. Get Item by mailId
        // 2. Check if the item is in the Gift list and check if the item is already epired
        // 3. Remove the item from DB and add to Inventory.
        // The message tells the player, that the item was added to their Bank tho.

        if (wizard.Account.GiftMailbox == null || wizard.Account.GiftMailbox.Count <= 0) {
            return;
        }

        var selectedMail = wizard.Account.GiftMailbox.SingleOrDefault(m => m.m_mailId == message.MailId, null);
        if (selectedMail == null) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_REDEEM_MAIL_GIFT_RESPONSE {
                ErrorCode = 1,
                MailId = message.MailId,
            });
            return;
        }

        // How could that even happen??
        if (selectedMail.m_recipientId != wizard.AccountId) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_REDEEM_MAIL_GIFT_RESPONSE {
                ErrorCode = 1,
                MailId = message.MailId,
            });
            return;
        }

        if (selectedMail.m_expireDuration > 0 && ((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds() > selectedMail.m_timeStamp + selectedMail.m_expireDuration)) {
            // Expired, remove the item from the Giftlist
            wizard.Account.RemoveMail(selectedMail.m_mailId);
            InformGameClient("This item expired and you cannot claim it anymore! This item gets removed from the list.");
            return;
        }

        if (!TryGetGiftRedemption(selectedMail, out var giftRedemption)) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_REDEEM_MAIL_GIFT_RESPONSE {
                ErrorCode = 1,
                MailId = message.MailId,
            });
            return;
        }

        wizard.Account.RemoveMail(selectedMail.m_mailId);

        var itemTemplate = CoreObjectFactory.GetCoreTemplate(giftRedemption.m_itemId);
        switch (itemTemplate) {
            case BoosterPackTemplate:
                for (uint i = 0; i < giftRedemption.m_itemCount; i++) {
                    PackManager.OpenPack(SessionActor.ActorRef, wizard, giftRedemption.m_itemId);
                }
                break;
            case GoldAmountTemplate goldItem:
                wizard.AddGold(goldItem.m_goldAmount);
                SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
                    Gold = wizard.GameStats.m_currentGold,
                    MaxGold = wizard.GameStats.m_baseGoldPouch
                });
                break;
            case LunariAmountTemplate lunariTemplate:
                wizard.AddLunari(lunariTemplate.m_lunariAmount);
                SendToSocket(new WIZARD2_53_PROTOCOL.MSG_UPDATEEVENTCURRENCY1 {
                    EventCurrency1 = wizard.GameStats.m_currentEventCurrency1,
                    MaxEventCurrency1 = wizard.GameStats.m_baseEventCurrency1Pouch
                });
                break;
            default:
                // Check if the item is emote or teleport effect
                var customEmote = itemTemplate?.m_behaviors?.OfType<CustomEmoteBehaviorTemplate>().FirstOrDefault();
                if (customEmote != null && customEmote.m_bitFieldNumber >= 0) {
                    if (customEmote.m_emoteType == CustomEmoteType.CE_Teleport) {
                        wizard.UnlockCustomTeleportEffect(customEmote.m_bitFieldNumber);
                    }
                    else {
                        wizard.UnlockCustomEmote(customEmote.m_bitFieldNumber);
                    }
                }

                // todo: serialize the item only once
                var coSerializer = new CoreObjectSerializer(
                    behaviors: Imcodec.ObjectProperty.SerializerFlags.None
                );

                for (uint i = 0; i < giftRedemption.m_itemCount; i++) {
                    if (!wizard.AddItemToInventory(giftRedemption.m_itemId, out WizClientObjectItem itemCoreObject)) {
                        Logger.Warning("Could not add item to inventory.");
                        SendToSocket(new GAME_5_PROTOCOL.MSG_REDEEM_MAIL_GIFT_RESPONSE {
                            ErrorCode = 1,
                            MailId = message.MailId,
                        });
                        return;
                    }

                    if (!coSerializer.Serialize(itemCoreObject, 24, out var serializedItem)) {
                        Logger.Warning("Failed to serialize core object.");
                        return;
                    }

                    SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
                        GlobalID = wizard.GameObjectID,
                        SerializedItem = serializedItem
                    });
                }
                break;
        }

        var mailData = new MailList { m_messages = wizard.Account.GiftMailbox };
        var mailSerializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.Compress);
        if (!mailSerializer.Serialize(mailData, 5, out var serializedData)) {
            Logger.Error("Failed to serialize MailList");
            return;
        }

        SendToSocket(new GAME_5_PROTOCOL.MSG_REDEEM_MAIL_GIFT_RESPONSE {
            ErrorCode = 0,
            MailId = message.MailId,
            TemplateId = giftRedemption.m_itemId
        });

        SendToSocket(new GAME_5_PROTOCOL.MSG_MAIL_DATA {
            Data = serializedData
        });
    }

    public static bool TryGetGiftRedemption(Mail mail, out CrownShopGiftRedemption? giftRedemption) {
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

        account.GiftMailbox ??= new();
        account.GiftMailbox.Add(message.Mail);

        SendToSocket(new GAME_5_PROTOCOL.MSG_NEW_MAIL {
            AccountMail = 0,
            CharacterID = wizard.CharId,
            MailType = 1
        });
    }

}
