/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Reagents;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractReagentComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const float RARE_REAGENT_CHANCE = 0.1f;
    private const float INITIAL_ADDITIONAL_REAGENT_CHANCE = 0.1f;
    private const float ADDITIONAL_REAGENT_CHANCE_REDUCTION = 0.5f;
    private const int MAX_ADDITIONAL_REAGENT_ROLLS = 5;
    private const uint PICKUP_SOUND_TEMPLATE_ID = 1309960781;
    private const uint RARE_PICKUP_SOUND_TEMPLATE_ID = 1051090169;

    public string ServiceName => "Interact";
    public string NpcIcon {
        get {
            // todo: Not all icons come from shared worlddata.
            var goTemplate = Entity.Template as GameObjectTemplate;
            return $"|_Shared|WorldData|{goTemplate.m_sIcon}";
        }
    }
    public string NpcNameKey {
        get {
            var goTemplate = Entity.Template as GameObjectTemplate;
            var reagentItemTemplate = ReagentFactory.GetReagentTemplate(goTemplate.m_objectName);
            return reagentItemTemplate.m_displayName;
        }
    }
    public string NpcTextKey => "GUI_CollectItem";
    public WizBangs WizBang => WizBangs.None;
    public string StateName => null;
    public string InteractWizBang => null;
    public string DisplayKey => null;

    private static readonly Random s_random = new();
    private static readonly CoreObjectSerializer s_reagentAddSerializer = new(
        behaviors: SerializerFlags.None
    );
    private static readonly ObjectSerializer s_lootInfoSerializer = new(
        Versionable: false,
        Behaviors: SerializerFlags.None
    );

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_adjectiveList is not null
        && goTemplate.m_adjectiveList.Any(x => x == "Reagent");

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _)
        => [ new InteractableOption { m_serviceName = ServiceName }];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        var quantity = RollReagentQuantity();
        var reagent = GetReagent(playerCharacter.CharId, quantity);
        if (reagent is null) {
            Logger.Error("Failed to get reagent for character {0} and quantity {1}",
                Logger.Args(playerCharacter.CharId, quantity));

            return;
        }

        // Determine if the reagent is rare and get the rare reagent if it is.
        var isRare = IsRareReagent();
        var rareReagent = isRare ? GetRareReagent(playerCharacter.CharId) : null;

        // Add all reagents to the player's inventory.
        for (int i = 0; i < quantity; i++) {
            playerCharacter.AddReagent(reagent);
        }
        if (isRare) {
            playerCharacter.AddReagent(rareReagent);
        }

        // Inform the game client that the player has gathered reagents.
        var reagents = isRare ? new[] { reagent, rareReagent } : [reagent];
        SendPlayerReagentAddMessage(playerActor, reagents, playerCharacter.CharId);

        // Inform the game client that they have gathered loot.
        SendPlayerLootInfoMessage(playerActor, new Dictionary<ulong, int> {
            [reagent.m_templateID] = reagent.m_quantity,
            [rareReagent?.m_templateID ?? 0] = rareReagent?.m_quantity ?? 0,
        }, playerCharacter.CharId);

        // Play the pickup sound.
        if (isRare) {
            SendPlayerRarePickupSound(playerActor);
        }
        else {
            SendPlayerPickupSound(playerActor);
        }

        var leaveServiceRangeMsg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
            MobileID = Entity.ActiveGameObject.m_globalID
        };
        playerActor.Tell(leaveServiceRangeMsg);

        // Finally, destroy this entity.
        Entity.DeleteObject();
    }

    private ClientReagentItem GetReagent(ulong charId, int quantity) {
        var goTemplate = Entity.Template as GameObjectTemplate;
        var item = ReagentFactory.GetHarvestable(goTemplate.m_objectName);

        if (item is null) {
            return null;
        }

        item.m_quantity = quantity;
        item.m_characterId = (GID) charId;

        return item;
    }

    private ClientReagentItem GetRareReagent(ulong charId) {
        var goTemplate = Entity.Template as GameObjectTemplate;
        var item = ReagentFactory.GetHarvestableRareVariant(goTemplate.m_objectName);

        if (item is null) {
            return null;
        }

        item.m_quantity = 1;
        item.m_characterId = (GID) charId;

        return item;
    }

    private static LootInfoList GetLootInfoList(Dictionary<ulong, int> items) => new() {
        m_loot = [.. items.Select(item => (LootInfo) new ItemLootInfo {
            m_itemID = (GID) item.Key,
            m_lootType = LOOT_TYPE.LOOT_TYPE_ITEM,
            m_numItems = item.Value
        })]
    };

    private static void SendPlayerReagentAddMessage(IActorRef playerActor, ClientReagentItem[] reagents, ulong globalId) {
        foreach (var reagent in reagents) {
            // Serialize the reagent and send it to the player.
            if (!s_reagentAddSerializer.Serialize(reagent, 1, out var reagentData)) {
                Logger.Error("Failed to serialize reagent {0}", 
                    Logger.Args(reagent));
                    
                return;
            }

            var newReagentMsg = new WIZARD_12_PROTOCOL.MSG_REAGENTADD {
                GlobalID = globalId,
                Data = reagentData,
            };

            playerActor.Tell(newReagentMsg);
        }
    }

    private static void SendPlayerLootInfoMessage(IActorRef playerActor, Dictionary<ulong, int> reagents, ulong globalId) {
        // Remove any reagents with a quantity of 0.
        reagents = reagents.Where(x => x.Value > 0).ToDictionary(x => x.Key, x => x.Value);

        var lootInfoList = GetLootInfoList(reagents);

        // Serialize the loot info list and send it to the player.
        if (!s_lootInfoSerializer.Serialize(lootInfoList, 1, out var lootInfoData)) {
            Logger.Error("Failed to serialize loot info list {0}", 
                Logger.Args(lootInfoList));
                
            return;
        }

        var lootInfoMsg = new WIZARD_12_PROTOCOL.MSG_LOOT {
            GlobalID = globalId,
            LootList = lootInfoData,
        };

        playerActor.Tell(lootInfoMsg);
    }

    private static void SendPlayerPickupSound(IActorRef playerActor) {
        var soundId = new GID();
        soundId.MParts.TemplateId = PICKUP_SOUND_TEMPLATE_ID;

        var soundMsg = new GAME_5_PROTOCOL.MSG_PLAYSOUND {
            SoundID = soundId,
        };

        playerActor.Tell(soundMsg);
    }

    private static void SendPlayerRarePickupSound(IActorRef playerActor) {
        var soundId = new GID();
        soundId.MParts.TemplateId = RARE_PICKUP_SOUND_TEMPLATE_ID;

        var soundMsg = new GAME_5_PROTOCOL.MSG_PLAYSOUND {
            SoundID = soundId,
        };

        playerActor.Tell(soundMsg);
    }

    private static bool IsRareReagent()
        => s_random.NextDouble() < RARE_REAGENT_CHANCE;

    private static int RollReagentQuantity() {
        var quantity = 1;
        var currentChance = INITIAL_ADDITIONAL_REAGENT_CHANCE;
        var rollCount = 0;

        while (s_random.NextDouble() < currentChance && rollCount < MAX_ADDITIONAL_REAGENT_ROLLS) {
            quantity++;
            currentChance *= ADDITIONAL_REAGENT_CHANCE_REDUCTION;
            rollCount++;
        }

        return quantity;
    }

}