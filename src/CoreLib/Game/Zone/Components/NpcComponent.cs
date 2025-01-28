/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class NpcComponent : ZoneEntityComponent, IComponentFactory, IClientBehaviorProvider<NPCBehavior> {

    public bool NoTransfer { get; set; } = false;
    public bool IsMonster { get; private set; }
    public bool IsBossMonster { get; private set; }
    public float Intelligence { get; private set; }
    public float SelfishFactor { get; private set; }
    public float AggressiveFactor { get; private set; }
    public int StartingHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    public MagicSchool MagicSchool { get; private set; }
    public int Level { get; private set; }
    public float Proximity { get; private set; }
    public string NameOverride { get; private set; }

    private readonly Dictionary<CoreObject, IActorRef> _playersInRange = [];

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate);

    public NpcComponent(ZoneEntity entity) : base(entity) {
        var npcBehaviorTemplate = entity.Template.m_behaviors
            .OfType<NPCBehaviorTemplate>()
            .First();
        var duelistBehaviorTemplate = entity.Template.m_behaviors
            .OfType<DuelistBehaviorTemplate>()
            .First();

        this.IsBossMonster = npcBehaviorTemplate.m_bossMob;
        this.Intelligence = npcBehaviorTemplate.m_fIntelligence;
        this.SelfishFactor = npcBehaviorTemplate.m_fSelfishFactor;
        this.AggressiveFactor = npcBehaviorTemplate.m_nAggressiveFactor;
        this.StartingHealth = npcBehaviorTemplate.m_nStartingHealth;

        this.IsMonster = duelistBehaviorTemplate is not null;
        this.Proximity = duelistBehaviorTemplate?.m_npcProximity ?? 0;
        
        // Try to parse the npcBehaviorTemplate.m_schoolOfFocus to a MagicSchool.
        var parsedSchool = MagicSchool.Balance;
        if (    npcBehaviorTemplate.m_schoolOfFocus != "" 
            && !Enum.TryParse(npcBehaviorTemplate.m_schoolOfFocus, out parsedSchool)) {
            Logger.Error("Failed to parse magic school {0} for creature {1}.",
                Logger.Args(npcBehaviorTemplate.m_schoolOfFocus, Entity.ActiveGameObject.m_globalID));

            return;
        }

        this.MagicSchool = parsedSchool;
        this.Level = npcBehaviorTemplate.m_nLevel;
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor) {
        // Check if the player is now in range of the object.
        if (IsInRadius(playerObj, Proximity) && !_playersInRange.ContainsKey(playerObj)) {
            // If the player is in range, trigger the enter events.
            OnProximityEnter(playerObj, playerActor);
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

    private void OnProximityEnter(CoreObject playerObj, IActorRef playerActor) {
        if (!IsMonster || !IsBossMonster) {
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

}