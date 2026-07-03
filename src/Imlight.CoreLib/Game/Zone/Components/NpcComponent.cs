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
 * NPC COMPONENT 
 * ========================================================================
 * 
 * PURPOSE:
 * Manages core NPC behavior and interaction mechanics, including 
 * combat, proximity detection, and statistical attributes.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Supports monster and non-monster NPC configurations.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class NpcComponent : ZoneEntityComponent, IComponentFactory, IClientBehaviorProvider<NPCBehavior> {

    public bool NoTransfer { get; set; } = false;
    public bool IsMonster { get; private set; }
    public bool IsBossMonster { get; private set; }
    public float IntelligenceFactor { get; private set; }
    public float SelfishnessFactor { get; private set; }
    public float AggressiveFactor { get; private set; }
    public int StartingHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    public MagicSchool MagicSchool { get; private set; }
    public int Level { get; private set; }
    public float Proximity { get; private set; }
    public string NameOverride { get; private set; }

    private readonly Dictionary<CoreObject, IActorRef> _playersInRange = [];
    private readonly NPCBehaviorTemplate _npcBehaviorTemplate;
    private readonly DuelistBehaviorTemplate _duelistBehaviorTemplate;

    private StatsComponent _statsComponent;
    private CombatCreatureDeckComponent _deckComponent;

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate);

    public NpcComponent(ZoneEntity entity) : base(entity) {
        _npcBehaviorTemplate = entity.Template.m_behaviors
            .OfType<NPCBehaviorTemplate>()
            .First();
        _duelistBehaviorTemplate = entity.Template.m_behaviors
            .OfType<DuelistBehaviorTemplate>()
            .FirstOrDefault();

        this.IsBossMonster = _npcBehaviorTemplate.m_bossMob;
        this.IntelligenceFactor = _npcBehaviorTemplate.m_fIntelligence;
        this.SelfishnessFactor = _npcBehaviorTemplate.m_fSelfishFactor;
        this.AggressiveFactor = _npcBehaviorTemplate.m_nAggressiveFactor;
        this.StartingHealth = _npcBehaviorTemplate.m_nStartingHealth;

        this.IsMonster = _duelistBehaviorTemplate is not null;
        this.Proximity = _duelistBehaviorTemplate?.m_npcProximity ?? 0;
        
        // Try to parse the npcBehaviorTemplate.m_schoolOfFocus to a MagicSchool.
        var parsedSchool = MagicSchool.Balance;
        if (    _npcBehaviorTemplate.m_schoolOfFocus != "" 
            && !Enum.TryParse(_npcBehaviorTemplate.m_schoolOfFocus, out parsedSchool)) {
            Logger.Error("Failed to parse magic school {0} for creature {1}.",
                Logger.Args(_npcBehaviorTemplate.m_schoolOfFocus, Entity.ActiveGameObject.m_globalID));

            return;
        }

        this.MagicSchool = parsedSchool;
        this.Level = _npcBehaviorTemplate.m_nLevel;
    }

    public override void OnStart() {
        // All NPCs have game stats.
        _statsComponent = Entity.GetComponentOfType<StatsComponent>();
        if (_statsComponent is null) {
            Logger.Error("NPC {0} does not have a StatsComponent.", Logger.Args(Entity.ActiveGameObject.m_globalID));

            return;
        }

        _statsComponent.Stats.m_currentHitpoints = StartingHealth;
        _statsComponent.Stats.m_baseHitpoints = StartingHealth;

        // Boss mobs will sometimes have base effects in their NPC template.
        if (_npcBehaviorTemplate.m_baseEffects.Count > 0) {
            foreach (var effect in _npcBehaviorTemplate.m_baseEffects) {
                CharacterEffectHelper.AddGameEffectToStats(_statsComponent.Stats, effect);
            }
        }

        // If the NPC is a monster, it will have a deck.
        if (IsMonster) {
            _deckComponent = Entity.GetComponentOfType<CombatCreatureDeckComponent>();
            if (_deckComponent is null) {
                Logger.Error("NPC {0} does not have a CombatDeckComponent.", 
                    Logger.Args(Entity.ActiveGameObject.m_debugName));

                return;
            }
        }
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        // Check if the player is now in range of the object.
        if (IsInRadius(playerObj, Proximity) && !_playersInRange.ContainsKey(playerObj)) {
            // If the player is in range, trigger the enter events.
            OnProximityEnter(playerObj, playerActor, playerWizard);
            _playersInRange.Add(playerObj, playerActor);
        } 
        else if (!IsInRadius(playerObj, Proximity) && _playersInRange.ContainsKey(playerObj)) {
            _playersInRange.Remove(playerObj);
        }
    }

    public NPCBehavior GetClientBehaviorInstance() => new() {
        m_isMonster = IsMonster,
        m_wsNameOverride = NameOverride,
    };

    private void OnProximityEnter(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        // Bug fix: There is an off chance the player spawns in the middle of our proximity radius,
        // before the wizard even has time to load themselves into the world.
        if (playerWizard is null) {
            return;
        }

        if (!IsMonster || playerWizard.IsInCombatGrace || playerWizard.IsInDuel) {
            return;
        }

        // Hey! I'm a dueling creature and a player just entered my proximity.
        // I really don't like that.
        var interactionMsg = new ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL {
            StartingParticipants = new Dictionary<IActorRef, CoreObject> {
                { playerActor, playerObj },
                { Entity.SelfRef, Entity.ActiveGameObject },
            },
        };
        Entity.ZoneRef.Tell(interactionMsg);

        // We do nothing further here. This message will be sent to the ZoneSigilSupervisor
        // to locate the closest sigil to the player and the creature.
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_QUERYCREATURESTATS))]
    private void ReceiveQueryGameStats(COMBAT_106_PROTOCOL.MSG_QUERYCREATURESTATS message) {
        var rsp = new COMBAT_106_PROTOCOL.MSG_CREATURESTATS {
            GameStats = _statsComponent.Stats,
            CombatIntelligence = IntelligenceFactor,
            CombatSelfishFactor = SelfishnessFactor,
            CombatAggressionFactor = AggressiveFactor,
            CombatLevel = Level,
            MagicSchool = MagicSchool,
            SpellList = _deckComponent?.Spells ?? [],
        };

        Sender.Tell(rsp);
    }

}