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
using Imlight.CoreLib.Shared.Resources;
using Imlight.PluginApi;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Exposes a creature to plugins as an <see cref="ICreature"/>.
/// </summary>
/// <remarks>
/// Reads the creature's live object, which its own actor moves, the same way <see cref="PluginPlayer"/> reads its wizard.
/// </remarks>
internal sealed class PluginCreature(CoreObject creature, CoreTemplate template, IZone zone) : ICreature {

    private readonly string _name = ResolveName(template);

    public CoreObject Creature => creature;

    public ulong GlobalId => creature.m_globalID.Full;

    public ulong TemplateId => creature.m_templateID.Full;

    public string Name => _name;

    public IZone Zone => zone;

    public System.Numerics.Vector3 Location
        => new(creature.m_location.X, creature.m_location.Y, creature.m_location.Z);

    public float Orientation => creature.m_orientation.Z;

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
