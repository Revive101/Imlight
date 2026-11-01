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
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/01/2026
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.PluginApi;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Exposes a creature to plugins as an <see cref="ICreature"/>.
/// </summary>
/// <remarks>
/// Reads the creature's live object and components, which its own actor changes, the same way <see cref="PluginPlayer"/>
/// reads its wizard. Only combat creatures are wrapped, and every one has an NPC, stats and AI component.
/// </remarks>
internal sealed class PluginCreature(ZoneEntity entity, IZone zone) : ICreature {

    private readonly CoreObject _creature = entity.ActiveGameObject;
    private readonly string _name = ResolveName(entity.Template);
    private readonly NpcComponent _npc = entity.GetComponentOfType<NpcComponent>();
    private readonly StatsComponent _stats = entity.GetComponentOfType<StatsComponent>();
    private readonly CombatCreatureAIComponent _ai = entity.GetComponentOfType<CombatCreatureAIComponent>();
    // A creature's deck is built once, when it is created.
    private readonly ImmutableList<uint> _spells = [
        .. entity.GetComponentOfType<CombatCreatureDeckComponent>()?.Spells.Select(x => x.m_templateID) ?? []
    ];
    private volatile bool _stopped;

    public ZoneEntity Entity => entity;

    public ulong GlobalId => _creature.m_globalID.Full;

    public ulong TemplateId => _creature.m_templateID.Full;

    public string Name => _name;

    public IZone Zone => zone;

    public System.Numerics.Vector3 Location
        => new(_creature.m_location.X, _creature.m_location.Y, _creature.m_location.Z);

    public float Orientation => _creature.m_orientation.Z;

    public int Level => _npc.Level;

    public School School => _npc.MagicSchool.ToPluginSchool();

    public int Health => _stats.Stats.m_currentHitpoints;

    public int MaxHealth => _stats.Stats.m_baseHitpoints;

    public bool IsBoss => _npc.IsBossMonster;

    public bool IsMinion => entity.IsCombatOnlyMinion;

    public bool InCombat => _ai.InDuel;

    public bool IsDead => _stopped || _ai.RemovedFromDuel;

    public float Intelligence => _npc.IntelligenceFactor;

    public float Aggressiveness => _npc.AggressiveFactor;

    public float Selfishness => _npc.SelfishnessFactor;

    public float Proximity => _npc.Proximity;

    public IReadOnlyList<uint> Spells => _spells;

    /// <summary>
    /// Records that the creature's actor has stopped.
    /// </summary>
    public void MarkStopped() => _stopped = true;

    // A stopped actor drops these, which is what makes calls on a dead creature do nothing.

    public void SetHealth(int health)
        => Send(new ZONE_102_PROTOCOL.MSG_SETCREATUREHEALTH { Health = health });

    public void SetMaxHealth(int maxHealth) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHealth);

        Send(new ZONE_102_PROTOCOL.MSG_SETCREATUREHEALTH { MaxHealth = maxHealth });
    }

    public void StopMoving()
        => Send(new ZONE_102_PROTOCOL.MSG_SETCREATUREPAUSED { Paused = true });

    public void ResumeMoving()
        => Send(new ZONE_102_PROTOCOL.MSG_SETCREATUREPAUSED { Paused = false });

    public void Despawn()
        => Send(new ZONE_102_PROTOCOL.MSG_DESPAWNCREATURE());

    private void Send(object message)
        => entity.SelfRef.Tell(message, null);

    private static string ResolveName(CoreTemplate template) {
        if (template is GameObjectTemplate gameObjectTemplate) {
            var displayName = Locale.GetEnglishName(gameObjectTemplate.m_displayName.ToString());
            if (!string.IsNullOrEmpty(displayName)) {
                return displayName;
            }

            var objectName = gameObjectTemplate.m_objectName.ToString();
            if (!string.IsNullOrEmpty(objectName)) {
                return objectName;
            }
        }

        return "";
    }

}
