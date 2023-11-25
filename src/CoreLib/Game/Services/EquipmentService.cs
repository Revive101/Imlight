using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

public class EquipmentService : MessageService {
    private readonly Dictionary<int, GameEffectInfo> _gameEffects;
    private int _effectInternalIDCounter = 1;

    public EquipmentService(SessionActor sessionActor) : base(sessionActor) {
        _gameEffects = new Dictionary<int, GameEffectInfo>();
    }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InventoryService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_EQUIPITEM))]
    private void ReceiveEquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        if (message.IsEquip == 1) {
            EquipItem(message);
        }
        else {
            UnEquipItem(message);
        }
    }

    private void EquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        // @TODO: There should be some "AntiAmbrose" logic here. Double check that the player meets the requirements
        // to equip this item and that the player does not already have an item equipped in its slot.
        var coreObject = GetActiveCoreObject();
        var playerCharacter = GetActiveCharacter();

        if (!ItemInInventory(message.ItemID, coreObject)) {
            // @TODO: Respond to client appropriately.
            Logger.Debug($"Player does not have the item in their inventory!");
            return;
        }

        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(coreObject,
                        out var inventoryBehavior)) {
            return;
        }
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(coreObject,
            out var equipmentBehavior)) {
            return;
        }

        // Get item object and its template.
        var itemObj = inventoryBehavior.m_itemList.First(item => item.m_globalID == message.ItemID);
        var templateId = itemObj.m_templateID;
        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(templateId);

        // Check equipped items to see if player already has an item equipped in the target slot.
        foreach (CoreObject obj in equipmentBehavior.m_itemList) {
            var objTemplate = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(obj.m_templateID);
            if (objTemplate.m_adjectiveList[1] == template.m_adjectiveList[1]) {
                // Get current equipped item and its slot.
                var slot = equipmentBehavior.m_slotList.FindIndex(slot => slot.m_itemID == obj.m_globalID);
                var currentEquippedItem = equipmentBehavior.m_slotList[slot].m_itemID;
                var oldItemTemplateID = inventoryBehavior.m_itemList.First(item => item.m_globalID == currentEquippedItem).m_templateID;
                var oldItemTemplate = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(oldItemTemplateID);

                if (currentEquippedItem == 0) {
                    Logger.Debug("Player somehow has an item with GID: 0!");
                    return;
                }

                // Remove item from equipment behavior lists.
                equipmentBehavior.m_slotList = RemoveSlotFromEquipmentSlotList(slot, equipmentBehavior.m_slotList);
                equipmentBehavior.m_itemList.RemoveAll(item => item.m_globalID == currentEquippedItem);
                //equipmentBehavior.m_publicItemList.RemoveAll(item => item.m_itemID == itemObj.m_templateID);
                //creationEquipment.RemoveAll(item => item.m_itemID == itemObj.m_templateID);

                // Unequip the previous item.
                SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
                    ItemID = obj.m_globalID,
                    SlotName = "",
                    IsEquip = 0
                });
                ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM() {
                    GlobalID = coreObject.m_globalID,
                    IndexToRemove = (byte) slot
                }, false);

                RemoveItemEffectsFromPlayer(coreObject, oldItemTemplate);
                break;
            }
        }

        // Confirm to the player that we've equipped their item server side.
        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
            ItemID = message.ItemID,
            SlotName = message.SlotName,
            IsEquip = message.IsEquip
        });

        // Put ID of equipped item in first empty slot in the slot list, and update itemList, creationList, publicItemList.
        var index = equipmentBehavior.m_slotList.FindIndex(slot => slot.m_itemID == 0);
        equipmentBehavior.m_slotList[index].m_itemID = (GID) message.ItemID;

        equipmentBehavior.m_itemList.Add(itemObj);
        //equipmentBehavior.m_publicItemList.Add(new EquippedItemInfo() { m_itemID = (uint)itemObj.m_templateID });
        //creationEquipment.Add(equippedItemInfo);

        // Serialize item and broadcast equip action to other players.
        var item = new WizardEquippedItemInfo() {
            m_itemID = (uint) itemObj.m_templateID,
            m_pattern = (Bui5) template.m_numPatterns,
            m_baseColor = (Bui5) template.m_numPrimaryColors,
            m_trimColor = (Bui5) template.m_numSecondaryColors,
        };

        var serializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask((SerializerOptions.PropertyFlags) 1);
        var data = serializer.Serialize(item);
        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM() {
            GlobalID = coreObject.m_globalID,
            SerializedInfo = data
        }, false);

        ApplyItemEffectsToPlayer(coreObject, template);
    }

    private void UnEquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        var coreObject = GetActiveCoreObject();
        var playerCharacter = GetActiveCharacter();

        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizEquipmentBehavior>(coreObject,
            out var equipmentBehavior)) {
            return;
        }
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(coreObject,
                out var inventoryBehavior)) {
            return;
        }

        if (!ItemInInventory(message.ItemID, coreObject)) {
            // @TODO: Respond to client appropriately.
            Logger.Debug($"Player does not have the item in their inventory!");
            return;
        }

        // Get item object and its template.
        var itemObj = inventoryBehavior.m_itemList.First(item => item.m_globalID == message.ItemID);
        var templateId = itemObj.m_templateID;
        var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(templateId);

        // Confirm to the player that we've unequipped their item server side.
        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
            ItemID = message.ItemID,
            SlotName = message.SlotName,
            IsEquip = message.IsEquip
        });

        // Get slot index of item to unequip and number of total equipped items.
        var slot = equipmentBehavior.m_slotList.FindIndex(slot => slot.m_itemID == message.ItemID);
        var currentEquippedItem = equipmentBehavior.m_slotList[slot].m_itemID;

        if (currentEquippedItem == 0) {
            Logger.Debug("Player somehow has an item with GID: 0!");
            return;
        }

        // Remove item from equipment behavior lists.
        equipmentBehavior.m_slotList = RemoveSlotFromEquipmentSlotList(slot, equipmentBehavior.m_slotList);
        equipmentBehavior.m_itemList.RemoveAll(item => item.m_globalID == currentEquippedItem);
        //equipmentBehavior.m_publicItemList.RemoveAll(item => item.m_itemID == itemObj.m_templateID);
        //creationEquipment.RemoveAll(item => item.m_itemID == itemObj.m_templateID);

        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM() {
            GlobalID = coreObject.m_globalID,
            IndexToRemove = (byte) slot
        }, false);

        RemoveItemEffectsFromPlayer(coreObject, template);
    }

    private bool ItemInInventory(ulong itemId, CoreObject coreObject) {
        if (!CoreObjectFactory.FindBehaviorInstance<ClientWizInventoryBehavior>(coreObject,
                out var inventoryBehavior)) {
            return false;
        }

        var invItemList = inventoryBehavior.m_itemList.Any(item => item.m_globalID == itemId);
        return invItemList;
    }

    private List<EquippedSlotInfo> RemoveSlotFromEquipmentSlotList(int slot, List<EquippedSlotInfo> slotList) {
        // Zero-out item from slot list and move all items down to fill "empty" zero slots, should they exist.
        var numEquippedItemsInSlots = slotList.Count(slot => slot.m_itemID != 0);
        slotList[slot].m_itemID = (GID) 0;

        if (slot < numEquippedItemsInSlots - 1) {
            for (int i = slot; i < numEquippedItemsInSlots; i++) {
                if (slotList[i].m_itemID != 0) {
                    slotList[i - 1].m_itemID = slotList[i].m_itemID;
                    slotList[i].m_itemID = (GID) 0;
                }
            }
        }
        return slotList;
    }

    private void ApplyItemEffectsToPlayer(CoreObject coreObject, WizItemTemplate template) {
        var effectSerializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                              | SerializerOptions.PropertyFlags.Transmit
                              | SerializerOptions.PropertyFlags.AuthorityTransmit);

        foreach (GameEffectInfo it in template.m_equipEffects) {
            int internalID = _effectInternalIDCounter++;

            if (it.m_effectName == "ProvideSpell") {
                var spellEffect = (ProvideSpellEffectInfo) it;
                var spellEffectObj = new ProvideSpellEffect() {
                    m_spellName = spellEffect.m_spellName,
                    m_numSpells = spellEffect.m_numSpells,
                    m_vFX = spellEffect.m_vFX,
                    m_vFXOverride = spellEffect.m_vFXOverride,
                    m_sound = spellEffect.m_sound,
                    m_effectNameID = StringHash.Compute(spellEffect.m_effectName),
                    m_internalID = internalID,
                    m_itemSlotID = StringHash.Compute(template.m_adjectiveList[1].ToString())
                };

                var spellData = effectSerializer.Serialize(spellEffectObj);
                SendToSocket(new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
                    GameObjectID = coreObject.m_globalID,
                    EffectData = spellData
                });

                _gameEffects.Add(internalID, spellEffect);
                continue;
            }

            if (it.m_effectName == "StartingPips") {
                var pipEffect = (StartingPipEffectInfo) it;
                var pipEffectObj = new StartingPipEffect() {
                    m_pipsGiven = pipEffect.m_pipsGiven,
                    m_powerPipsGiven = pipEffect.m_powerPipsGiven,
                    m_effectNameID = StringHash.Compute(pipEffect.m_effectName),
                    m_internalID = internalID,
                    m_itemSlotID = StringHash.Compute(template.m_adjectiveList[1].ToString())
                };

                var pipData = effectSerializer.Serialize(pipEffectObj);
                SendToSocket(new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
                    GameObjectID = coreObject.m_globalID,
                    EffectData = pipData
                });

                _gameEffects.Add(internalID, pipEffect);
                continue;
            }

            if (it.m_effectName == "SpeedBuff") {
                var speedEffect = (SpeedEffectInfo) it;
                var speedEffectObj = new SpeedEffect() {
                    m_speedMultiplier = speedEffect.m_speedMultiplier,
                    m_effectNameID = StringHash.Compute(speedEffect.m_effectName),
                    m_internalID = internalID,
                    m_itemSlotID = StringHash.Compute(template.m_adjectiveList[1].ToString())
                };

                var speedData = effectSerializer.Serialize(speedEffectObj);
                ZoneBroadcast(new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
                    GameObjectID = coreObject.m_globalID,
                    EffectData = speedData
                }, false);

                _gameEffects.Add(internalID, speedEffect);
                continue;
            }

            var statEffect = (StatisticEffectInfo) it;
            var wSE = new WizStatisticEffect() {
                m_lookupIndex = statEffect.m_lookupIndex,
                m_effectNameID = StringHash.Compute(statEffect.m_effectName),
                m_internalID = internalID,
                m_itemSlotID = StringHash.Compute(template.m_adjectiveList[1].ToString())
            };

            // Read it and weep.
            var effectName = statEffect.m_effectName.ToString();
            switch (effectName) {
                case var _ when effectName.Contains("MaxMana"):         wSE.m_manaBonus = statEffect.m_lookupIndex + 98;                            break;
                case var _ when effectName.Contains("MaxHealth"):       wSE.m_hitPointBonus = statEffect.m_lookupIndex + 1;                         break;
                case var _ when effectName.Contains("MaxEnergy"):       wSE.m_energyBonus = statEffect.m_lookupIndex + 1;                           break;
                case var _ when effectName.Contains("FlatReduceDamage"):wSE.m_damageReduceFlat = statEffect.m_lookupIndex + 1;                      break;
                case var _ when effectName.Contains("FlatDamage"):      wSE.m_damageBonusFlat = statEffect.m_lookupIndex + 1;                       break;
                case var _ when effectName.Contains("CriticalHit"):     wSE.m_criticalHitRating = statEffect.m_lookupIndex + 1;                     break;
                case var _ when effectName.Contains("Block"):           wSE.m_blockRating = statEffect.m_lookupIndex + 1;                           break;
                case var _ when effectName.Contains("PipConversion"):   wSE.m_pipConversionRating = statEffect.m_lookupIndex + 1;                   break;
                case var _ when effectName.Contains("ShadowPipRating"): wSE.m_shadowPipRating = statEffect.m_lookupIndex + 1;                       break;
                case var _ when effectName.Contains("PowerPip"):        wSE.m_powerPipBonusPercent = (statEffect.m_lookupIndex - 99) / 100f;        break;
                case var _ when effectName.Contains("ReduceDamage"):    wSE.m_damageReducePercent = (statEffect.m_lookupIndex - 99) / 100f;         break;
                case var _ when effectName.Contains("Damage"):          wSE.m_damageBonusPercent = (statEffect.m_lookupIndex - 99) / 100f;          break;
                case var _ when effectName.Contains("Accuracy"):        wSE.m_accuracyBonusPercent = (statEffect.m_lookupIndex - 99) / 100f;        break;
                case var _ when effectName.Contains("ArmorPiercing"):   wSE.m_armorPiercingBonusPercent = (statEffect.m_lookupIndex - 99) / 100f;   break;
                case var _ when effectName.Contains("FishingLuck"):     wSE.m_fishingLuckBonusPercent = (statEffect.m_lookupIndex - 99) / 100f;     break;
                case var _ when effectName.Contains("IncHealing"):      wSE.m_healIncBonusPercent = (statEffect.m_lookupIndex - 99) / 100f;         break;
                case var _ when effectName.Contains("LifeHealing"):     wSE.m_healBonusPercent = (statEffect.m_lookupIndex - 99) / 100f;            break;
                case var _ when effectName.Contains("StunResistance"):  wSE.m_stunResistancePercent = (statEffect.m_lookupIndex - 99) / 100f;       break;
                case var _ when effectName.Contains("XPPercent"):       wSE.m_expPercent = (statEffect.m_lookupIndex - 99) / 100f;                  break;
                case var _ when effectName.Contains("GoldPercent"):     wSE.m_goldPercent = (statEffect.m_lookupIndex - 99) / 100f;                 break;
                case "CanonicalStormMastery":   wSE.m_stormMastery = 1;     break;
                case "CanonicalFireMastery":    wSE.m_fireMastery = 1;      break;
                case "CanonicalIceMastery":     wSE.m_iceMastery = 1;       break;
                case "CanonicalLifeMastery":    wSE.m_lifeMastery = 1;      break;
                case "CanonicalDeathMastery":   wSE.m_deathMastery = 1;     break;
                case "CanonicalMythMastery":    wSE.m_mythMastery = 1;      break;
                case "CanonicalBalanceMastery": wSE.m_balanceMastery = 1;   break;
                default: break;
            }

            var effectData = effectSerializer.Serialize(wSE);
            SendToSocket(new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
                GameObjectID = coreObject.m_globalID,
                EffectData = effectData
            });

            _gameEffects.Add(internalID, statEffect);
        }
    }

    private void RemoveItemEffectsFromPlayer(CoreObject coreObject, WizItemTemplate template) {
        int internalID = 0;

        foreach (GameEffectInfo it in template.m_equipEffects) {

            if (it.m_effectName == "ProvideSpell") {
                var spellEffect = (ProvideSpellEffectInfo) it;

                foreach (var effect in _gameEffects) {
                    if (effect.Value.m_effectName != spellEffect.m_effectName) {
                        continue;
                    }

                    if (((ProvideSpellEffectInfo) effect.Value).m_spellName != spellEffect.m_spellName) {
                        continue;
                    }

                    if (((ProvideSpellEffectInfo) effect.Value).m_numSpells != spellEffect.m_numSpells) {
                        continue;
                    }

                    internalID = effect.Key;
                }

                SendToSocket(new GAME_5_PROTOCOL.MSG_REMOVEEFFECT() {
                    GameObjectID = coreObject.m_globalID,
                    EffectNameID = StringHash.Compute(it.m_effectName),
                    InternalID = internalID,
                });

                _gameEffects.Remove(internalID);
                continue;
            }

            if (it.m_effectName == "StartingPips") {
                var pipEffect = (StartingPipEffectInfo) it;

                foreach (var effect in _gameEffects) {
                    if (effect.Value.m_effectName != pipEffect.m_effectName) {
                        continue;
                    }

                    if (((StartingPipEffectInfo) effect.Value).m_pipsGiven != pipEffect.m_pipsGiven) {
                        continue;
                    }

                    if (((StartingPipEffectInfo) effect.Value).m_powerPipsGiven != pipEffect.m_powerPipsGiven) {
                        continue;
                    }

                    internalID = effect.Key;
                }

                SendToSocket(new GAME_5_PROTOCOL.MSG_REMOVEEFFECT() {
                    GameObjectID = coreObject.m_globalID,
                    EffectNameID = StringHash.Compute(it.m_effectName),
                    InternalID = internalID,
                });

                _gameEffects.Remove(internalID);
                return;
            }   

            if (it.m_effectName == "SpeedBuff") {
                var speedEffect = (SpeedEffectInfo) it;

                foreach (var effect in _gameEffects) {
                    if (effect.Value.m_effectName != speedEffect.m_effectName) {
                        continue;
                    }

                    if (((SpeedEffectInfo) effect.Value).m_speedMultiplier != speedEffect.m_speedMultiplier) {
                        continue;
                    }

                    internalID = effect.Key;
                }

                ZoneBroadcast(new GAME_5_PROTOCOL.MSG_REMOVEEFFECT() {
                    GameObjectID = coreObject.m_globalID,
                    EffectNameID = StringHash.Compute(it.m_effectName),
                    InternalID = internalID,
                }, false);

                _gameEffects.Remove(internalID);
                return;
            }

            var statEffect = (StatisticEffectInfo) it;

            foreach (var effect in _gameEffects) {
                if (effect.Value.m_effectName != statEffect.m_effectName) {
                    continue;
                }

                if (((StatisticEffectInfo) effect.Value).m_lookupIndex != statEffect.m_lookupIndex) {
                    continue;
                }

                internalID = effect.Key;
            }

            SendToSocket(new GAME_5_PROTOCOL.MSG_REMOVEEFFECT() {
                GameObjectID = coreObject.m_globalID,
                EffectNameID = StringHash.Compute(it.m_effectName),
                InternalID = internalID,
            });

            _gameEffects.Remove(internalID);
        }
    }
}
