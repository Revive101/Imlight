using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.CoreLib.Game.Zone.Components;
internal sealed class WoodenChestComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const uint TREASURE_CHEST_TEMPLATE_ID = 77823;

    public string ServiceName => "Interact";

    public string NpcIcon => "GUI/QuestButtons/Art_Quest_Chest_Wood.dds";

    public string NpcNameKey => "WizardGameObjects_00000054";

    public string NpcTextKey => "GUI_ChestInteract";

    public WizBangs WizBang => WizBangs.None;

    public string StateName => "Open";

    public string InteractWizBang => "";

    public string DisplayKey => "";

    public static bool ShouldAttachToEntity(CoreTemplate template) =>
        template is GameObjectTemplate goTemplate
        && goTemplate.m_templateID == TREASURE_CHEST_TEMPLATE_ID;
    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _)
        => [
            new ServiceOptionBase() {
                m_displayKey = DisplayKey,
                m_forceInteract = false,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
                m_serviceIndex = 0
            }
         ];
    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        var goldAmount = Random.Shared.Next(10, 101);

        SendLoot(playerActor, goldAmount);
        UpdateGold(playerActor, playerCharacter, goldAmount);
        PlaySound(playerActor);
        TriggerChestAnimation(); // Rename (doesn't actually trigger any animation)
        RemoveObject();
    }

    private void TriggerChestAnimation() {
        var chestStates = new uint[] { StringHash.Compute(StateName), StringHash.Compute("NotInteracting") };
        for (int i = 0; i < chestStates.Length; i++) {
            var enterState = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
                Data = "",
                GameObjectID = Entity.ActiveGameObject.m_globalID,
                IgnoreIfCurrentStateIsOff = 0,
                State = chestStates[i]
            };

            var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
                Message = enterState,
                Selfless = false,
            };

            Entity.ZoneRef.Tell(broadcastMsg);
        }
    }

    private void SendLoot(IActorRef playerActor, int goldAmount) {
        var lootInfoList = new LootInfoList {
            m_loot = [],
            m_goldInfo = new GoldLootInfo {
                m_goldAmount = goldAmount,
                m_lootType = LOOT_TYPE.LOOT_TYPE_GOLD,
            },
            m_lootRarityList = new LootRarityList {
                m_loot = []
            }
        };

        var serializer = new ObjectSerializer(
            false,
            Behaviors: SerializerFlags.None
        );

        if (!serializer.Serialize(lootInfoList, 4, out var data)) {
            Logger.Error("Failed to serialize LootInfoList.");
            return;
        }

        var loot = new WIZARD_12_PROTOCOL.MSG_LOOT {
            GlobalID = Entity.ActiveGameObject.m_globalID,
            LootList = data
        };

        playerActor.Tell(loot);
    }

    private void UpdateGold(IActorRef playerActor, Wizard playerCharacter, int goldAmount) {
        var updateGold = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = goldAmount,
            MaxGold = playerCharacter.GameStats.m_baseGoldPouch
        };

        playerActor.Tell(updateGold);
    }

    private void PlaySound(IActorRef playerActor) {
        var playSound = new GAME_5_PROTOCOL.MSG_PLAYSOUND {
            SoundID = 89061816524770,
            ReinteractTime = 0,
            SoundFilename = "",
            StartDelay = 0,
            PlayAtMusicVolume = 0,
        };

        playerActor.Tell(playSound);
    }

    private void RemoveObject() {
        var removeObject = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID,
        };

        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = removeObject,
            Selfless = false,
        };

        Entity.ZoneRef.Tell(broadcastMsg);
    }
}
