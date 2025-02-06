/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Game.Reagents;


/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

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
    public string WizBang => null;
    public string StateName => null;
    public string InteractWizBang => null;
    public string DisplayKey => null;

    private static readonly Random s_random = new();
    private static readonly CoreObjectSerializer s_reagentAddSerializer
        = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None);
    private static readonly ObjectSerializer s_lootInfoSerializer = new();

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_adjectiveList.Any(x => x == "Reagent");

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter)
        => [new InteractableOption {
            m_serviceName = ServiceName,
        }];

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
            m_lootType = LootInfo.LOOT_TYPE.LOOT_TYPE_ITEM,
            m_numItems = item.Value
        })]
    };

    private static void SendPlayerReagentAddMessage(IActorRef playerActor, ClientReagentItem[] reagents, ulong globalId) {
        foreach (var reagent in reagents) {
            var reagentData = s_reagentAddSerializer.Serialize(reagent);
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
        var lootInfoData = s_lootInfoSerializer.Serialize(lootInfoList);
        var lootInfoMsg = new WIZARD_12_PROTOCOL.MSG_LOOT {
            GlobalID = globalId,
            LootList = lootInfoData,
        };

        playerActor.Tell(lootInfoMsg);
    }

    private static void SendPlayerPickupSound(IActorRef playerActor) {
        var soundId = new GID();
        soundId.MParts.Id = PICKUP_SOUND_TEMPLATE_ID;

        var soundMsg = new GAME_5_PROTOCOL.MSG_PLAYSOUND {
            SoundID = soundId,
        };

        playerActor.Tell(soundMsg);
    }

    private static void SendPlayerRarePickupSound(IActorRef playerActor) {
        var soundId = new GID();
        soundId.MParts.Id = RARE_PICKUP_SOUND_TEMPLATE_ID;

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