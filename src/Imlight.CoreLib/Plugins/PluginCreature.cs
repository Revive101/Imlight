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
 * Last Updated: 10/27/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Game.Zone.Core;
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
