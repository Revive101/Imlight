/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Cantrips;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

public class CantripService : MessageService, IWithTimers {
    private const int CANTRIP_CAST_TIME = 5;
    public CantripService(SessionActor sessionActor) : base(sessionActor) { }
    public ITimerScheduler Timers { get; set; }
    private readonly TimeSpan _zoneRemovalWaitTime = TimeSpan.FromSeconds(CANTRIP_CAST_TIME);

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new CantripService(parentActor));

    protected override void OnDispose() {

    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        // Send a pet energy tick message to the client.
        var wizard = GetActiveWizard();
        var petOwnerBehavior = wizard.PetOwnerBehavior;

        // The client has a max energy increase effect applied, so sending it here would double the energy client side.
        var magicSchool = wizard.MagicSchoolBehavior.MagicSchool;
        var level = wizard.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxEnergy = baseStats.m_petEnergy;

        var tickMsg = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
            GlobalID = wizard.CharId,
            Energy = petOwnerBehavior.Energy,
            MaxEnergy = normMaxEnergy,
            TickTime = 0
        };

        SendToSocket(tickMsg);
    }

    [MessageHandler(typeof(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSSPELLCAST))]
    private void ReceiveCantripSpellCast(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSSPELLCAST message) {
        var wizard = GetActiveWizard();
        CantripsSpellTemplate cantrip = CantripFactory.CreateCantripTemplateFromId(message.SpellTemplateID);
        
        // Rituals require a target to be selected first, so don't use energy here
        if (cantrip.m_cantripsSpellEffect != CantripsSpellTemplate.CantripsSpellEffect.CSE_Ritual) {
            bool hasEnergy = castCantrip(message.SpellTemplateID);

            if (!hasEnergy) {
                return;
            }
        }

        var castEffect = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTEFFECT {
            GameObjectID = wizard.GameObject.m_globalID,
            SpellTemplateID = (int) message.SpellTemplateID
        };

        switch (cantrip.m_cantripsSpellEffect) {
            case CantripsSpellTemplate.CantripsSpellEffect.CSE_Emote:
                castEffect.AnimationName = cantrip.m_animationNames[0]; break;
            case CantripsSpellTemplate.CantripsSpellEffect.CSE_PlayEffect:
                // note that the only cantrip from this section that needs rng is the dice roll one
                // all the other cantrips in this section only have 1 animation
                var rand = new Random();
                int num = rand.Next(cantrip.m_animationKFMs.Count);
                castEffect.AnimationKFM = cantrip.m_animationKFMs[num];
                castEffect.AnimationName = cantrip.m_animationNames[num];
                break;
            case CantripsSpellTemplate.CantripsSpellEffect.CSE_Teleport:
                var tpmsg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
                    DestinationLocation = "Start",
                    DestinationZone = cantrip.m_effectParameter,
                    SendToClient = true,
                };
                Timers.StartSingleTimer("zonetransfer", tpmsg, _zoneRemovalWaitTime);
                break;
            case CantripsSpellTemplate.CantripsSpellEffect.CSE_Ritual:
                var castRitualMsg = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTRITUAL {
                    SpellTemplateID = (int) message.SpellTemplateID,
                    Phase = 0
                };
                SendToSocket(castRitualMsg);
                return; // User needs to select a target first
        }
        ZoneBroadcast(castEffect);
    }

    [MessageHandler(typeof(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTRITUAL))]
    private void ReceiveCastRitual(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTRITUAL message) {
        bool hasEnergy = castCantrip((uint) message.SpellTemplateID);

        if (!hasEnergy) {
            return;
        }

        SendToSocket(message);
    }

    private bool castCantrip(uint spellTemplateID) {
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

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        TellOtherServices(message);
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
            TickTime = (int) wizard.PetOwnerBehavior.NextEnergyTickEpoch
        };

        SendToSocket(networkMessage);
        return true;
    }
}