using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
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
        // 2. Check if the item is in the Gift list

        SendToSocket(new GAME_5_PROTOCOL.MSG_REDEEM_MAIL_GIFT_RESPONSE {
            ErrorCode = 0,
            MailId = message.MailId,
            TemplateId = 1363076
        });
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
