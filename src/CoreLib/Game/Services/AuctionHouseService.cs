/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Networking;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

internal class AuctionHouseService : MessageService {

    public AuctionHouseService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new AuctionHouseService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_AUCTIONHOUSEREQUEST))]
    private void ReceiveAuctionHouseRequest(WIZARD_12_PROTOCOL.MSG_AUCTIONHOUSEREQUEST message) {

        switch (message.Command) {
            case 0:
                SendAuctionHouseContents(message.npcGlobalID, message.key);
                break;
            case 1:
                BuyFromAuctionHouse(message.itemTemplateID, message.key);
                break;
            default:
                break;
        }
    }

    private void SendAuctionHouseContents(ulong npcId, uint key) {
        var serializer = new ObjectSerializer()
          .OnBehaviors(SerializerOptions.Behaviors.None)
          .OnPropertyMask((SerializerOptions.PropertyFlags) 1);

        var houseEntry = new AuctionHouseEntry {
            m_templateID = (GID) (106959),
            m_numForSale = 100,
            m_buyPrice = 10000,
            m_sellPrice = 2500
        };
        var houseEntryList = new List<AuctionHouseEntry>() { houseEntry };

        var auctionHouseEntry = new AuctionHouseOffering {
            m_auctionHousePurchaseKey = key,
            m_auctionList = houseEntryList
        };
        var auctionHouseEntryData = serializer.Serialize(auctionHouseEntry);

        var auctionHouseResponse = new WIZARD_12_PROTOCOL.MSG_AUCTIONHOUSECONTENTS {
            Contents = auctionHouseEntryData,
            GlobalID = npcId
        };
        SendToSocket(auctionHouseResponse);
    }

    private void BuyFromAuctionHouse(ulong templateId, uint key) {

    }

}
