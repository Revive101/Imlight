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

namespace Imlight.PluginApi.Events;

/// <summary>
/// Fired when a creature has been added to its zone, including a minion summoned in a duel.
/// <see cref="IZone.Creatures"/> already includes it. Fires on the creature's actor.
/// </summary>
public sealed class OnCreatureSpawn(ICreature creature) : ImlightEvent {

    /// <summary>
    /// The creature that spawned.
    /// </summary>
    public ICreature Creature { get; } = creature;

}

/// <summary>
/// Fired when a creature has left its zone for any reason: after it dies, when a plugin or the game despawns it,
/// when a duel it is in ends, or when its zone unloads. Every creature that fired <see cref="OnCreatureSpawn"/>
/// fires this once. Fires on the creature's actor, which has already stopped, so calls on the creature do nothing.
/// </summary>
public sealed class OnCreatureDespawn(ICreature creature) : ImlightEvent {

    /// <summary>
    /// The creature that left. <see cref="ICreature.IsDead"/> is true and <see cref="IZone.Creatures"/> no longer
    /// includes it.
    /// </summary>
    public ICreature Creature { get; } = creature;

}

/// <summary>
/// Fired when a defeated creature is removed from its duel at the end of a round, or a creature in a duel is
/// despawned. <see cref="OnCreatureDespawn"/> follows once it leaves the zone. Creatures that are removed alive
/// don't fire it. Fires on the creature's actor.
/// </summary>
public sealed class OnCreatureDeath(ICreature creature) : ImlightEvent {

    /// <summary>
    /// The creature that died. <see cref="ICreature.IsDead"/> is true and <see cref="ICreature.Health"/> is zero.
    /// </summary>
    public ICreature Creature { get; } = creature;

}

/// <summary>
/// Fired when a defeated creature's loot has been rolled for a player who won a duel, before any of it is given, once
/// per creature for each winner. It fires for every creature, with empty loot if it has no drop table or the roll
/// dropped nothing. Handlers can change the loot, and the player is given whatever is left once they have all run.
/// Cancelling it gives them nothing from that creature and shows no loot popup; their other loot and the experience
/// earned for pips used in the duel are unaffected. The creature itself is gone by now, so it is identified by
/// template. Fires on the player's session, not the duel's actor.
/// </summary>
public sealed class OnCreatureLoot(IPlayer player, ulong creatureTemplateId, string creatureName, int gold,
    int experience, int trainingPoints, bool grantsPotionSlot, List<LootItem> items)
    : ImlightEvent, ICancellable {

    private int _gold = gold;
    private int _experience = experience;
    private int _trainingPoints = trainingPoints;

    /// <summary>
    /// The player receiving the loot.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The template ID of the creature that dropped the loot.
    /// </summary>
    public ulong CreatureTemplateId { get; } = creatureTemplateId;

    /// <summary>
    /// The internal name of the creature's template, or empty if it has none.
    /// </summary>
    public string CreatureName { get; } = creatureName;

    /// <summary>
    /// The gold to give. The player keeps only what fits in their gold pouch.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int Gold {
        get => _gold;
        set {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _gold = value;
        }
    }

    /// <summary>
    /// The experience to give.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int Experience {
        get => _experience;
        set {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _experience = value;
        }
    }

    /// <summary>
    /// The training points to give.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int TrainingPoints {
        get => _trainingPoints;
        set {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _trainingPoints = value;
        }
    }

    /// <summary>
    /// Whether the loot gives the player another potion slot.
    /// </summary>
    public bool GrantsPotionSlot { get; set; } = grantsPotionSlot;

    /// <summary>
    /// The items, reagents, spells and treasure cards to give. Add to it, remove from it or clear it to change the
    /// loot. The player can end up with less, such as when a reagent stack is full or they already know a spell.
    /// </summary>
    public List<LootItem> Items { get; } = items;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}
