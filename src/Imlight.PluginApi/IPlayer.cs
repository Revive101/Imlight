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
 * Last Updated: 10/23/2026
 */

using System.Collections.Generic;
using System.Numerics;

namespace Imlight.PluginApi;

/// <summary>
/// A player in the game.
/// </summary>
/// <remarks>
/// An IPlayer belongs to one game session, and a player gets a new session on every zone change.
/// Store <see cref="CharacterId"/> rather than the IPlayer itself.
/// </remarks>
public interface IPlayer {

    /// <summary>
    /// The wizard's character ID.
    /// </summary>
    ulong CharacterId { get; }

    /// <summary>
    /// The wizard's display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The zone the wizard is in, or null while the wizard is between zones.
    /// </summary>
    IZone? Zone { get; }

    /// <summary>
    /// The wizard's position in their zone.
    /// </summary>
    Vector3 Location { get; }

    /// <summary>
    /// The direction the wizard is facing, in radians.
    /// </summary>
    float Orientation { get; }

    /// <summary>
    /// The wizard's level.
    /// </summary>
    int Level { get; }

    /// <summary>
    /// The wizard's total experience points.
    /// </summary>
    int Experience { get; }

    /// <summary>
    /// The gold the wizard is carrying.
    /// </summary>
    int Gold { get; }

    /// <summary>
    /// The most gold the wizard can carry.
    /// </summary>
    int MaxGold { get; }

    /// <summary>
    /// The wizard's current health.
    /// </summary>
    int Health { get; }

    /// <summary>
    /// The wizard's maximum health.
    /// </summary>
    int MaxHealth { get; }

    /// <summary>
    /// The wizard's current mana.
    /// </summary>
    int Mana { get; }

    /// <summary>
    /// The wizard's maximum mana.
    /// </summary>
    int MaxMana { get; }

    /// <summary>
    /// The wizard's current pet energy.
    /// </summary>
    int Energy { get; }

    /// <summary>
    /// The wizard's maximum pet energy.
    /// </summary>
    int MaxEnergy { get; }

    /// <summary>
    /// The wizard's unspent training points.
    /// </summary>
    int TrainingPoints { get; }

    /// <summary>
    /// The wizard's cantrip level.
    /// </summary>
    int CantripLevel { get; }

    /// <summary>
    /// The potion charge the wizard has, in potions.
    /// </summary>
    float PotionCharge { get; }

    /// <summary>
    /// The most potion charge the wizard can hold.
    /// </summary>
    float PotionMax { get; }

    /// <summary>
    /// A snapshot of the items in the wizard's backpack.
    /// </summary>
    IReadOnlyList<PlayerItem> Inventory { get; }

    /// <summary>
    /// A snapshot of the items the wizard has equipped.
    /// </summary>
    IReadOnlyList<PlayerItem> Equipment { get; }

    /// <summary>
    /// Whether the wizard has at least one copy of the item, in their backpack or equipped.
    /// </summary>
    bool HasItem(ulong templateId);

    /// <summary>
    /// Shows a server message to the player. Safe to call from any thread.
    /// </summary>
    void SendMessage(string message);

    // Every setter below saves the change and updates the player's client.

    /// <summary>
    /// Sets the wizard's level, which resets their stats for that level and heals them to full.
    /// </summary>
    /// <remarks>
    /// Applied by the player's session after this returns, so <see cref="Level"/> may not change right away.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">The level is below 1 or above the server's max level.</exception>
    void SetLevel(int level);

    /// <summary>
    /// Gives the wizard experience points, levelling them up if they reach the next level.
    /// </summary>
    /// <remarks>
    /// Applied by the player's session after this returns, so <see cref="Experience"/> may not change right away.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">The amount is not positive.</exception>
    void AddExperience(int amount);

    /// <summary>
    /// Gives the wizard gold, up to <see cref="MaxGold"/>.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The amount is negative.</exception>
    void AddGold(int amount);

    /// <summary>
    /// Takes gold from the wizard. Takes nothing and returns false if they have less than <paramref name="amount"/>.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The amount is negative.</exception>
    bool RemoveGold(int amount);

    /// <summary>
    /// Sets the most gold the wizard can carry. Gold they already carry is kept even above the new max.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The max is negative.</exception>
    void SetMaxGold(int maxGold);

    /// <summary>
    /// Sets the wizard's health, clamped between 0 and <see cref="MaxHealth"/>.
    /// </summary>
    void SetHealth(int health);

    /// <summary>
    /// Sets the wizard's maximum health. It resets to the level's value when the wizard levels.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The max is not positive.</exception>
    void SetMaxHealth(int maxHealth);

    /// <summary>
    /// Sets the wizard's mana, clamped between 0 and <see cref="MaxMana"/>.
    /// </summary>
    void SetMana(int mana);

    /// <summary>
    /// Sets the wizard's maximum mana. It resets to the level's value when the wizard levels.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The max is negative.</exception>
    void SetMaxMana(int maxMana);

    /// <summary>
    /// Sets the wizard's pet energy, clamped between 0 and <see cref="MaxEnergy"/>.
    /// </summary>
    void SetEnergy(int energy);

    /// <summary>
    /// Sets the wizard's unspent training points.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The amount is negative.</exception>
    void SetTrainingPoints(int trainingPoints);

    /// <summary>
    /// Sets the wizard's cantrip level and resets their cantrip XP.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The level is negative or above the max cantrip level.</exception>
    void SetCantripLevel(int level);

    /// <summary>
    /// Sets the wizard's potion charge and the most they can hold. The charge is clamped between 0 and the max.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The max is negative.</exception>
    void SetPotions(float charge, float max);

    /// <summary>
    /// Gives the wizard a new copy of the item, the same way loot is given: into the backpack, or into the bank
    /// when the backpack is full (jewels may go to the shared bank). If there is no room anywhere, the item is lost
    /// and the player is told.
    /// </summary>
    /// <remarks>
    /// Applied by the player's session after this returns, so <see cref="Inventory"/> may not change right away.
    /// </remarks>
    /// <exception cref="System.ArgumentException">The template does not exist or cannot be given as an item.</exception>
    void GiveItem(ulong templateId);

    /// <summary>
    /// Removes an item from the wizard's backpack. Nothing happens if it is not there, and equipped items are not removed.
    /// </summary>
    /// <param name="itemId">The item's <see cref="PlayerItem.ItemId"/>, not its template ID.</param>
    /// <remarks>
    /// Applied by the player's session after this returns, so <see cref="Inventory"/> may not change right away.
    /// </remarks>
    void RemoveItem(ulong itemId);

    // Moving the player goes through the same path as the game's own teleports, so it fires
    // OnPlayerTeleport or OnPlayerZoneChange with ZoneChangeCause.Plugin, and a handler can cancel it.
    // Calls made while the player is already changing zones are ignored.

    /// <summary>
    /// Teleports the wizard within their zone, keeping the direction they face.
    /// </summary>
    /// <remarks>
    /// Applied by the player's session after this returns, so <see cref="Location"/> may not change right away.
    /// </remarks>
    void SetLocation(Vector3 location);

    /// <summary>
    /// Teleports the wizard within their zone and turns them to face <paramref name="orientation"/>, in radians.
    /// </summary>
    /// <remarks>
    /// Applied by the player's session after this returns, so <see cref="Location"/> may not change right away.
    /// </remarks>
    void SetLocation(Vector3 location, float orientation);

    /// <summary>
    /// Turns the wizard to face <paramref name="orientation"/>, in radians, without moving them.
    /// </summary>
    /// <remarks>
    /// Applied by the player's session after this returns, so <see cref="Orientation"/> may not change right away.
    /// </remarks>
    void SetOrientation(float orientation);

    /// <summary>
    /// Sends the wizard to the start of another zone. Sending them to their own zone teleports them to its start.
    /// </summary>
    /// <param name="zone">The zone's internal name, e.g. <c>WizardCity/WC_Hub</c>.</param>
    /// <remarks>
    /// This IPlayer stops being the wizard's once they arrive; get the new one from the zone events.
    /// </remarks>
    /// <exception cref="System.ArgumentException">The zone does not exist.</exception>
    void TransferZone(string zone);

    /// <summary>
    /// Sends the wizard to <paramref name="location"/> in another zone, facing <paramref name="orientation"/>,
    /// in radians.
    /// </summary>
    /// <param name="zone">The zone's internal name, e.g. <c>WizardCity/WC_Hub</c>.</param>
    /// <param name="location">Where to put the wizard in the zone.</param>
    /// <param name="orientation">The direction the wizard faces when they arrive.</param>
    /// <remarks>
    /// This IPlayer stops being the wizard's once they arrive; get the new one from the zone events.
    /// </remarks>
    /// <exception cref="System.ArgumentException">The zone does not exist.</exception>
    void TransferZone(string zone, Vector3 location, float orientation);

}
