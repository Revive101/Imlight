/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Models.World;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Imlight.Common.Caches.TypeCache;
using static Imlight.Common.Caches.TypeCache.CombatParticipant;

namespace Imlight.CoreLib.Game.Combat;

internal enum CombatSlotType {
    Creature,
    Player
}

public class CombatDuelActorSubCircle {
    private const float AGGRO_TIME_IN_SECONDS = 0.75f;
    private const byte MAX_PIP_COUNT = 7;
    private const byte PLAYER_HAND_SIZE = 7;
    private const int CREATURE_SPELL_COUNT = 1000;

    internal string SlotName { get; set; }
    internal CombatSlotType SlotType { get; set; }
    internal int SlotIndex { get; private set; }
    internal Vector3 WorldPosition { get; set; }
    internal float WorldRotation { get; set; }
    internal IActorRef ParticipantActor { get; private set; }
    internal CoreObject ParticipantObject { get; private set; }
    internal ServerWizGameStats ParticipantGameStats { get; private set; }
    internal CombatParticipant CombatParticipant { get; private set; }
    internal bool AddedToDuel { get; set;}
    internal readonly List<SpellEffect> HangingEffects = new();
    public uint AvailableSpells {
        get {
            if (CombatDeck is null) {
                return 0;
            }

            return (uint) CombatDeck.RemainingCardCount;
        }
    }
    public uint TotalSpells {
        get {
            if (CombatDeck is null) {
                return 0;
            }

            return (uint) CombatDeck.TotalCardCount;
        }
    }
    internal bool Occupied => ParticipantObject is not null;
    internal CombatTeam OccupiedTeam {
        get {
            if (ParticipantObject is null) {
                return CombatTeam.Player;
            }

            return ParticipantObject.m_templateID == 1 ? CombatTeam.Player : CombatTeam.Monster;
        }
    }
    internal bool IsAlive => ParticipantGameStats?.m_currentHitpoints > 0;
    internal CombatDeck CombatDeck;

    private readonly CombatDuelActor _duelActor;
    private readonly float _radius;
    private readonly float _rotation;
    private readonly Color _color;

    // ctor
    internal CombatDuelActorSubCircle(CombatDuelActor duelActor, float radius, float rotation, Color color, int index) {
        _duelActor = duelActor;
        _radius = radius;
        _rotation = rotation;
        _color = color;
        SlotIndex = index;
    }

    internal CombatParticipant AssignParticipant(IActorRef actor, CoreObject participantObject) {
        ParticipantActor = actor;
        ParticipantObject = participantObject;
        var team = participantObject.m_templateID == 1 ? CombatTeam.Player : CombatTeam.Monster;

        // Set the CombatParticipant based on what team they are.
        if (team == CombatTeam.Player) {
            InitializePlayerSubCircle();
        }
        else {
            InitializeCreatureSubCircle();
        }

        // Inform the actor that they've been added to a duel.
        var msg = new COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL {
            DuelActor = _duelActor.ActorRef,
            Duel = _duelActor,
            SubCircle = this,
            SlotPosition = WorldPosition,
            SlotOrientation = WorldRotation
        };
        ParticipantActor.Tell(msg);

        PlayEntranceAnimation(participantObject);

        return this.CombatParticipant;
    }

    internal Hand DrawHand() {
        var newHand = CombatDeck.GetHand();
        CombatParticipant.m_pHand = newHand;

        return newHand;
    }

    internal void DiscardCard(Spell spell) {
        CombatDeck.Discard(spell);
    }

    internal Spell GetSpellFromLastHand(byte index) {
        if (CombatDeck.LastGivenHand is null || index >= CombatDeck.LastGivenHand.Count) {
            return null;
        }

        return CombatDeck.LastGivenHand[index];
    }

    internal void DoPipGain() {
        // If the participant has the maximum amount of pips, do not gain any more.
        var genericPips = CombatParticipant.m_pipCount.m_genericPips;
        var powerPips = CombatParticipant.m_pipCount.m_powerPips;
        if (genericPips + powerPips >= MAX_PIP_COUNT) {
            return;
        }

        var participant = CombatParticipant;
        var gainedPowerPip = DeterminePowerPipGain(participant);
        if (gainedPowerPip) {
            participant.m_pipCount.m_powerPips++;
        }
        else {
            participant.m_pipCount.m_genericPips++;
        }
    }

    internal bool HasSchoolMastery(uint magicSchoolID) {
        if (ParticipantGameStats.m_schoolID == magicSchoolID) {
            return true;
        }

        return (MagicSchool) magicSchoolID switch {
            MagicSchool.Storm   => ParticipantGameStats.m_stormMastery   > 0,
            MagicSchool.Fire    => ParticipantGameStats.m_fireMastery    > 0,
            MagicSchool.Ice     => ParticipantGameStats.m_iceMastery     > 0,
            MagicSchool.Myth    => ParticipantGameStats.m_mythMastery    > 0,
            MagicSchool.Life    => ParticipantGameStats.m_lifeMastery    > 0,
            MagicSchool.Death   => ParticipantGameStats.m_deathMastery   > 0,
            MagicSchool.Balance => ParticipantGameStats.m_balanceMastery > 0,
            _ => false,
        };
    }

    internal T GetStatBySchool<T>(List<T> list, MagicSchool enumValue) {
        if (list is null) {
            return default;
        }
        if (list.Count < 7) {
            throw new ArgumentException("List must have at least 7 items");
        }
        if (!typeof(T).IsPrimitive && !typeof(T).IsEnum) {
            throw new ArgumentException("List items must be primitive types or enums");
        }

        return enumValue switch {
            MagicSchool.Fire    => list[0],
            MagicSchool.Ice     => list[1],
            MagicSchool.Storm   => list[2],
            MagicSchool.Myth    => list[3],
            MagicSchool.Life    => list[4],
            MagicSchool.Death   => list[5],
            MagicSchool.Balance => list[6],
            _ => default,
        };
    }

    internal bool HasPipsForSpell(Spell spell) {
        var spellRank = spell.m_pipCost.m_spellRank;
        var genericPips = CombatParticipant.m_pipCount.m_genericPips;
        var powerPips = CombatParticipant.m_pipCount.m_powerPips;
        var isMastered = HasSchoolMastery(spell.m_magicSchoolID);

        // Power pips count as 2 generic pips if the spell is mastered.
        var totalPips = isMastered
            ? genericPips + (powerPips * 2)
            : genericPips + powerPips;

        return totalPips >= spellRank;
    }

    private void InitializePlayerSubCircle() {
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var wizard = ParticipantActor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
            .Result
            .Wizard;

        // Dyanmic symbols start at 9 for players.
        var dynamicSymbol = (DynamicSigilSymbol) (SlotIndex + 9);

        ParticipantGameStats = wizard.GameStats;
        var combatStats = ParticipantGameStats.GetCombatGameStats();

        // Collage spells the player has learned and temporary spells (perhaps from equipment)
        // into one list to create the combat hand.
        var allSpells = new List<SpellData>();
        allSpells.AddRange(wizard.SpellbookBehavior.SpellList);

        // Count temporary spells as 1 quantity.
        var temporarySpells = new List<SpellData>();
        foreach (var tempSpell in wizard.SpellbookBehavior.TemporarySpells) {
            // If the spell data already exists, increase the quantity.. otherwise add it.
            var existingSpell = temporarySpells.Find(s => s.m_templateID == tempSpell.m_templateID);
            if (existingSpell is not null) {
                existingSpell.m_quantity++;
            }
            else {
                temporarySpells.Add(new SpellData {
                    m_templateID = tempSpell.m_templateID,
                    m_quantity = 1
                });
            }
        }
        allSpells.AddRange(temporarySpells);
        CombatDeck = new CombatDeck(allSpells, PLAYER_HAND_SIZE);

        CombatParticipant = new CombatParticipant {
            m_ownerID = ParticipantObject.m_globalID,
            m_templateID = 219902325553, // recorded from live
            m_isPlayer = true,
            m_isMonster = 0,
            m_teamID = 0,
            m_primaryMagicSchoolID = (int) wizard.MagicSchoolBehavior.MagicSchool,
            m_pipCount = new() {
                m_powerPips = ParticipantGameStats.m_startingPowerPips,
                m_genericPips = ParticipantGameStats.m_startingPips
            },
            m_pipRoundRates = new(),
            m_originalTeam = 0,
            m_maxHandSize = PLAYER_HAND_SIZE,
            m_playerHealth = ParticipantGameStats.m_currentHitpoints,
            m_maxPlayerHealth = ParticipantGameStats.m_baseHitpoints,
            m_myTeamTurn = _duelActor.Duel.m_firstTeamToAct == 0,
            m_pGameStats = combatStats,
            m_pPlayDeck = new PlayDeck(),
            m_subcircle = SlotIndex,
            m_dynamicSymbol = dynamicSymbol,
            m_PipsSuspended = false,

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

        // Dynamic symbols start 1-4 for creatures.
        var dynamicSymbol = (DynamicSigilSymbol) (SlotIndex + 1);

        CombatDeck = new CombatDeck(creatureStats.SpellList, PLAYER_HAND_SIZE);

        ParticipantGameStats = creatureStats.GameStats;
        CombatParticipant = new CombatParticipant {
            m_ownerID = ParticipantObject.m_globalID,
            m_templateID = 2199023290637, // Captured 2199023290637 from live
            m_isPlayer = false,
            m_isMonster = 1, // Doesn't seem to be used.
            m_teamID = 1,
            m_originalTeam = 1,
            m_maxHandSize = PLAYER_HAND_SIZE,
            m_primaryMagicSchoolID = (int) creatureStats.MagicSchool,
            m_pipCount = new() {
                m_powerPips = ParticipantGameStats.m_startingPowerPips,
                m_genericPips = ParticipantGameStats.m_startingPips
            },
            m_pipRoundRates = new(),
            m_playerHealth = creatureStats.GameStats.m_currentHitpoints,
            m_maxPlayerHealth = creatureStats.GameStats.m_baseHitpoints,
            m_myTeamTurn = _duelActor.Duel.m_firstTeamToAct == 1,
            m_pGameStats = creatureStats.GameStats.GetCombatGameStats(),
            m_mobLevel = creatureStats.CombatLevel,

            m_subcircle = SlotIndex,
            m_dynamicSymbol = dynamicSymbol,

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
        await Task.Delay((int) (AGGRO_TIME_IN_SECONDS * 1000));

        // Set state to stationary.
        _duelActor.ZoneBroadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
            GameObjectID = participantObject.m_globalID,
            State = (uint) NPCStates.Stationary
        });
    }

    private bool DeterminePowerPipGain(CombatParticipant participant) {
        var stats = participant.m_pGameStats;
        var powerPipChance = 100 * (stats.m_powerPipBase + stats.m_powerPipBonusPercentAll);

        var powerPipRoll = new Random().Next(0, 100);
        return powerPipRoll <= powerPipChance;
    }
}
