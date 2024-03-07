/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Models.World;
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using System.Threading.Tasks;
using static Imlight.Common.Caches.TypeCache;
using static Imlight.Common.Caches.TypeCache.CombatParticipant;

namespace Imlight.CoreLib.Game.Combat;

internal enum SlotType {
    Monster,
    Player
}

public class DuelActorSubCircle {
    private const float AggroTimeInSeconds = 0.75f;

    internal string SlotName { get; set; }
    internal SlotType SlotType { get; set; }
    internal int SlotIndex { get; private set; }
    internal Vector3 WorldPosition { get; set; }
    internal float WorldRotation { get; set; }
    internal IActorRef ParticipantActor { get; private set; }
    internal CoreObject ParticipantObject { get; private set; }
    internal WizGameStats ParticipantGameStats { get; private set; }
    internal CombatParticipant CombatParticipant { get; private set; }
    public uint AvailableSpells {
        get {
            if (_combatHand is null) {
                return 0;
            }

            return (uint) _combatHand.AvailableSpells.Count;
        }
    }
    public uint TotalSpells {
        get {
            if (_combatHand is null) {
                return 0;
            }

            return (uint) _combatHand.Spells.Count;
        }
    }
    internal bool Occupied => ParticipantObject is not null;
    internal Team OccupiedTeam {
        get {
            if (ParticipantObject is null) {
                return Team.Player;
            }

            return ParticipantObject.m_templateID == 1 ? Team.Player : Team.Monster;
        }
    }
    internal bool IsAlive => ParticipantGameStats.m_currentHitpoints > 0;

    private readonly DuelActor _duelActor;
    private readonly float _radius;
    private readonly float _rotation;
    private readonly Color _color;
    private CombatHand _combatHand;

    // ctor
    internal DuelActorSubCircle(DuelActor duelActor, float radius, float rotation, Color color, int index) {
        _duelActor = duelActor;
        _radius = radius;
        _rotation = rotation;
        _color = color;
        SlotIndex = index;
    }

    internal async Task AssignParticipant(IActorRef actor, CoreObject participantObject) {
        ParticipantActor = actor;
        ParticipantObject = participantObject;
        var team = participantObject.m_templateID == 1 ? Team.Player : Team.Monster;

        // Set the CombatParticipant based on what team they are.
        if (team == Team.Player) {
            InitializePlayerSubCircle();
        }
        else {
            InitializeCreatureSubCircle();
        }

        // Inform the actor that they've been added to a duel.
        var msg = new COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL {
            DuelActor = _duelActor.ActorRef,
            SlotPosition = WorldPosition,
            SlotOrientation = WorldRotation
        };
        ParticipantActor.Tell(msg);

        await PlayEntranceAnimation(participantObject);
    }

    internal Hand DrawHand() {
        var newHand = _combatHand.GetHand();
        CombatParticipant.m_pHand = newHand;

        return newHand;
    }

    internal Spell DiscardCard(byte index) {
        var spell = _combatHand.LastGivenHand[index];
        _combatHand.Discard(index);

        return spell;
    }

    internal Spell GetSpellFromLastHand(byte index) {
        if (_combatHand.LastGivenHand is null || index >= _combatHand.LastGivenHand.Count) {
            return null;
        }

        return _combatHand.LastGivenHand[index];
    }

    private void InitializePlayerSubCircle() {
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var wizard = ParticipantActor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
            .Result
            .Wizard;

        ParticipantGameStats = wizard.GameStats.GetCombatGameStats();
        _combatHand = new CombatHand(wizard.SpellbookBehavior.Spells, 7);

        CombatParticipant = new CombatParticipant {
            m_ownerID = ParticipantObject.m_globalID,
            m_templateID = 219902325553, // recorded from live
            m_isPlayer = true,
            m_teamID = 0,
            m_primaryMagicSchoolID = (int) wizard.MagicSchoolBehavior.MagicSchool,
            m_pipCount = new() { m_powerPips = 0, m_genericPips = 0 },
            m_pipRoundRates = new(),
            m_originalTeam = 0,
            m_maxHandSize = 7,
            m_playerHealth = ParticipantGameStats.m_currentHitpoints,
            m_maxPlayerHealth = ParticipantGameStats.m_baseHitpoints,
            m_myTeamTurn = _duelActor.Duel.m_firstTeamToAct == 0,
            m_pGameStats = ParticipantGameStats,
            m_pPlayDeck = new PlayDeck(),
            m_subcircle = 4,
            m_dynamicSymbol = DynamicSigilSymbol.Sun,

            m_color = _color,
            m_rotation = _rotation,
            m_radius = _radius,
        };
    }

    private void InitializeCreatureSubCircle() {
        var queryGameStatsMsg = new COMBAT_106_PROTOCOL.MSG_QUERYCREATURESTATS();
        var creatureStats = ParticipantActor
            .Ask<COMBAT_106_PROTOCOL.MSG_CREATURESTATS>(queryGameStatsMsg)
            .Result;

        ParticipantGameStats = creatureStats.GameStats;
        CombatParticipant = new CombatParticipant {
            m_ownerID = ParticipantObject.m_globalID,
            m_templateID = 2199023290637, // Captured 2199023290637 from live
            m_isPlayer = false,
            m_isMonster = 1u,
            m_teamID = 1,
            m_originalTeam = 1,
            m_maxHandSize = 7,
            m_primaryMagicSchoolID = 83375795,
            m_pipCount = new() { m_powerPips = 0, m_genericPips = 0 },
            m_pipRoundRates = new(),
            m_playerHealth = creatureStats.GameStats.m_currentHitpoints,
            m_maxPlayerHealth = creatureStats.GameStats.m_baseHitpoints,
            m_myTeamTurn = _duelActor.Duel.m_firstTeamToAct == 1,
            m_pGameStats = creatureStats.GameStats,
            m_mobLevel = creatureStats.CombatLevel,

            m_subcircle = 0,
            m_dynamicSymbol = DynamicSigilSymbol.Dagger,

            m_color = _color,
            m_rotation = _rotation,
            m_radius = _radius,
        };
    }

    private async Task PlayEntranceAnimation(CoreObject participantObject) {
        // Set the state of the participant to entering sigil.
        _duelActor.ZoneBroadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
            GameObjectID = participantObject.m_globalID,
            State = (uint) NPCStates.Sigil
        });

        // Send aggro to the participant.
        _duelActor.ZoneBroadcast(new WIZARD_12_PROTOCOL.MSG_AGGRO {
            GlobalID = participantObject.m_globalID,
            LocX = WorldPosition.X,
            LocY = WorldPosition.Y,
            LocZ = WorldPosition.Z,
            Yaw = WorldRotation,
            SigilGID = _duelActor.SigilId
        });

        // Wait the amount of time it takes for the actor to enter the sigil, then set
        // their state to combat idle.
        await Task.Delay((int) (AggroTimeInSeconds * 1000));

        // Set state to stationary.
        _duelActor.ZoneBroadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
            GameObjectID = participantObject.m_globalID,
            State = (uint) NPCStates.Stationary
        });
    }
}
