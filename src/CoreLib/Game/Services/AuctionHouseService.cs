/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Collections;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

internal class AuctionHouseService : MessageService {
    private readonly CoreObjectSerializer _itemSerializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask((SerializerOptions.PropertyFlags) 1);
    private readonly ObjectSerializer _serializer = new ObjectSerializer()
                .OnBehaviors(SerializerOptions.Behaviors.None)
                .OnPropertyMask((SerializerOptions.PropertyFlags) 1);

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
                ConfirmBuyFromAuctionHouse(message.itemTemplateID, message.key);
                break;
            case 2:
                SellToAuctionHouse(message.itemGlobalID, message.key);
                break;
            case 3:
                BuyFromAuctionHouse(message.itemTemplateID, message.texture, message.decal, message.key);
                break;
            case 4:
                // ?
                break;
            case 5:
                // ?
                break;
            case 6:
                // ?
                break;
            case 9:
                // ?
                break;
            default:
                break;
        }
    }

    private void SendAuctionHouseContents(ulong npcId, uint key) {
        // Todo: minimize number of database calls, each player service does not need to fetch
        // all auction house entries. this should be stored in memory somewhere shared.

        // Retrieve all Auction House entries.
        var houseEntryList = AuctionHouseCollection.GetAllAuctionHouseEntries();

        while (houseEntryList.Count > 0) {
            // Contents sent in blocks of up to 50 entries.
            var houseEntryBlock = (houseEntryList.Count >= 50)
                ? houseEntryList.GetRange(0, 50) : houseEntryList.GetRange(0, houseEntryList.Count);

            var auctionHouseEntries = new AuctionHouseOffering {
            m_auctionHousePurchaseKey = key,
                m_auctionList = houseEntryBlock
        };
            var auctionHouseEntriesData = _serializer.Serialize(auctionHouseEntries);

            var auctionHouseContentsMsg = new WIZARD_12_PROTOCOL.MSG_AUCTIONHOUSECONTENTS {
                Contents = auctionHouseEntriesData,
            GlobalID = npcId
        };
            SendToSocket(auctionHouseContentsMsg);

            // Remove first 50 entries from list.
            if (houseEntryList.Count >= 50) {
                houseEntryList.RemoveRange(0, 50);
            } else {
                houseEntryList.RemoveRange(0, houseEntryList.Count);
            }
        }
    }
    }
        }
    }
    }

    private void BuyFromAuctionHouse(ulong templateId, uint key) {

    }

}
