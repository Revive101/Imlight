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

namespace Imlight.PluginApi.Npcs;

/// <summary>
/// One player interacting with a plugin's NPC.
/// </summary>
public sealed class NpcInteraction(IPlayer player, INpc npc) {

    /// <summary>
    /// The player who interacted.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The NPC they interacted with.
    /// </summary>
    public INpc Npc { get; } = npc;

    /// <summary>
    /// Shows the player a message box headed with the NPC's name.
    /// </summary>
    public void Say(string text) => Player.ShowMessageBox(Npc.Name, text);

}
