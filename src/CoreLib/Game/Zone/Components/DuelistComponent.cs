/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Behaviors;
using System;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class DuelistComponent : ZoneEntityComponent, IComponentFactory, IClientBehaviorProvider<NPCBehavior> {

    public bool NoTransfer { get; set; } = false;
    public bool IsMonster { get; private set; } = true;
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

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && gameObjectTemplate.m_behaviors.Any(x => x is DuelistBehaviorTemplate);

    public DuelistComponent(ZoneEntity entity) : base(entity) {
        var npcBehaviorTemplate = entity.Template.m_behaviors
            .OfType<NPCBehaviorTemplate>()
            .First();
        var duelistBehaviorTemplate = entity.Template.m_behaviors
            .OfType<DuelistBehaviorTemplate>()
            .First();

        // IsMonster is always true, so long as the entity has a DuelistTemplate.

        this.IsBossMonster = npcBehaviorTemplate.m_bossMob;
        this.Intelligence = npcBehaviorTemplate.m_fIntelligence;
        this.SelfishFactor = npcBehaviorTemplate.m_fSelfishFactor;
        this.AggressiveFactor = npcBehaviorTemplate.m_nAggressiveFactor;
        this.StartingHealth = npcBehaviorTemplate.m_nStartingHealth;
        this.Proximity = duelistBehaviorTemplate.m_npcProximity;
        
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

    public NPCBehavior GetClientBehaviorInstance() => new() {
        m_isMonster = true,
        m_wsNameOverride = NameOverride,
    };

}