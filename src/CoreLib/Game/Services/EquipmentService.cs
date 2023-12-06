using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using System;
using System.Collections.Generic;
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
        try {
            if (message.IsEquip == 1) {
                EquipItem(message);
            }
            else {
                UnEquipItem(message);
            }
        }
        catch (Exception ex) {
            Logger.Error("Error while equipping item: {0} {1}", Logger.Args(ex.Message, ex.StackTrace));
        }
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        // Send the player's equipment to the client.
        var wizard = GetActiveCharacter();
        var equipment = wizard.EquipmentGetAllItems();
        foreach (var pieceTemplate in equipment) {
            ApplyItemEffectsToPlayer(pieceTemplate);
        }
    }

    private void EquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        var playerCharacter = GetActiveCharacter();
        var itemId = message.ItemID;

        var item = playerCharacter.InventoryGetItem(itemId);
        if (item is null) {
            // Todo: Log an infraction here.
            Logger.Debug($"Player does not have the item in their inventory!");
            return;
        }

        // Todo: Check if player meets requirements to equip item. If not, log an infraction.

        // Check to see if the player already has this item equipped. If they do, broadcast the removal of it.
        if (playerCharacter.EquipmentHasEquippedItem(itemId)) {
            var slot = playerCharacter.EquipmentGetItemSlotIndex(itemId);
            SendPublicUnequipItem((byte) slot, itemId);
        }

        // This method will remove any items that are already equipped in the target slot.
        var template = playerCharacter.EquipmentEquipItem(itemId);
        if (template is null) {
            Logger.Debug($"EquipmentEquipItem returned null template!");
            return;
        }

        // Confirm to the player that we've equipped their item server side.
        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
            ItemID = message.ItemID,
            SlotName = message.SlotName,
            IsEquip = message.IsEquip
        });

        SendPublicEquipItem(item);
        ApplyItemEffectsToPlayer(template);
    }

    private void UnEquipItem(GAME_5_PROTOCOL.MSG_EQUIPITEM message) {
        var playerCharacter = GetActiveCharacter();
        var itemId = message.ItemID;

        // Check to see if the player has this item equipped. If they don't, log an infraction.
        var item = playerCharacter.InventoryGetItem(itemId);
        if (item is null) {
            // Todo: Log an infraction here.
            Logger.Debug($"Player does not have the item in their inventory!");
            return;
        }

        // Get the slot index of the item we're unequipping.
        var slot = playerCharacter.EquipmentGetItemSlotIndex(itemId);
        var template = playerCharacter.EquipmentUnequipItem(itemId);

        SendPublicUnequipItem((byte) slot, itemId);
        RemoveItemEffectsFromPlayer(template);
    }

    private void SendPublicEquipItem(WizClientObjectItem item) {
        // Serialize item and broadcast equip action to other players.
        var publicItem = new WizardEquippedItemInfo() {
            m_itemID = (uint) item.m_templateID,
            m_pattern = (Bui5) item.m_pattern,
            m_baseColor = (Bui5) item.m_primaryColor,
            m_trimColor = (Bui5) item.m_secondaryColor,
        };

        var serializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask((SerializerOptions.PropertyFlags) 1);
        var data = serializer.Serialize(publicItem);
        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM() {
            GlobalID = GetActiveCoreObject().m_globalID,
            SerializedInfo = data
        }, false);
    }

    private void SendPublicUnequipItem(byte slot, ulong itemId) {
        // This one goes to the client.
        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM() {
            ItemID = itemId,
            SlotName = "",
            IsEquip = 0
        });

        // This one goes to the zone.
        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICUNEQUIPITEM() {
            GlobalID = GetActiveCoreObject().m_globalID,
            IndexToRemove = slot
        }, false);
    }

    // Todo: Move the methods below to the Wizard class.

    private void ApplyItemEffectsToPlayer(WizItemTemplate template) {
        var charObjId = GetActiveCoreObject().m_globalID;
        var effectSerializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                              | SerializerOptions.PropertyFlags.Transmit
                              | SerializerOptions.PropertyFlags.AuthorityTransmit);

        foreach (GameEffectInfo it in template.m_equipEffects) {
            int internalID = _effectInternalIDCounter++;

            switch (it.m_effectName) {
                case "ProvideSpell":
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
                        GameObjectID = charObjId,
                        EffectData = spellData
                    });

                    _gameEffects.Add(internalID, spellEffect);
                    continue;

                case "StartingPips":
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
                        GameObjectID = charObjId,
                        EffectData = pipData
                    });

                    _gameEffects.Add(internalID, pipEffect);
                    continue;

                case "SpeedBuff":
                    var speedEffect = (SpeedEffectInfo) it;
                    var speedEffectObj = new SpeedEffect() {
                        m_speedMultiplier = speedEffect.m_speedMultiplier,
                        m_effectNameID = StringHash.Compute(speedEffect.m_effectName),
                        m_internalID = internalID,
                        m_itemSlotID = StringHash.Compute(template.m_adjectiveList[1].ToString())
                    };

                    var speedData = effectSerializer.Serialize(speedEffectObj);
                    ZoneBroadcast(new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
                        GameObjectID = charObjId,
                        EffectData = speedData
                    }, false);

                    _gameEffects.Add(internalID, speedEffect);
                    continue;

                default:
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
                        case var _ when effectName.Contains("MaxMana"):         wSE.m_manaBonus                 = statEffect.m_lookupIndex + 98; break;
                        case var _ when effectName.Contains("MaxHealth"):       wSE.m_hitPointBonus             = statEffect.m_lookupIndex + 1;  break;
                        case var _ when effectName.Contains("MaxEnergy"):       wSE.m_energyBonus               = statEffect.m_lookupIndex + 1;  break;
                        case var _ when effectName.Contains("FlatReduceDamage"):wSE.m_damageReduceFlat          = statEffect.m_lookupIndex + 1;  break;
                        case var _ when effectName.Contains("FlatDamage"):      wSE.m_damageBonusFlat           = statEffect.m_lookupIndex + 1;  break;
                        case var _ when effectName.Contains("CriticalHit"):     wSE.m_criticalHitRating         = statEffect.m_lookupIndex + 1;  break;
                        case var _ when effectName.Contains("Block"):           wSE.m_blockRating               = statEffect.m_lookupIndex + 1;  break;
                        case var _ when effectName.Contains("PipConversion"):   wSE.m_pipConversionRating       = statEffect.m_lookupIndex + 1;  break;
                        case var _ when effectName.Contains("ShadowPipRating"): wSE.m_shadowPipRating           = statEffect.m_lookupIndex + 1;  break;
                        case var _ when effectName.Contains("PowerPip"):        wSE.m_powerPipBonusPercent      = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case var _ when effectName.Contains("ReduceDamage"):    wSE.m_damageReducePercent       = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case var _ when effectName.Contains("Damage"):          wSE.m_damageBonusPercent        = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case var _ when effectName.Contains("Accuracy"):        wSE.m_accuracyBonusPercent      = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case var _ when effectName.Contains("ArmorPiercing"):   wSE.m_armorPiercingBonusPercent = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case var _ when effectName.Contains("FishingLuck"):     wSE.m_fishingLuckBonusPercent   = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case var _ when effectName.Contains("IncHealing"):      wSE.m_healIncBonusPercent       = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case var _ when effectName.Contains("LifeHealing"):     wSE.m_healBonusPercent          = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case var _ when effectName.Contains("StunResistance"):  wSE.m_stunResistancePercent     = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case var _ when effectName.Contains("XPPercent"):       wSE.m_expPercent                = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case var _ when effectName.Contains("GoldPercent"):     wSE.m_goldPercent               = (statEffect.m_lookupIndex - 99) / 100f; break;
                        case "CanonicalStormMastery":   wSE.m_stormMastery = 1;   break;
                        case "CanonicalFireMastery":    wSE.m_fireMastery = 1;    break;
                        case "CanonicalIceMastery":     wSE.m_iceMastery = 1;     break;
                        case "CanonicalLifeMastery":    wSE.m_lifeMastery = 1;    break;
                        case "CanonicalDeathMastery":   wSE.m_deathMastery = 1;   break;
                        case "CanonicalMythMastery":    wSE.m_mythMastery = 1;    break;
                        case "CanonicalBalanceMastery": wSE.m_balanceMastery = 1; break;
                        default: break;
                    }

                    var effectData = effectSerializer.Serialize(wSE);
                    SendToSocket(new GAME_5_PROTOCOL.MSG_ADDEFFECT() {
                        GameObjectID = charObjId,
                        EffectData = effectData
                    });

                    _gameEffects.Add(internalID, statEffect);
                    continue;
            }
        }
    }

    private void RemoveItemEffectsFromPlayer(WizItemTemplate template) {
        int internalID = 0;
        var charObjId = GetActiveCoreObject().m_globalID;

        foreach (GameEffectInfo it in template.m_equipEffects) {

            switch (it.m_effectName) {
                case "ProvideSpell":
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
                        GameObjectID = charObjId,
                        EffectNameID = StringHash.Compute(it.m_effectName),
                        InternalID = internalID,
                    });

                    _gameEffects.Remove(internalID);
                    continue;

                case "StartingPips":
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
                        GameObjectID = charObjId,
                        EffectNameID = StringHash.Compute(it.m_effectName),
                        InternalID = internalID,
                    });

                    _gameEffects.Remove(internalID);
                    continue;

                case "SpeedBuff":
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
                        GameObjectID = charObjId,
                        EffectNameID = StringHash.Compute(it.m_effectName),
                        InternalID = internalID,
                    }, false);

                    _gameEffects.Remove(internalID);
                    continue;

                default:
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
                        GameObjectID = charObjId,
                        EffectNameID = StringHash.Compute(it.m_effectName),
                        InternalID = internalID,
                    });

                    _gameEffects.Remove(internalID);
                    continue;
            }
        }
    }
}
