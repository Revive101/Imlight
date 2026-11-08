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
 * Last Updated: 11/08/2026
 */

using System;
using System.Numerics;

namespace Imlight.PluginApi.Npcs;

/// <summary>
/// An NPC a plugin spawned with <see cref="INpcManager.Spawn"/>. It never fights, and players can talk to it once it
/// has an interaction.
/// </summary>
/// <remarks>
/// An NPC lives until it is removed or its zone unloads, and is never saved. To keep one in a zone, spawn it again in
/// an <see cref="Events.OnZoneLoad"/> handler. Every call is safe from any thread.
/// </remarks>
public interface INpc {

    /// <summary>
    /// The NPC's object ID in the zone.
    /// </summary>
    ulong GlobalId { get; }

    /// <summary>
    /// The template the NPC was spawned from, which sets how it looks.
    /// </summary>
    ulong TemplateId { get; }

    /// <summary>
    /// The name the NPC was given, or its template's name if it was given none.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The zone the NPC was spawned in.
    /// </summary>
    IZone Zone { get; }

    /// <summary>
    /// The NPC's position in its zone. An NPC never moves.
    /// </summary>
    Vector3 Location { get; }

    /// <summary>
    /// The direction the NPC is facing, in radians.
    /// </summary>
    float Orientation { get; }

    /// <summary>
    /// True once the NPC was removed or its zone unloaded. Calls on a removed NPC do nothing.
    /// </summary>
    bool IsRemoved { get; }

    /// <summary>
    /// True while the NPC has an interaction, so players near it are offered to interact.
    /// </summary>
    bool IsInteractable { get; }

    /// <summary>
    /// Sets what happens when a player interacts with the NPC, or makes it not interactable when null.
    /// </summary>
    /// <remarks>
    /// The handler runs on the NPC's own actor. A player already near the NPC sees the change the next time they
    /// walk up to it.
    /// </remarks>
    void SetInteraction(Action<NpcInteraction>? handler);

    /// <summary>
    /// The icon over the NPC's head. It only shows while the NPC is interactable.
    /// </summary>
    NpcWizBang WizBang { get; }

    /// <summary>
    /// Sets the icon over the NPC's head, such as <see cref="NpcWizBang.StartQuest"/> to show players it has
    /// something for them. It only shows while the NPC is interactable, and nearby players see a change within a
    /// second.
    /// </summary>
    void SetWizBang(NpcWizBang wizBang);

    /// <summary>
    /// Puts the NPC in an animation state from its template, such as <c>Idle</c>. Players who arrive later see it in
    /// the same state. The state <c>Off</c> hides the NPC.
    /// </summary>
    void SetState(string state);

    /// <summary>
    /// Removes the NPC from its zone.
    /// </summary>
    void Remove();

}
