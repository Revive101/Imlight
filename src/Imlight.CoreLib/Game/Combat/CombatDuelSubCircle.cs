/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * COMBAT PARTICIPANT CONTROLLER
 * ========================================================================
 * 
 * PURPOSE:
 * Manages individual participant data and state within a duel, providing
 * methods to modify health, mana, pips, and track spell effects on the participant.
 * 
 * USAGE EXAMPLE:
 * var subCircle = new CombatDuelSubCircle(duelActor, radius, rotation, color, index);
 * subCircle.AssignParticipant(actor, participantObject);
 * subCircle.DamageParticipant(damage);
 * 
 * NOTE:
 * Each subcircle handles deck management, spell casting costs, and
 * participant-specific stat calculations during effect resolution.
 * 
 * TODO:
 * - This should derive from ZoneEntityComponent
 * - Check if creature can be stunned
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.Math;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Imlight.CoreLib.Game.Combat;

internal enum CombatSlotType {
    
    Creature,
    Player

}

/// <summary>
/// Represents a position in the combat duel for a single participant, managing their combat state, spells, and effects.
/// </summary>
/// <remarks>
/// Each subcircle maintains a participant's combat data including their position in the duel, stats, cards, pips, 
/// and hanging effects. It provides methods to manipulate the participant's state through damage, healing, and
/// spell effects. Subcircles are assigned to either the player or monster team and positioned within the duel
/// according to the sigil template.
/// </remarks>
public class CombatDuelSubCircle {
    
    private const float AGGRO_TIME_IN_SECONDS = 0.75f;
    private const byte MAX_PIP_COUNT = 7;
    private const byte PLAYER_HAND_SIZE = 7;

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
    internal bool IsSummonedMinion { get; private set; }
    internal List<SpellEffect> _hangingEffects { get {
        if (CombatParticipant is null) {
            return null;
        }
        if (CombatParticipant is not null && CombatParticipant.m_hangingEffects is null) {
            CombatParticipant.m_hangingEffects = [];
        }

        return CombatParticipant.m_hangingEffects;
    }}
    public uint AvailableSpells {
        get {
            if (_combatDeck is null) {
                return 0;
            }

            return (uint) _combatDeck.RemainingCardCount;
        }
    }
    public uint TotalSpells {
        get {
            if (_combatDeck is null) {
                return 0;
            }

            return (uint) _combatDeck.TotalCardCount;
        }
    }
    internal bool Occupied => ParticipantObject is not null;
    internal CombatTeam OccupiedTeam {
        get {
            if (ParticipantObject is null) {
                return CombatTeam.Player;
            }

            if (IsSummonedMinion) {
                return CombatTeam.Player;
            }

            return ParticipantObject.m_templateID == 1 ? CombatTeam.Player : CombatTeam.Monster;
        }
    }
    internal bool IsAlive => ParticipantGameStats?.m_currentHitpoints > 0;
    internal CombatDeck _combatDeck;
    internal readonly CombatDuelComponent _duelActor;
    internal Wizard _wizard;
    internal int _usedPipsForExperienceGain = 0;

    private readonly float _radius;
    private readonly float _rotation;
    private readonly Color _color;

    // ctor
    internal CombatDuelSubCircle(CombatDuelComponent duelActor, float radius, float rotation, Color color, int index) {
        _duelActor = duelActor;
        _radius = radius;
        _rotation = rotation;
        _color = color;
        SlotIndex = index;
    }

    internal CombatParticipant AssignParticipant(IActorRef actor, CoreObject participantObject, bool isSummonedMinion = false) {
        ParticipantActor = actor;
        ParticipantObject = participantObject;
        IsSummonedMinion = isSummonedMinion;

        var isHumanPlayer = participantObject.m_templateID == 1;

        // Set the CombatParticipant based on what team they are.
        if (isHumanPlayer) {
            InitializePlayerSubCircle();
        }
        else {
            InitializeCreatureSubCircle(isSummonedMinion);
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

        // We don't need to await this.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        PlayEntranceAnimation(participantObject, actor);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        return this.CombatParticipant;
    }

    internal void RemoveParticipant() {
        ParticipantActor = null;
        ParticipantObject = null;
        CombatParticipant = null;
        AddedToDuel = false;
    }

    internal Hand DrawHand() {
        var newHand = _combatDeck.GetHand();
        CombatParticipant.m_pHand = newHand;

        return newHand;
    }

    internal Hand GetCurrentHand() {
        // As-is, no draw or refill: a discard frees a slot this turn and the client must see it open.
        return new() { m_spellList = _combatDeck.LastGivenHand ?? [] };
    }

    internal void DiscardCard(Spell spell) {
        _combatDeck.Discard(spell);
    }

    internal Spell GetSpellFromLastHand(byte index) {
        if (_combatDeck.LastGivenHand is null || index >= _combatDeck.LastGivenHand.Count) {
            return null;
        }

        return _combatDeck.LastGivenHand[index];
    }

    internal void DoPipGain() {
        // If the participant has the maximum amount of pips, do not gain any more.
        var genericPips = CombatParticipant.m_pipCount.m_genericPips;
        var powerPips = CombatParticipant.m_pipCount.m_powerPips;
        if (genericPips + powerPips >= MAX_PIP_COUNT) {
            return;
        }

        var gainedPowerPip = DeterminePowerPipGain(CombatParticipant);
        if (gainedPowerPip) {
            CombatParticipant.m_pipCount.m_powerPips++;
        }
        else {
            CombatParticipant.m_pipCount.m_genericPips++;
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

    internal bool HasSchoolMastery(string school) {
        if (Enum.TryParse<MagicSchool>(school, out var magicSchool)) {
            return HasSchoolMastery((uint)magicSchool);
        }

        Logger.Warning("Failed to parse magic school \"{0}\" from string.", Logger.Args(school));

        return false;
    }

    internal T GetStatBySchool<T>(List<T> list, string magicSchool) {
        if (list is null) {
            return default;
        }
        if (!typeof(T).IsPrimitive && !typeof(T).IsEnum) {
            throw new ArgumentException("List items must be primitive types or enums");
        }

        // Check if we're over the max index.
        var maxIndex = MagicSchools.GetMaxMagicSchoolIndex();
        if (list.Count < maxIndex) {
            throw new ArgumentException("List must have a count equal to the max magic school index.");
        }

        var index = (int) MagicSchools.GetMagicSchool(magicSchool).m_schoolIndex;

        return list[index];
    }

    internal bool HasPipsForSpell(Spell spell) {
        // X-pip spells scale to any pip count; no fixed minimum.
        if (CombatActionResolver.IsXPipSpell(spell)) {
            return true;
        }

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

    internal bool TryStun() {
        // todo: check if this creature can be stunned.
        CombatParticipant.m_stunned = 1;

        return true;
    }

    internal void DamageParticipant(int damage) {
        // If the participant is a player, update their health.
        if (_wizard is not null) {
            var currentHealth = ParticipantGameStats.m_currentHitpoints;
            var newHealth = currentHealth - damage;

            // Make sure the health doesn't go below 0.
            if (newHealth < 0) {
                newHealth = 0;
            }

            // Update the health of the player.
            _wizard.UpdateHealth(newHealth);
        }
        else {
            // Make sure the health doesn't go below 0.
            if (ParticipantGameStats.m_currentHitpoints - damage < 0) {
                ParticipantGameStats.m_currentHitpoints = 0;
            }
            else {
                ParticipantGameStats.m_currentHitpoints -= damage;
            }
        }
    }

    internal void HealParticipant(int heal) {
        // If the participant is a player, update their health.
        if (_wizard is not null) {
            var currentHealth = ParticipantGameStats.m_currentHitpoints;
            var newHealth = currentHealth + heal;

            // Make sure the health doesn't go above the max health.
            if (newHealth > ParticipantGameStats.m_baseHitpoints) {
                newHealth = ParticipantGameStats.m_baseHitpoints;
            }

            // Update the health of the player.
            _wizard.UpdateHealth(newHealth);
        }
        else {
            // Make sure the health doesn't go above the max health.
            if (ParticipantGameStats.m_currentHitpoints + heal > ParticipantGameStats.m_baseHitpoints) {
                ParticipantGameStats.m_currentHitpoints = ParticipantGameStats.m_baseHitpoints;
            }
            else {
                ParticipantGameStats.m_currentHitpoints += heal;
            }
        }
    }

    internal void DeductMana(int mana) {
        // If the participant is a player, update their mana.
        // Creature's don't have mana.
        if (_wizard is not null) {
            var currentMana = ParticipantGameStats.m_currentMana;
            var newMana = currentMana - mana;

            // Make sure the mana doesn't go below 0.
            if (newMana < 0) {
                newMana = 0;
            }

            // Update the mana of the player.
            _wizard.UpdateMana(newMana);
        }
    }

    internal void DeductPips(MagicSchool school, byte spellRank) {
        var isMastered = HasSchoolMastery((uint) school);
        var pipCount = CombatParticipant.m_pipCount;

        // Deduct pips based on the spell rank.
        // We have a second conditional here incase of byte overflow.
        while (spellRank is > 0 and < (MAX_PIP_COUNT * 2)) {
            if (isMastered && pipCount.m_powerPips > 0) {
                pipCount.m_powerPips--;
                spellRank -= 2;
            }
            else if (!isMastered && pipCount.m_powerPips > 0) {
                pipCount.m_powerPips--;
                spellRank--;
            }
            else if (pipCount.m_powerPips == 0 && pipCount.m_genericPips > 0) {
                pipCount.m_genericPips--;
                spellRank--;
            }
            else if (pipCount.m_powerPips == 0 && pipCount.m_genericPips == 0) {
                break;
            }
        }
    }

    internal void DeductAllPips() {
        var ourPipCount = CombatParticipant.m_pipCount;
        ourPipCount.m_powerPips = 0;
        ourPipCount.m_genericPips = 0;
    }

    internal void Reshuffle() => _combatDeck.Reshuffle();

    /// <summary>
    /// Draws a random treasure card from the vault and adds it to the current hand.
    /// </summary>
    /// <returns>The drawn spell, or null if no vault cards available or hand is full.</returns>
    internal Spell DrawFromVault() => _combatDeck.DrawFromVault();

    /// <summary>
    /// Gets the number of treasure cards remaining in the vault.
    /// </summary>
    internal int VaultRemainingCount => _combatDeck.VaultRemainingCount;

    /// <summary>
    /// Gets the number of treasure cards currently in the hand.
    /// </summary>
    internal int TreasureCardsInHand => _combatDeck.TreasureCardsInHand;

    /// <summary>
    /// Permanently consumes a successfully cast treasure card from the vault.
    /// </summary>
    internal uint ConsumeFromVault(Spell spell) => _combatDeck.ConsumeFromVault(spell);

    private void InitializePlayerSubCircle() {
        // todo: this method is a mess.
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        _wizard = ParticipantActor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
            .Result
            .Wizard;

        // Dyanmic symbols start at 9 for players.
        var dynamicSymbol = (DynamicSigilSymbol) (SlotIndex + 9);

        ParticipantGameStats = _wizard.GameStats;
        var combatStats = ParticipantGameStats.GetCombatGameStats();

        // Collage spells the player has learned and temporary spells (perhaps from equipment)
        // into one list to create the combat hand. Treasure cards go into a separate vault.
        var allSpells = new List<CombatDeckSpellData>();
        var vaultSpells = new List<CombatDeckSpellData>();
        if (_wizard.SpellbookBehavior.SpellList is not null) {
            foreach (var spell in _wizard.SpellbookBehavior.SpellList) {
                var template = CoreObjectFactory.GetCoreTemplate(spell.m_templateID);
                var isTreasureCard = template is SpellTemplate spellTemplate
                    && (spellTemplate.m_Treasure || spellTemplate.m_name.EndsWith(" TC"));

                // A deck entry the player hasn't learned can only be a treasure card (item cards live in
                // TemporarySpells). Guarded on a populated list so an empty list can't misclassify the deck.
                if (!isTreasureCard
                    && _wizard.SpellbookBehavior.LearnedSpellTemplateIds is { Count: > 0 }
                    && !_wizard.SpellbookBehavior.LearnedSpellTemplateIds.Contains(spell.m_templateID)) {
                    isTreasureCard = true;
                }

                if (isTreasureCard) {
                    // Treasure cards go to the vault (separate pool, drawn on demand).
                    vaultSpells.Add(new CombatDeckSpellData {
                        TemplateId = spell.m_templateID,
                        Quantity = spell.m_quantity,
                        IsTreasureCard = true
                    });
                }
                else {
                    // Regular spells go to the main deck.
                    allSpells.Add(new CombatDeckSpellData {
                        TemplateId = spell.m_templateID,
                        Quantity = spell.m_quantity,
                    });
                }
            }
        }

        // Count temporary spells as 1 quantity, skipping any that the player
        // has excluded via the spell deck UI (MSG_UPDATEITEMSPELLEXCLUSIONLIST).
        var temporarySpells = new List<CombatDeckSpellData>();
        var equippedDeckId = _wizard.EquipmentBehavior.SlotList
            .FirstOrDefault(s => s.SlotType == EquipmentSlotType.Deck)?.ItemId;
        foreach (var tempSpell in _wizard.SpellbookBehavior.TemporarySpells) {
            // Check if this item spell is excluded for the equipped deck.
            if (equippedDeckId != null
                && _wizard.SpellbookBehavior.IsItemSpellExcluded(equippedDeckId.Value, tempSpell.m_templateID)) {
                continue;
            }

            // If the spell data already exists, increase the quantity.. otherwise add it.
            var existingSpell = temporarySpells.Find(s => s.TemplateId == tempSpell.m_templateID);
            if (existingSpell is not null) {
                existingSpell.Quantity++;
            }
            else {
                temporarySpells.Add(new CombatDeckSpellData {
                    TemplateId = tempSpell.m_templateID,
                    Quantity = 1,
                    IsItemCard = true
                });
            }
        }
        allSpells.AddRange(temporarySpells);
        _combatDeck = new CombatDeck(allSpells, vaultSpells, PLAYER_HAND_SIZE);

        CombatParticipant = new CombatParticipant {
            m_ownerID = ParticipantObject.m_globalID,
            m_templateID = 219902325553, // recorded from live
            m_isPlayer = true,
            m_zoneID = _duelActor.SigilId,
            m_isMonster = 0,
            m_teamID = 0,
            m_primaryMagicSchoolID = (int) _wizard.MagicSchoolBehavior.MagicSchool,
            m_pipCount = DetermineStartingPips(),
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

    private void InitializeCreatureSubCircle(bool asMinion = false) {
        var queryGameStatsMsg = new COMBAT_106_PROTOCOL.MSG_QUERYCREATURESTATS();
        var creatureStats = ParticipantActor
            .Ask<COMBAT_106_PROTOCOL.MSG_CREATURESTATS>(queryGameStatsMsg)
            .Result;

        // Dynamic symbols start 1-4 for creatures.
        var dynamicSymbol = (DynamicSigilSymbol) (SlotIndex + 1);

        // Convert the creature stats to a combat deck.
        var spellData = new List<CombatDeckSpellData>();
        foreach (var spell in creatureStats.SpellList) {
            if (spell is null) {
                continue;
            }

            spellData.Add(new CombatDeckSpellData {
                TemplateId = spell.m_templateID,
                Quantity = spell.m_quantity
            });
        }

        _combatDeck = new CombatDeck(spellData, [], PLAYER_HAND_SIZE);

        ParticipantGameStats = creatureStats.GameStats;
        CombatParticipant = new CombatParticipant {
            m_ownerID = ParticipantObject.m_globalID,
            m_templateID = 2199023290637, // Captured 2199023290637 from live
            m_isPlayer = false,
            m_zoneID = _duelActor.SigilId,
            m_isMonster = 0, // Live server sends 0
            // Minions are creatures on the player team; m_isPlayer stays false.
            m_teamID = asMinion ? 0 : 1,
            m_originalTeam = 0,
            m_isMinion = asMinion,
            m_maxHandSize = PLAYER_HAND_SIZE,
            m_primaryMagicSchoolID = (int) creatureStats.MagicSchool,
            m_pipCount = DetermineStartingPips(),
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

    private async Task PlayEntranceAnimation(CoreObject participantObject, IActorRef participantActor) {
        // Send the "Sigil" state to the zone so the client transitions the object.
        _duelActor.ZoneBroadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = participantObject.m_globalID,
            State = StringHash.Compute("Sigil")
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

        // Set the actual position of the game object to the sigil.
        participantObject.m_location = new Vector3(WorldPosition.X, WorldPosition.Y, WorldPosition.Z);
        participantObject.m_orientation = new Vector3(0, 0, WorldRotation);

        // Wait the amount of time it takes for the actor to enter the sigil, then set
        // their state to stationary.
        await Task.Delay((int) (AGGRO_TIME_IN_SECONDS * 1000));

        // Broadcast the "Stationary" state to the zone so the client knows the
        // entrance animation is complete and the participant is now at the sigil.
        _duelActor.ZoneBroadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = participantObject.m_globalID,
            State = StringHash.Compute("Stationary")
        });
    }

    private PipCount DetermineStartingPips() {
        var pipCount = new PipCount() {
            m_powerPips = ParticipantGameStats.m_startingPowerPips,
            m_genericPips = ParticipantGameStats.m_startingPips
        };

        // Ensure that the total number of pips does not exceed MAX_PIP_COUNT.
        if (pipCount.m_genericPips + pipCount.m_powerPips > MAX_PIP_COUNT) {
            int excessPips = pipCount.m_genericPips + pipCount.m_powerPips - MAX_PIP_COUNT;

            // Reduce generic pips first if there is an excess
            if (excessPips <= pipCount.m_genericPips) {
                pipCount.m_genericPips -= (byte) excessPips;
            }
            else {
                // If excess pips are more than generic pips, set generic pips to 0
                // and adjust power pips accordingly
                excessPips -= pipCount.m_genericPips;
                pipCount.m_genericPips = 0;
                pipCount.m_powerPips -= (byte) excessPips;
            }
        }

        return pipCount;
    }

    private bool DeterminePowerPipGain(CombatParticipant participant) {
        var stats = participant.m_pGameStats;
        var powerPipChance = 100 * (stats.m_powerPipBase + stats.m_powerPipBonusPercentAll);

        var powerPipRoll = Random.Shared.Next(0, 100);
        return powerPipRoll <= powerPipChance;
    }

}
