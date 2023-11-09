using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat {
    public class DuelActor : ReceiveProtocolDispatcher {
        private Duel _duel;
        private Dictionary<IActorRef, CoreObject> _participants;
        private ulong _sigilId; // Same as the sigil ID.
        private Vector3 _sigilLocation;
        private Vector3 _sigilOrientation;

        public DuelActor() {
            _duel = new Duel();
            _participants = new Dictionary<IActorRef, CoreObject>();
        }

        public static Props Props() => Akka.Actor.Props.Create(() => new DuelActor());

        [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_STARTDUEL))]
        protected void ReceiveStartDuel(COMBAT_106_PROTOCOL.MSG_STARTDUEL message) {
            this._participants = message.Participants;
            this._sigilId = message.SigilId;
            this._sigilLocation = message.SigilLocation;
            this._sigilOrientation = message.SigilOrientation;

            // "Aggro" means to move the participants to their respective sub-sigil location.
            SendAggroToParticipants(_participants);

            _duel = CreateDuelWithDefaults(message.SigilId);

            // Return the duel details to the sender. As it stands, this is returned to
            // the WizardZoneCombatSigil that created this duel actor.
            var rsp = new COMBAT_106_PROTOCOL.MSG_DUELDETAILS {
                DuelActor = Self,
                Duel = _duel
            };
            Sender.Tell(rsp);

            // todo: this needs to happen slightly later with a grace period. Other players may be
            // standing in the sigil zone and should be a part of this combat, but will not be in
            // the participants list initially.
            SendCombatStartToParticipants(_participants);

            // testing stuff below
            var serializer = new ObjectSerializer()
                .OnBehaviors(SerializerOptions.Behaviors.None)
                .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                              | SerializerOptions.PropertyFlags.Transmit
                              | SerializerOptions.PropertyFlags.AuthorityTransmit);
            var data = serializer.Serialize(new UpFirstData() {
                m_resultType = 1884669703,
                m_roundNum = 96,
                m_upFirst = 320,
            });
            var combatPhaseMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATPHASE {
                DuelID = _sigilId,
                NewPhase = 1,
                PlayerID = 0,
                Data = data
            };
            var combatUpFirstMsp = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATUPFIRST {
                DuelID = _sigilId,
                UpFirst = 1,
                RoundNum = 1,
                PlayerID = 0,
                FirstTeamToAct = 0,
            };
            var combatPhaseMsg2 = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATPHASE {
                DuelID = _sigilId,
                NewPhase = 2,
                PlayerID = 0,
            };
            var showCombatUiMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_SHOWCOMBATUI {
                DuelID = _sigilId,
            };
            var planningMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_SETPLANNINGPHASETIMER {
                DuelID = _sigilId,
                Time = 30
            };

            foreach (var participant in message.Participants) {
                participant.Key.Tell(combatPhaseMsg);
                participant.Key.Tell(combatUpFirstMsp);
                participant.Key.Tell(combatPhaseMsg2);
                participant.Key.Tell(showCombatUiMsg);
                participant.Key.Tell(planningMsg);
            }
        }

        private Duel CreateDuelWithDefaults(ulong sigilId) {
            // todo: source these from config
            var duel = new Duel() {
                m_flatParticipantList = new(),
                m_duelID = sigilId,
                m_planningTimer = 30,
                m_firstTeamToAct = 0,
                m_executionPhaseTimer = 3.4078238f, // From packet capture. No idea what it means.
                m_duelPhase = Duel.kDuelPhase.kPhase_PrePlanning,
            };

            return duel;
        }

        private void SendAggroToParticipants(Dictionary<IActorRef, CoreObject> participants) {
            foreach (var participant in participants) {
                SendAggroToParticipant(participant.Key, participant.Value);
            }
        }

        private void SendAggroToParticipant(IActorRef actorRef, CoreObject coreObject) {
            var aggroMsg = new WIZARD_12_PROTOCOL.MSG_AGGRO {
                GlobalID = coreObject.m_globalID,
                LocX = _sigilLocation.X,
                LocY = _sigilLocation.Y,
                LocZ = _sigilLocation.Z,
                SigilGID = _sigilId
            };

            actorRef.Tell(aggroMsg);
        }

        private void SendCombatStartToParticipants(Dictionary<IActorRef, CoreObject> participants) {
            foreach (var participant in participants) {
                // A player will always have a template ID of 1.
                CombatParticipant combatParticipant;
                if (participant.Value.m_templateID == 1) {
                    combatParticipant = CreateCombatParticipantFromPlayer(participant.Key, participant.Value);
                }
                else {
                    combatParticipant = CreateCombatParticipantFromCreature(participant.Value);
                }

                // Serialize and send the combat participant to the participant.
                var serializer = new ObjectSerializer()
                .OnBehaviors(SerializerOptions.Behaviors.None)
                .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                              | SerializerOptions.PropertyFlags.Transmit
                              | SerializerOptions.PropertyFlags.AuthorityTransmit);
                var serializedCombatParticipant = serializer.Serialize(combatParticipant);

                // Send to client.
                var addMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATADD {
                    DuelID = _sigilId,
                    ParticipantData = serializedCombatParticipant
                };

                participant.Key.Tell(addMsg);
            }
        }

        private CombatParticipant CreateCombatParticipantFromPlayer(IActorRef playerActor, CoreObject playerObject) {
            var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var queryCharacterRsp = playerActor
                .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
                .Result
                .Character;

            var combatParticipant = new CombatParticipant {
                m_ownerID = playerObject.m_globalID,
                m_templateID = playerObject.m_templateID, // Captued 2199023255553 from live
                m_isPlayer = true,
                m_zoneID = 0,
                m_teamID = 0,
                m_primaryMagicSchoolID = 0,
                m_pipCount = new() { m_powerPips = 1, m_genericPips = 1 },
                m_pipRoundRates = new(),
                m_PipsSuspended = false,
                m_playerHealth = queryCharacterRsp.GameStats.m_currentHitpoints,
                m_maxPlayerHealth = queryCharacterRsp.GameStats.m_baseHitpoints,

                // todo: this causes client to fail deserialization
                //m_pGameStats = queryCharacterRsp.GameStats,
            };

            return combatParticipant;
        }

        private CombatParticipant CreateCombatParticipantFromCreature(CoreObject creatureObject) {
            var combatParticipant = new CombatParticipant {
                m_ownerID = creatureObject.m_globalID,
                m_templateID = creatureObject.m_templateID, // Captued 2199023290637 from live
                m_isPlayer = true,
                m_zoneID = 0,
                m_teamID = 1,
                m_primaryMagicSchoolID = 0,
                m_pipCount = new() { m_powerPips = 1, m_genericPips = 1 },
                m_pipRoundRates = new(),
                m_PipsSuspended = false,
            };

            return combatParticipant;

        }
    }
}
