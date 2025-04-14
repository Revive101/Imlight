/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * CANTRIP SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player cantrip (minor magic) interactions, including spell casting, 
 * energy management, and special effect processing.
 * 
 * USAGE EXAMPLE:
 * Internal service handling various cantrip-related messages and actions 
 * within the game server's session management system.
 * 
 * NOTE:
 * - Handles multiple types of cantrip effects (emote, teleport, invisibility)
 * - Implements energy cost and cooldown management
 * 
 * TODO:
 * - Review and complete unhandled ritual cantrip scenarios
 * - Investigate string hash computations
 * - Refine energy shop and energy refill logic
 * 
 * Created by: Jeff
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Cantrips;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

public class CantripService(SessionActor sessionActor) : MessageService(sessionActor), IWithTimers {

    private const int CANTRIP_CAST_TIME = 3;

    public ITimerScheduler Timers { get; set; }

    private readonly TimeSpan _cantripCastTimeSpan = TimeSpan.FromSeconds(CANTRIP_CAST_TIME);
    private readonly CoreObjectSerializer _effectSerializer = new(
        behaviors: SerializerFlags.None
    );

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new CantripService(parentActor));

    [MessageHandler(typeof(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSSPELLCAST))]
    private void ReceiveCantripSpellCast(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSSPELLCAST message) {
        var wizard = GetActiveWizard();
        CantripsSpellTemplate cantrip = CantripFactory.CreateCantripTemplateFromId(message.SpellTemplateID);

        // Rituals require a target to be selected first, so don't use energy here
        if (cantrip.m_cantripsSpellEffect != CantripsSpellEffect.CSE_Ritual) {
            bool hasEnergy = CastCantrip(message.SpellTemplateID);

            if (!hasEnergy) {
                return;
            }
        }

        var castEffect = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTEFFECT {
            GameObjectID = wizard.GameObject.m_globalID,
            SpellTemplateID = (int) message.SpellTemplateID
        };

        switch (cantrip.m_cantripsSpellEffect) {
            case CantripsSpellEffect.CSE_Emote:
                CastEmoteCantrip(cantrip, ref castEffect);
                break;
            case CantripsSpellEffect.CSE_PlayEffect:
                CastPlayEffectCantrip(cantrip, ref castEffect);
                break;
            case CantripsSpellEffect.CSE_Teleport:
                CastTeleportCantrip(cantrip);
                break;
            case CantripsSpellEffect.CSE_Ritual:
                CastRitualCantrip((int) message.SpellTemplateID);
                return; // User needs to select a target first
            case CantripsSpellEffect.CSE_Invisibility:
                CastInvisCantrip(cantrip);
                break;
        }

        ZoneBroadcast(castEffect, isSelfless: false);
    }

    [MessageHandler(typeof(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTRITUAL))]
    private void ReceiveCastRitual(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTRITUAL message) {
        bool hasEnergy = CastCantrip((uint) message.SpellTemplateID);

        if (!hasEnergy) {
            return;
        }

        SendToSocket(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        TellOtherServices(message);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ADDEFFECT))]
    private void ReceiveAddEffect(GAME_5_PROTOCOL.MSG_ADDEFFECT message) {
        SendToSocket(message);
    }

    [MessageHandler(typeof(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANCELINVISIBLITY))]
    private void ReceiveCancelInvisibility(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANCELINVISIBLITY message) {
        var wizard = GetActiveWizard();
        var effect = wizard.GameEffects.Find(e => e.m_effectNameID == StringHash.Compute("CantripsMajorInvisibilityEffect"));
        if (effect == null) {
            return;
        }

        wizard.GameEffects.Remove(effect);
        var removeEffect = new GAME_5_PROTOCOL.MSG_REMOVEEFFECT {
            GameObjectID = wizard.GameObject.m_globalID,
            EffectNameID = effect.m_effectNameID,
            InternalID = effect.m_internalID
        };

        SendToSocket(removeEffect);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_ENERGYSHOPOPEN))]
    private void ReceiveEnergyShopOpen(WIZARD_12_PROTOCOL.MSG_ENERGYSHOPOPEN message) {
        // This would usually open a menu to spend crowns to refill energy
        // We aren't going to do that, but it will refill your energy if you are QA or above
        var wizard = GetActiveWizard();
        MaybeBackflip(wizard);
        if (wizard.Account.AuthLevel >= AuthLevel.QualityAssurance) {
            // The client has a max mana increase effect applied, so sending it here would double the mana client side.
            var magicSchool = wizard.MagicSchoolBehavior.MagicSchool;
            var level = wizard.MagicSchoolBehavior.Level;
            var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
            var normMaxEnergy = baseStats.m_petEnergy;

            wizard.UpdateEnergy(normMaxEnergy);

            // Inform the client of the change.
            var networkMessage = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
                GlobalID = wizard.GameObject.m_globalID,
                Energy = normMaxEnergy,
                MaxEnergy = normMaxEnergy,
                TickTime = (int) wizard.PetOwnerBehavior.LastEnergyTickEpoch
            };
            SendToSocket(networkMessage);
            InformGameClient("Refilled energy.");
        }
        else {
            InformGameClient("You cannot do that.");

            return;
        }
    }

    private bool CastCantrip(uint spellTemplateID) {
        var wizard = GetActiveWizard();
        CantripsSpellTemplate cantrip = CantripFactory.CreateCantripTemplateFromId(spellTemplateID);

        bool hasEnergy = UseEnergy(wizard, cantrip.m_energyCost);
        byte outOfEnergy = 0;
        if (!hasEnergy) {
            cantrip.m_energyCost = 0;
            outOfEnergy = 1;
        }

        var cantripResponse = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSRESPONSE {
            EnergyUsed = (uint) cantrip.m_energyCost,
            CooldownSeconds = (uint) cantrip.m_cooldownSeconds,
            OutOfEnergy = outOfEnergy
        };
        SendToSocket(cantripResponse);

        if (!hasEnergy) {
            return false;
        }
        return true;
    }

    private bool UseEnergy(Wizard wizard, int energyCost) {
        var newEnergy = wizard.PetOwnerBehavior.Energy - energyCost;
        if (newEnergy < 0) {
            return false;
        }
        wizard.UpdateEnergy(newEnergy);

        // The client has a max energy increase effect applied, so sending it here would double the energy client side.
        var magicSchool = wizard.MagicSchoolBehavior.MagicSchool;
        var level = wizard.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxEnergy = baseStats.m_petEnergy;

        var networkMessage = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
            GlobalID = wizard.GameObject.m_globalID,
            Energy = newEnergy,
            MaxEnergy = normMaxEnergy,
            TickTime = (int) wizard.PetOwnerBehavior.LastEnergyTickEpoch
        };

        SendToSocket(networkMessage);

        return true;
    }

    private void CastEmoteCantrip(CantripsSpellTemplate cantrip, ref CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTEFFECT castEffect) {
        castEffect.AnimationName = cantrip.m_animationNames[0];
    }

    private void CastPlayEffectCantrip(CantripsSpellTemplate cantrip, ref CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTEFFECT castEffect) {
        // note that the only cantrip from this section that needs rng is the dice roll one
        // all the other cantrips in this section only have 1 animation
        var rand = new Random();
        int num = rand.Next(cantrip.m_animationKFMs.Count);
        castEffect.AnimationKFM = cantrip.m_animationKFMs[num];
        castEffect.AnimationName = cantrip.m_animationNames[num];
    }

    private void CastTeleportCantrip(CantripsSpellTemplate cantrip) {
        var tpmsg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationLocation = "Start",
            DestinationZone = cantrip.m_effectParameter,
            SendToClient = true,
            OwnerCharId = GetActiveWizard().CharId
        };
        Timers.StartSingleTimer("zonetransfer", tpmsg, _cantripCastTimeSpan);
    }

    private void CastRitualCantrip(int spellTemplateID) {
        var castRitualMsg = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTRITUAL {
            SpellTemplateID = spellTemplateID,
            Phase = 0
        };
        SendToSocket(castRitualMsg);
    }

    private void CastInvisCantrip(CantripsSpellTemplate cantrip) {
        var wizard = GetActiveWizard();
        GameEffectBase effect;
        if (cantrip.m_name == "CantripsMinorInvisibility") {
            effect = new NamedEffect {
                m_effectNameID = 1662424096, // if someone can find the string for this stringhash i will cum rapidly and forcefully, breaking the sound barrier in the process
                m_internalID = wizard.GameEffects.Count,
                m_endTime = (uint) DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds()
            };
        }
        else if (cantrip.m_name == "CantripsMajorInvisibility") {
            effect = new CantripsMajorInvisibilityEffect {
                m_effectNameID = StringHash.Compute("CantripsMajorInvisibilityEffect"),
                m_internalID = wizard.GameEffects.Count
            };
        }
        else {
            return;
        }

        // Attempt to serialize the effect.
        var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        if (!_effectSerializer.Serialize(effect, flags, out var serializedEffect)) {
            return;
        }

        var addEffect = new GAME_5_PROTOCOL.MSG_ADDEFFECT {
            GameObjectID = wizard.GameObject.m_globalID,
            EffectData = serializedEffect
        };

        Timers.StartSingleTimer("inviseffect", addEffect, _cantripCastTimeSpan);
    }

    private void MaybeBackflip(Wizard wizard) {
        // 1 in 20 chance the player does a backflip instead :D
        var rand = new Random();
        int num = rand.Next(20);
        if (num != 0) {
            return;
        }

        uint backflipID = 1521398842;
        CantripsSpellTemplate cantrip = CantripFactory.CreateCantripTemplateFromId(backflipID);
        var castEffect = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTEFFECT {
            GameObjectID = wizard.GameObject.m_globalID,
            SpellTemplateID = (int) backflipID
        };

        CastEmoteCantrip(cantrip, ref castEffect);
        SendToSocket(castEffect);
        InformGameClient("backflip!");
    }

}