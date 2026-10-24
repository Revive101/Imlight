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

namespace Imlight.PluginApi.Events;

/// <summary>
/// Fired when a player says something in zone chat, before it is logged and shown to the zone.
/// Cancelling it drops the message. Group chat, quick chat and chat commands don't fire it, and whispers
/// fire <see cref="OnPlayerWhisper"/> instead.
/// </summary>
public sealed class OnPlayerChat(IPlayer player, string message) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who sent the message.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The message, with unsupported characters already removed.
    /// </summary>
    public string Message { get; } = message;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player has been added to a zone: on first login to a wizard, and after every zone change.
/// </summary>
public sealed class OnPlayerZoneEnter(IPlayer player, IZone zone) : ImlightEvent {

    /// <summary>
    /// The player who entered the zone.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The zone the player entered. <see cref="IZone.Players"/> already includes them.
    /// </summary>
    public IZone Zone { get; } = zone;

}

/// <summary>
/// Fired when a player's client attaches to the game server from login or after a dropped connection,
/// before they are placed in a zone. Zone changes don't fire it.
/// Cancelling it rejects the attach and closes the session.
/// </summary>
public sealed class OnPlayerLogin(IPlayer player) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who is logging in. <see cref="IPlayer.Zone"/> is null, since they are not in a zone yet.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player's session ends (logging out, disconnecting or being kicked), before they are removed
/// from their zone. Zone changes don't fire it. A player who reconnects after a dropped connection may log in
/// again before the old session's quit fires.
/// </summary>
public sealed class OnPlayerQuit(IPlayer player) : ImlightEvent {

    /// <summary>
    /// The player who is quitting.
    /// </summary>
    public IPlayer Player { get; } = player;

}

/// <summary>
/// Fired when a player is about to leave their zone for a different one, before the destination is loaded.
/// For recalls, dorm trips and dungeon sigils it fires before the teleport effects play or the recall cooldown
/// starts. Teleports within the same zone fire <see cref="OnPlayerTeleport"/> instead.
/// Cancelling it keeps the player where they are. Cancelling a <see cref="ZoneChangeCause.Defeat"/> leaves them
/// standing where the duel was, at whatever health they had.
/// </summary>
public sealed class OnPlayerZoneChange(IPlayer player, string destinationZone, ZoneChangeCause cause)
    : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who is changing zones. <see cref="IPlayer.Zone"/> is the zone they are leaving.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The internal name of the zone the player is going to, e.g. <c>WizardCity/WC_Hub</c>.
    /// </summary>
    public string DestinationZone { get; } = destinationZone;

    /// <summary>
    /// What sent the player.
    /// </summary>
    public ZoneChangeCause Cause { get; } = cause;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player has agreed to a zone change and is about to be removed from their zone. Unlike
/// <see cref="OnPlayerZoneChange"/>, the player is definitely leaving. Also fires when their session ends,
/// right after <see cref="OnPlayerQuit"/>.
/// </summary>
public sealed class OnPlayerZoneLeave(IPlayer player, IZone zone) : ImlightEvent {

    /// <summary>
    /// The player who is leaving the zone.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The zone the player is leaving. <see cref="IZone.Players"/> still includes them.
    /// </summary>
    public IZone Zone { get; } = zone;

}

/// <summary>
/// Fired when a zone change the player was about to make doesn't happen, after <see cref="OnPlayerZoneChange"/>
/// fired for it. A client that declined or had to patch may still retry, in which case
/// <see cref="OnPlayerZoneLeave"/> fires without another <see cref="OnPlayerZoneChange"/>.
/// </summary>
public sealed class OnPlayerZoneChangeFailed(IPlayer player, string destinationZone, ZoneChangeFailReason reason)
    : ImlightEvent {

    /// <summary>
    /// The player who stayed where they were.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The internal name of the zone the player was going to.
    /// </summary>
    public string DestinationZone { get; } = destinationZone;

    /// <summary>
    /// Why the change didn't happen.
    /// </summary>
    public ZoneChangeFailReason Reason { get; } = reason;

}

/// <summary>
/// Fired when a player is about to be moved to another spot in the zone they are already in, for example by
/// pressing Go Home while in the hub. Cancelling it keeps the player where they are.
/// </summary>
public sealed class OnPlayerTeleport(IPlayer player, System.Numerics.Vector3 destination, float orientation,
    ZoneChangeCause cause) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who is teleporting. <see cref="IPlayer.Location"/> is where they are now.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// Where the player is going.
    /// </summary>
    public System.Numerics.Vector3 Destination { get; } = destination;

    /// <summary>
    /// The direction the player will face, in radians.
    /// </summary>
    public float Orientation { get; } = orientation;

    /// <summary>
    /// What sent the player.
    /// </summary>
    public ZoneChangeCause Cause { get; } = cause;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player steps onto a dungeon sigil, before its countdown starts and before they are dismounted.
/// Cancelling it leaves the player on the sigil with no countdown.
/// </summary>
public sealed class OnPlayerDungeonCountdownStart(IPlayer player, string destinationZone)
    : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who stepped onto the sigil.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The internal name of the dungeon the sigil leads to.
    /// </summary>
    public string DestinationZone { get; } = destinationZone;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a dungeon sigil's countdown ends without the player entering: they stepped off the sigil, or a
/// handler cancelled the <see cref="OnPlayerZoneChange"/> for the dungeon.
/// </summary>
public sealed class OnPlayerDungeonCountdownCancel(IPlayer player, string destinationZone) : ImlightEvent {

    /// <summary>
    /// The player whose countdown ended.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The internal name of the dungeon the sigil leads to.
    /// </summary>
    public string DestinationZone { get; } = destinationZone;

}

/// <summary>
/// Fired when a zone's out-of-combat regeneration is about to heal a player. Players at full health don't fire it.
/// Cancelling it skips this heal.
/// </summary>
public sealed class OnPlayerRegenerate(IPlayer player, int amount) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player being healed.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The health the player will gain. Handlers can change it; the result is capped at the player's max health,
    /// and zero or less heals nothing.
    /// </summary>
    public int Amount { get; set; } = amount;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player posts a named event to their zone, before the zone's triggers and quest goals see it.
/// Cancelling it drops the event, so nothing in the zone reacts to it.
/// </summary>
public sealed class OnPlayerZoneEvent(IPlayer player, string eventName, ZoneEventSource source)
    : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who posted the event.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The event's name.
    /// </summary>
    public string EventName { get; } = eventName;

    /// <summary>
    /// Where the event came from. <see cref="ZoneEventSource.Client"/> events complete quest goals for every
    /// player in the zone and are entirely client-controlled.
    /// </summary>
    public ZoneEventSource Source { get; } = source;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player asks to move to another realm, before the target realm is contacted.
/// Cancelling it keeps them on this realm. If the transfer goes ahead, <see cref="OnPlayerQuit"/> fires as the
/// session ends.
/// </summary>
public sealed class OnPlayerRealmTransfer(IPlayer player, string realmName) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who is transferring.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The name of the realm they asked for.
    /// </summary>
    public string RealmName { get; } = realmName;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player is about to go up one or more levels, from experience or from <see cref="IPlayer.SetLevel"/>.
/// Cancelling it keeps them at their current level; if experience caused it, they gain only as much
/// experience as fits within that level.
/// </summary>
public sealed class OnPlayerLevelUp(IPlayer player, int newLevel) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who is levelling up. <see cref="IPlayer.Level"/> is still their old level.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The level the player is going to.
    /// </summary>
    public int NewLevel { get; } = newLevel;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player whispers to another online player, before it is delivered.
/// Cancelling it drops the message. Quick chat whispers and group chat don't fire it.
/// </summary>
public sealed class OnPlayerWhisper(IPlayer player, ulong targetCharacterId, string message)
    : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who sent the whisper.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The <see cref="IPlayer.CharacterId"/> of the player being whispered to.
    /// </summary>
    public ulong TargetCharacterId { get; } = targetCharacterId;

    /// <summary>
    /// The message.
    /// </summary>
    public string Message { get; } = message;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player loses health in a duel, as the server resolves the round and before the client plays it.
/// Hits fully absorbed by wards or resistances don't fire it. Fires on the duel's actor, not the player's session.
/// </summary>
public sealed class OnPlayerDamage(IPlayer player, int damage) : ImlightEvent {

    /// <summary>
    /// The player who was damaged. <see cref="IPlayer.Health"/> is already reduced.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The health the player lost, never more than they had.
    /// </summary>
    public int Damage { get; } = damage;

}

/// <summary>
/// Fired when a player's health reaches zero in a duel, right after the <see cref="OnPlayerDamage"/> that caused it.
/// The player stays in the duel and can still be revived; if they aren't by the end, <see cref="OnPlayerDeath"/>
/// fires. Fires on the duel's actor, not the player's session.
/// </summary>
public sealed class OnPlayerKnockout(IPlayer player) : ImlightEvent {

    /// <summary>
    /// The player who was knocked out.
    /// </summary>
    public IPlayer Player { get; } = player;

}

/// <summary>
/// Fired when a duel ends with a player still knocked out, before they are sent to the hub. It fires whether
/// their team won or lost. A knocked-out player who disconnects or logs out before the end doesn't fire it.
/// Cancelling it leaves the player standing where the duel was, still at zero health, so a handler that cancels
/// should also restore their health with <see cref="IPlayer.SetHealth"/>. Fires on the duel's actor, not the
/// player's session.
/// </summary>
public sealed class OnPlayerDeath(IPlayer player) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who died. <see cref="IPlayer.Health"/> is zero.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player's queued spell is about to be cast in a duel, before it is rolled for a fizzle.
/// Passes, and turns lost to a stun or a dead target, don't fire it. Cancelling it turns the cast into a pass,
/// so no pips, mana or card are spent. Fires on the duel's actor, not the player's session.
/// </summary>
public sealed class OnPlayerSpellCast(IPlayer player, uint spellTemplateId, string spellName, string school,
    int pipCost) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who is casting.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The template ID of the spell being cast.
    /// </summary>
    public uint SpellTemplateId { get; } = spellTemplateId;

    /// <summary>
    /// The internal name of the spell's template.
    /// </summary>
    public string SpellName { get; } = spellName;

    /// <summary>
    /// The spell's school, e.g. <c>Fire</c>.
    /// </summary>
    public string School { get; } = school;

    /// <summary>
    /// The spell's rank in pips.
    /// </summary>
    public int PipCost { get; } = pipCost;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player casts a cantrip, before any energy is spent. For a ritual it fires when the cast starts,
/// not when the target is chosen. Cancelling it stops the cast without spending energy or starting the cooldown.
/// </summary>
public sealed class OnPlayerCantripCast(IPlayer player, uint spellTemplateId, string cantripName, int energyCost)
    : ImlightEvent, ICancellable {

    /// <summary>
    /// The player who is casting.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The template ID of the cantrip being cast.
    /// </summary>
    public uint SpellTemplateId { get; } = spellTemplateId;

    /// <summary>
    /// The internal name of the cantrip's template, e.g. <c>CantripsMinorInvisibility</c>.
    /// </summary>
    public string CantripName { get; } = cantripName;

    /// <summary>
    /// The energy the cantrip will cost.
    /// </summary>
    public int EnergyCost { get; } = energyCost;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}
