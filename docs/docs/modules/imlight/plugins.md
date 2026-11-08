# Plugins

Plugins change how an Imlight server behaves without rebuilding Imlight. Drop a compiled plugin DLL into the `plugins` folder next to the server binary, and the server loads it on boot.

Plugins are event based: the server fires **events** as things happen, and plugins **subscribe** to them to react or to cancel the action. If you have ever developed plugins for Minecraft (Bukkit/Spigot, etc.) you will find this familiar.

## Keep in mind that this is in experimental alpha stage!
Methods and events may change without notice. Things may break or be removed. The Wizard101 client also can be very finicky in what it expects, so things on the client may not update to what the server sees.

## Writing a Plugin

A plugin is a .NET class library that references only `Imlight.PluginApi`. See [`ExampleImlightPlugin`](https://github.com/phazejeff/ExampleImlightPlugin) for a complete project.

```xml
<ItemGroup>
  <!-- The server provides Imlight.PluginApi at runtime; don't ship a copy. -->
  <ProjectReference Include="path/to/Imlight.PluginApi.csproj">
    <Private>false</Private>
  </ProjectReference>
</ItemGroup>
```

::: warning A plugin must be a single DLL
The server does not load a plugin's own dependencies. Anything a plugin needs beyond `Imlight.PluginApi` and the .NET runtime has to be compiled into the plugin DLL itself.
:::

Every non-abstract `ImlightPlugin` subclass in the DLL is created and enabled. Each one needs a public parameterless constructor:

```csharp
public sealed class GreeterPlugin : ImlightPlugin {

    public override string Name => "Greeter";

    public override string Version => "1.0.0";

    public override void OnEnable()
        => Events.Subscribe<OnPlayerZoneEnter>(e => e.Player.SendMessage($"Welcome to {e.Zone.Name}!"));

}
```

`OnEnable` runs at boot, before the servers accept connections. `OnDisable` runs when the process exits, in the reverse of the order plugins were enabled.

If `OnEnable` throws, the plugin is logged and skipped: its event handlers and commands are removed, its scheduled tasks cancelled and its NPCs removed, and `OnDisable` never runs.

## Events

| Event | Cancellable | Fired |
| --- | --- | --- |
| `OnPlayerLogin` | Yes | When a player's client attaches from login or after a dropped connection, before they are placed in a zone. `Player.Zone` is null. Cancelling rejects the attach and closes the session. Not fired for zone changes. |
| `OnPlayerQuit` | No | When a player's session ends (logout, disconnect or kick), before they are removed from their zone. Not fired for zone changes. A player who reconnects after a dropped connection may log in again before the old session's quit fires. |
| `OnPlayerZoneChange` | Yes | When a player is about to leave for a different zone, before the destination loads. `Player.Zone` is the zone they are leaving and `Cause` says what sent them (door, recall, dorm, Spiral Door, dungeon, defeat, cantrip, command, plugin or other). For recalls, dorm trips and dungeons it fires before the teleport effects or recall cooldown. Cancelling keeps them where they are. Teleports within a zone fire `OnPlayerTeleport` instead. |
| `OnPlayerZoneChangeFailed` | No | When a zone change that fired `OnPlayerZoneChange` doesn't happen: the destination failed to load, the client declined, or the client has to patch first. A client that declined or patched may still retry, and then `OnPlayerZoneLeave` fires without another `OnPlayerZoneChange`. |
| `OnPlayerZoneLeave` | No | When a player has agreed to a zone change and is about to be removed from their zone, or when their session ends, right after `OnPlayerQuit`. `Zone.Players` still includes them. |
| `OnPlayerZoneEnter` | No | When a player is added to a zone: on first login to a wizard, and after every zone change. |
| `OnPlayerTeleport` | Yes | When a player is about to be moved to another spot in the zone they are in, for example pressing Go Home in the hub. Has the same `Cause` as `OnPlayerZoneChange`. Cancelling keeps them where they are. |
| `OnPlayerDungeonCountdownStart` | Yes | When a player steps onto a dungeon sigil, before the countdown starts and before they are dismounted. Cancelling leaves them on the sigil with no countdown. |
| `OnPlayerDungeonCountdownCancel` | No | When a dungeon countdown ends without the player entering: they stepped off, or a handler cancelled the dungeon's `OnPlayerZoneChange`. |
| `OnPlayerRegenerate` | Yes | When the zone's out-of-combat regeneration is about to heal a player. `Amount` can be changed; it is capped at max health. Cancelling skips the heal. Not fired at full health. |
| `OnPlayerZoneEvent` | Yes | When a player posts a named event to their zone (from the client's scripting, a dialog entry, or finishing loading in), before triggers and quest goals see it. Cancelling drops it. `Client` events are entirely client-controlled. |
| `OnPlayerRealmTransfer` | Yes | When a player asks to move to another realm, before it is contacted. Cancelling keeps them here. If it goes ahead, `OnPlayerQuit` fires as the session ends. |
| `OnPlayerLevelUp` | Yes | When a player is about to go up a level, from experience or `SetLevel`. `Player.Level` is still the old level. Cancelling keeps them at it; experience that caused it is granted only up to one point short of the next level. |
| `OnPlayerChat` | Yes | When a player sends zone chat, before it is logged and broadcast. Cancelling drops the message. Not fired for muted players, chat commands, whispers, group chat or quick chat. |
| `OnPlayerWhisper` | Yes | When a player whispers to an online player, before it is delivered. Cancelling drops the message. Not fired for muted players, blocked or offline targets, quick chat whispers or group chat. |
| `OnPlayerKnockout` | No | When a player's health reaches zero in a duel, right after the damage that caused it. They can still be revived; if not, `OnPlayerDeath` fires when the duel ends. `Source` is who knocked them out. |
| `OnPlayerDeath` | Yes | When a duel ends with a player still knocked out, before they are sent to the hub, whether their team won or lost. Cancelling leaves them standing where the duel was at zero health, so also call `Player.SetHealth`. `Source` is who last knocked them out. Not fired for a knocked-out player who disconnects or logs out first. |
| `OnPlayerCantripCast` | Yes | When a player casts a cantrip, before energy is spent. For a ritual, fired when the cast starts, not when the target is chosen. Cancelling stops the cast without spending energy or starting the cooldown. |
| `OnCreatureSpawn` | No | When a creature is added to its zone, including a minion summoned in a duel. `Zone.Creatures` already includes it. |
| `OnCreatureDespawn` | No | When a creature has left its zone for any reason, including after it dies and when the zone unloads. Fires once for every creature that fired `OnCreatureSpawn`. Calls on the creature do nothing by this point. |
| `OnCreatureDeath` | No | When a defeated creature is removed from its duel at the end of a round, or a creature in a duel is despawned. `OnCreatureDespawn` follows. `Source` is who landed the killing blow, and is null if it was despawned. Not fired for creatures removed alive, such as the winners of a duel. |
| `OnCreatureLoot` | Yes | When a defeated creature's loot has been rolled for a duel winner, before any of it is given, once per creature for each winner. Fires for every creature, with empty loot if it has no drop table or nothing dropped. Handlers can set `Gold`, `Experience`, `TrainingPoints` and `GrantsPotionSlot`, and edit the `Items` list of items, reagents, spells and treasure cards, as in `e.Items.Add(new LootItem(templateId, 3))`. The player gets what is left after every handler runs, and can end up with less, such as when a reagent stack is full. The creature is gone by now, so it comes as `CreatureTemplateId` and `CreatureName`. Cancelling gives nothing from that creature and shows no loot popup; the experience earned from pips is unaffected. |
| `OnZoneLoad` | No | When a zone, including each instance, has finished loading. The place to spawn [NPCs](#npcs) that should always be in a zone. |
| `OnDuelStart` | No | When a duel has been created with its starting participants, before the first round. `Zone.Duels` already includes it. |
| `OnDuelRoundStart` | No | When a new round begins, before its planning phase. `Duel.Round` is already the new round. Creatures may already be choosing their moves. |
| `OnDuelEnd` | No | Once when a duel is decided, before rewards and before `OnPlayerDeath`. `Winners` holds the players on the winning side, and is empty if the creatures won or a PvP match drew. `Duel.Players` and `Duel.Creatures` still hold who was left. |
| `OnDuelistJoin` | No | When a player or creature joins a duel already running, including a summoned minion. `Source` is the summoner, and is null for anyone not summoned. Not fired for starting participants, who are in `OnDuelStart`'s `Duel.Players` and `Duel.Creatures`. |
| `OnPlayerDuelLeave` | No | When a player leaves a duel before it ends, by fleeing, forfeiting, disconnecting or logging out. `Duel.Players` no longer includes them. |
| `OnDuelistDamage` | No | When a player or creature loses health in a duel, as the round resolves and before the client plays it. `Damage` is the health lost and `Duelist.Health` is already reduced. `Source` is who dealt it; for an over-time effect that is whoever cast it. Not fired for hits fully absorbed by wards or resistances. |
| `OnDuelistSpellCast` | Yes | When a player's, creature's or minion's queued spell is about to be cast in a duel, before the fizzle roll, so it fires for spells that then fizzle. `Target` is who it was aimed at, which is the caster for a spell with no chosen target, such as one that hits a whole team. Cancelling turns the cast into a pass, and a player spends no pips, mana or card. Not fired for passes, stuns or dead targets. |
| `OnDuelistHeal` | No | When a player or creature gains health in a duel, from a spell or a heal over time, as the round resolves and before the client plays it. `Healing` is the health gained and `Duelist.Health` is already raised. `Source` is who did the healing; for a heal over time that is whoever cast it. Not fired for heals on someone at full health. |
| `OnDuelistSpellFizzle` | No | When a spell that passed `OnDuelistSpellCast` fails to go off. `WasDispelled` is true if a dispel on the caster stopped it and false if it missed its accuracy roll. `Target` is as in `OnDuelistSpellCast`. |
| `OnDuelistPass` | No | When a turn goes by without a spell. `Reason` says whether they `Passed`, were `Stunned`, lost their target (`TargetGone`) or had the cast `Cancelled` by a plugin. Not fired for a spell that resolves with nothing worth casting, though the client plays that as a pass. |
| `OnDuelistStun` | Yes | When a duelist is about to be stunned, after any stun block and stun resistance have failed to stop it. `Rounds` is how many turns it takes and `Source` is who cast it. Cancelling treats the target as immune, so they get no stun block either. Not fired for bosses. |
| `OnDuelistMindControl` | Yes | When a duelist is about to be mind controlled. `Turns` is how long it lasts and `Source` is who cast it. Cancelling leaves them on their own team. Not fired for bosses or targets already on the caster's team. |
| `OnDuelistSpellCritical` | No | When a spell lands a critical, once per cast, after its effects have been applied and its cost paid. `Multiplier` is the critical's multiplier. `Target` is as in `OnDuelistSpellCast`. |
| `OnDuelistReshuffle` | No | After a spell reshuffles a duelist's deck. `Source` is who cast it. |

Handlers are matched on the event's exact type, so subscribing to `ImlightEvent` or another base type receives nothing. Handlers for the same event run in the order they subscribed.

::: danger Handlers run on the actor that fired the event
Events are delivered synchronously, on the thread of the actor that raised them. This lets a handler cancel an action before it happens, but a slow handler stalls that player's session. Return quickly, and never block.
:::

A handler that throws is logged and skipped. An `IPlayer` belongs to a single game session, and every zone change creates a new one, so store `CharacterId` rather than the `IPlayer`.

## Players

An `IPlayer` exposes the wizard's stats as read-only properties: `Level`, `Experience`, `Gold`, `MaxGold`, `Health`, `MaxHealth`, `Mana`, `MaxMana`, `Energy`, `MaxEnergy`, `TrainingPoints`, `CantripLevel`, `PotionCharge` and `PotionMax`. Change them through the matching methods, which save the change and update the player's client:

```csharp
Commands.Register("buyheal", c => {
    if (!c.Player.RemoveGold(100)) {
        c.Reply("Healing costs 100 gold.");
        return;
    }
    c.Player.SetHealth(c.Player.MaxHealth);
});
```

| Method | Behaviour |
| --- | --- |
| `SetLevel(level)` | Resets stats for the new level and heals to full. Throws outside 1 to the server's max level. |
| `AddExperience(amount)` | Levels the wizard up if they reach the next level. Throws if `amount` is not positive. |
| `AddGold(amount)` | Stops at `MaxGold`. |
| `RemoveGold(amount)` | Returns false and takes nothing if the wizard has less than `amount`. |
| `SetMaxGold(max)` | Gold already carried above the new max is kept. |
| `SetHealth(health)`, `SetMana(mana)`, `SetEnergy(energy)` | Clamped between 0 and the max. |
| `SetMaxHealth(max)`, `SetMaxMana(max)` | Reset to the level's values when the wizard levels. |
| `SetTrainingPoints(points)` | Sets unspent training points. |
| `SetCantripLevel(level)` | Resets cantrip XP. Throws above the max cantrip level. |
| `SetPotions(charge, max)` | `charge` is clamped between 0 and `max`. |
| `Learn(spellTemplateId)` | Teaches the spell. Returns false if the wizard already knows it. Throws `ArgumentException` if the template does not exist or is not a spell. |

Setters that don't clamp throw `ArgumentOutOfRangeException` for negative values, and `SetMaxHealth` also throws for 0. `SetLevel` and `AddExperience` are applied by the player's session after they return, so `Level` and `Experience` may still show the old value straight afterwards. Every other setter takes effect immediately. Call them from an event or command handler, as the server's own commands do, or from a [scheduled task](#scheduler).

### Inventory

`Inventory` lists the items in the wizard's backpack and `Equipment` the items they wear, each as a `PlayerItem` with its `ItemId`, `TemplateId` and `Name`. Both are snapshots that do not change after you read them. `HasItem(templateId)` checks both.

```csharp
Commands.Register("trade", c => {
    var ticket = c.Player.Inventory.FirstOrDefault(item => item.TemplateId == TicketTemplateId);
    if (ticket is null) {
        c.Reply("You need a ticket.");
        return;
    }
    c.Player.RemoveItem(ticket.ItemId);
    c.Player.GiveItem(PrizeTemplateId);
});
```

| Method | Behaviour |
| --- | --- |
| `GiveItem(templateId)` | Gives a new copy of the item the way loot is given: into the backpack, or the bank when the backpack is full (jewels may go to the shared bank). If there is no room anywhere, the item is lost and the player is told. Throws `ArgumentException` if the template does not exist or is not an item. |
| `RemoveItem(itemId)` | Removes that exact item from the backpack. Takes an `ItemId`, not a template ID. Does nothing if the item is not there or is equipped. |

Both are applied by the player's session after they return, like `SetLevel`, so `Inventory` may still show the old items straight afterwards.

### Messages

`SendMessage(message)` shows a server message in the player's chat. `ShowMessageBox(title, text)` shows a box with the title above the text that the player has to close; line breaks in the text are kept. Both are safe to call from any thread.

### Moving Players

| Method | Behaviour |
| --- | --- |
| `SetLocation(location)` | Teleports the wizard within their zone, keeping the direction they face. |
| `SetLocation(location, orientation)` | Teleports the wizard within their zone and turns them to face `orientation`, in radians. |
| `SetOrientation(orientation)` | Turns the wizard without moving them. |
| `TransferZone(zone)` | Sends the wizard to the start of another zone, by internal name such as `WizardCity/WC_Hub`. Sending them to their own zone teleports them to its start. |
| `TransferZone(zone, location, orientation)` | Sends the wizard to a spot in another zone. |

These go through the same path as the game's own teleports, so they fire `OnPlayerTeleport` or `OnPlayerZoneChange` with the `Plugin` cause, and a handler can cancel them. They are applied by the player's session after they return, like `SetLevel`, and calls made while the player is already changing zones are ignored. `TransferZone` throws `ArgumentException` if the zone does not exist. Once the wizard arrives in another zone, the old `IPlayer` is no longer theirs; get the new one from `OnPlayerZoneEnter` or `Server.GetPlayer`.

```csharp
Commands.Register("hub", c => c.Player.TransferZone("WizardCity/WC_Hub"));
```

## Zones

`IPlayer.Zone` is the `IZone` the player is in, or null while they are between zones. A zone has its `Name`, whether it `IsInstance`, and the `Players`, [`Creatures`](#creatures), [`Duels`](#duels) and plugin [`Npcs`](#npcs) in it:

```csharp
Events.Subscribe<OnPlayerZoneEnter>(e => {
    foreach (var other in e.Zone.Players) {
        other.SendMessage($"{e.Player.Name} joined. {e.Zone.Players.Count} wizards here now.");
    }
});
```

`Players` is a snapshot, safe to read from any handler; it does not change after you read it, so read it again for fresh data. The same goes for `Creatures`, `Duels` and `Npcs`. By the time `OnPlayerZoneEnter` fires, the entering player is already in it. The `IPlayer`s in it are the same objects events hand you, so `Players.Contains(e.Player)` works. Each dungeon instance is its own `IZone`. An `IZone` lasts only while its zone is loaded, and instances unload once they sit empty, so store `Name` rather than the `IZone`.

## Creatures

An `ICreature` is a combat creature: a mob from a spawn path, one spawned by a trigger or quest, or a minion summoned in a duel. Friendly NPCs that can't be fought are not creatures. `IZone.Creatures` lists the ones in a zone, and `Server.GetCreature` finds one by `GlobalId`.

A creature has its `GlobalId`, `TemplateId` (shared by every spawn of the same kind), `Zone`, whether it `IsBoss` or `IsMinion`, whether it is `InCombat`, and whether it `IsDead`, along with what every [duelist](#duels) has. `Spells` lists the template ID of each spell in its deck, once however many copies it has. Its AI is described by four values:

| Property | Meaning |
| --- | --- |
| `Intelligence` | The chance, from 0 to 1, that it picks the strongest spell in its hand each round. |
| `Aggressiveness` | The chance, from 0 to 1, that it attacks each round rather than preparing. |
| `Selfishness` | The chance, from 0 to 1, that it prepares on itself rather than on a teammate. |
| `Proximity` | How close a player must come for it to start a duel. |

```csharp
Events.Subscribe<OnCreatureSpawn>(e => {
    if (e.Creature.IsBoss) {
        e.Creature.SetMaxHealth(e.Creature.MaxHealth * 2);
        e.Creature.SetHealth(e.Creature.MaxHealth * 2);
        e.Creature.SetIntelligence(1);
    }
});
```

| Method | Behaviour |
| --- | --- |
| `SetHealth(health)` | Clamped between 1 and `MaxHealth`. Use `Despawn` to kill it. |
| `SetMaxHealth(max)` | Lowers its health to the new max if it was above it. Throws if `max` is not positive. |
| `SetIntelligence`, `SetAggressiveness`, `SetSelfishness` | Throw if the chance is not between 0 and 1. In a duel they apply from the creature's next round. |
| `SetProximity(proximity)` | Throws if negative. A player already inside the new range is noticed the next time they move. |
| `StopMoving()` | Stops it walking its path once it reaches the point it is walking to. Creatures that don't walk a path, such as minions, ignore it. |
| `ResumeMoving()` | Lets it walk its path again. A creature in a duel stays put regardless. |
| `Despawn()` | Removes it from the zone. Its spawn point can spawn a new one later, except in an instance. A creature in a duel is defeated instead at the end of the round, as if a player had killed it, so the winners still get their rewards. |

Every setter is applied by the creature's own actor after it returns, so the matching property may not change straight away, and in a duel players see a health change at the start of the next round. Calls on a creature that has died or despawned do nothing.

An `ICreature` lives until the creature dies or despawns. A respawn is a new creature with a new `GlobalId`, so store `TemplateId` if you want to recognise the kind of creature. A creature at 0 health in a duel is not `IsDead` until the end of the round, when the duel removes it.

## Duels

`IZone.Duels` lists the duels running in a zone, and `IPlayer.Duel` and `ICreature.Duel` give the duel a participant is in, or null. A duel has its `Id`, `Zone`, whether it `IsPvP`, the current `Round`, whether it `HasEnded`, and the `Players` and `Creatures` in it:

```csharp
Commands.Register("duelinfo", c => {
    if (c.Player.Duel is not { } duel) {
        c.Reply("You are not in a duel.");
        return;
    }
    c.Reply($"Round {duel.Round}: {duel.Players.Count} wizards against {duel.Creatures.Count} creatures.");
});
```

`Players` and `Creatures` are snapshots, like `IZone.Players`. `Players` keeps knocked-out wizards, who can still be revived, and drops those who flee or forfeit. `Creatures` holds summoned minions too, so in a PvE duel it is not only the enemies, and it drops a creature once it is dead. Both are empty once `HasEnded` is true. An `IDuel` covers one fight: the next fight on the same sigil is a new `IDuel` with the same `Id`.

`IPlayer` and `ICreature` are both an `IDuelist`, with the `Name`, `Duel`, `Location`, `Orientation`, `Level`, `School`, `Health` and `MaxHealth` they share. The `OnDuelist` events hand you one, so pattern match to reach the rest:

```csharp
Events.Subscribe<OnDuelistSpellCast>(e => {
    if (e.Duelist is ICreature { IsBoss: true } && e.SpellName == BannedSpell) {
        e.IsCancelled = true;
    }
});
```

## Server

`Server` gives you the whole server: the `Players` online, the `Zones` loaded, `GetPlayer` to look a player up by character ID or name, and `Broadcast` to message every player in a zone:

```csharp
Commands.Register("who", c => {
    var names = Server.Players.Select(x => x.Name);
    c.Reply($"{Server.Players.Count} online: {string.Join(", ", names)}");
});
```

`Players` and `Zones` are snapshots, like `IZone.Players`, so read them again for fresh data. `Players` only holds players who are in a zone, so a player mid zone change or still logging in is missing from it and `GetPlayer` returns null for them. `GetPlayer(name)` matches the display name case-insensitively.

`GetZone` finds a shared zone by internal name, and `GetCreature` finds a spawned creature by `GlobalId`. Instances are not included in `GetZone`, as several can share a name.

Most of the API gives items and spells by template ID. `GetItem` and `GetSpell` turn an ID into its details, such as a name, school or pip cost:

```csharp
Events.Subscribe<OnCreatureLoot>(evt => {
    foreach (var loot in evt.Items) {
        var item = Server.GetItem(loot.TemplateId);
        if (item is not null) {
            evt.Player.SendMessage($"You found {item.Name}!");
        }
    }
});
```

To go the other way, `GetSpell(name)` takes a spell's internal name, matched exactly, and `FindItems` takes an item's internal name, matched case-insensitively. `FindItems` returns a list, as several items can share a name. English item names cannot be searched. All of these return null or an empty list for something that does not exist.

## NPCs

`Npcs.Spawn` puts an NPC in a zone, built from any game object template that can be shown, such as a placed NPC's. Give it an interaction to make players able to talk to it, and a wizbang to mark it over its head:

```csharp
Events.Subscribe<OnZoneLoad>(e => {
    if (e.Zone.Name != "WizardCity/WC_Hub") {
        return;
    }
    var npc = Npcs.Spawn(e.Zone, GuideTemplateId, new Vector3(100, 200, 0), name: "Town Guide");
    npc.SetInteraction(i => i.Say($"Welcome, {i.Player.Name}!"));
    npc.SetWizBang(NpcWizBang.StartQuest);
});
```

NPCs never fight or move, and are never saved: one lives until it is `Remove`d or its zone unloads, so spawn it again in `OnZoneLoad`, which fires each time a zone finishes loading. A spawn made while the zone is still loading appears once it has loaded. `Npcs.Spawned` lists your plugin's NPCs, and `IZone.Npcs` every plugin's NPCs in that zone. Every `INpc` call is safe from any thread.

| Method | Behaviour |
| --- | --- |
| `SetInteraction(handler)` | Runs the handler when a player interacts with the NPC, or makes it not interactable when null. The handler runs on the NPC's own actor. A player already near the NPC sees the change the next time they walk up to it. |
| `SetWizBang(wizBang)` | Sets the icon over its head. It shows only while the NPC is interactable, and nearby players see a change within a second. |
| `SetState(state)` | Plays an animation state from its template, such as `Idle`. Players who arrive later see the same state. `Off` hides the NPC. Throws if `state` is empty. |
| `Remove()` | Removes it from its zone. |

An `INpc` also has its `GlobalId`, `TemplateId`, `Name` (the one it was given, or its template's), `Zone`, `Location`, `Orientation`, current `WizBang`, whether it `IsInteractable`, and whether it `IsRemoved`, which turns true once it is removed or its zone unloads. Calls on a removed NPC do nothing. `Npcs.RemoveAll()` removes every NPC your plugin spawned.

`Spawn` throws `ArgumentException` if the template is not a game object that can be shown, and `InvalidOperationException` if the zone has already unloaded.

In an interaction, `Say` shows the player a message box headed with the NPC's name. It is a message box rather than the game's dialog window, because the client only shows dialog text from its own string tables. For the same reason, the interaction option is always labelled the client's generic way.

::: warning A custom name does not change the nameplate
The `name` passed to `Spawn` shows when players interact with the NPC, but the client keeps the template's name over its head, even for a regular NPC template. To show a different name there, use a template with that name.
:::

## Commands

Plugins can add their own chat commands through `Commands`, in `OnEnable`:

```csharp
public override void OnEnable() {
    Commands.Register("roll", c => c.Reply($"{c.Player.Name} rolled {Random.Shared.Next(1, 101)}"));

    // Restricted to HallMonitor and above, also reachable as "announce".
    Commands.Register("broadcast", c => c.Reply($"Would broadcast: {c.RawArgs}"),
                      CommandPermission.HallMonitor, "announce");
}
```

Players use a command by typing `.` and its name in chat, such as `.roll`. The handler receives a `CommandInvocation` with the `Player`, the `Name` they typed in lower case, and the `Args` after it (`RawArgs` joins them back into one string). Names and aliases are single words, matched case-insensitively.

::: warning Players without an account level can't use commands
Chat is only treated as a command for accounts above `CommandPermission.None`. For anyone else, `.roll` is sent to the zone as ordinary chat and fires `OnPlayerChat`, even if the command is registered with `CommandPermission.None`.
:::

`Register` throws `ArgumentException` if a name is empty or not a single word, is listed twice, or is already a server command or another plugin's command, and registers nothing in that case. If `OnEnable` throws, every command the plugin had registered is removed. `CommandPermission.Developer` commands are administrator-only outside debug builds, the same as server commands. A handler that throws is logged and the player is told the command failed.

::: danger Commands run on one shared actor
Every command, the server's and every plugin's, runs on a single dispatcher actor. A slow command handler stalls commands for every player on the server, so return quickly and never block.
:::

## Scheduler

`Scheduler` runs code later or on a repeat, so a plugin never needs its own timers or threads. `RunAfter` runs once after a delay, and `RunEvery` repeats until cancelled, with its first run one interval from now:

```csharp
public override void OnEnable() {
    Scheduler.RunEvery(TimeSpan.FromMinutes(10), () => Server.Broadcast("Double gold is on this weekend!"));

    Commands.Register("countdown", c => {
        var characterId = c.Player.CharacterId;
        var remaining = 3;
        IScheduledTask countdown = null!;
        countdown = Scheduler.RunEvery(TimeSpan.FromSeconds(1), () => {
            Server.GetPlayer(characterId)?.SendMessage(remaining > 0 ? $"{remaining}..." : "Go!");
            if (remaining-- == 0) {
                countdown.Cancel();
            }
        });
    });
}
```

Both return an `IScheduledTask`, and its `Cancel` can be called from anywhere, including inside the task. A run that has already started finishes. Each `RunEvery` run starts one interval after the last one finished, so a slow task drifts later rather than piling up. `RunAfter` throws for a negative delay and `RunEvery` for an interval that isn't positive.

Each plugin's tasks run one at a time on that plugin's own actor, so they never overlap each other, and a slow task delays only that plugin's other tasks. They can still run at the same time as the plugin's event and command handlers, so guard any data the two share. A task that throws is logged, and a repeating task keeps repeating.

Every task is cancelled just before `OnDisable`, or if `OnEnable` throws, and anything scheduled after that never runs. Tasks do not survive a restart.

A task can outlive the player or zone it was scheduled for, so store `CharacterId` or the zone's `Name` and look them up when it runs, as the countdown does. `Server.GetPlayer` returns null if the player has left or is between zones.

## Logging

`Logger` writes to the server's log, with each line labelled with the plugin's name:

```csharp
public override void OnEnable() {
    Logger.Information("Ready to greet.");

    Scheduler.RunEvery(TimeSpan.FromMinutes(5), () => {
        try {
            SaveScores();
        }
        catch (IOException ex) {
            Logger.Error("Could not save scores", ex);
        }
    });
}
```

`Debug`, `Information`, `Warning` and `Error` each take a message, and `Error` can also take an exception. The server's `LogLevel` setting decides which are kept, so `Debug` lines are dropped by default. Messages are plain text, so use string interpolation rather than Serilog `{Placeholder}` templates.

Like the rest of the API, `Logger` is not set yet in a plugin's constructor. Use it from `OnEnable` onwards.

## Storing Data

`DataFolder` is the plugin's own folder for configs and anything else it keeps between runs. It is `plugins/<Name>` next to the server binary, created the first time you read it. Keep the plugin's files there rather than anywhere else on disk:

```csharp
public override void OnEnable() {
    var path = Path.Combine(DataFolder, "config.json");
    if (!File.Exists(path)) {
        File.WriteAllText(path, "{}");
    }
}
```

Characters a folder name can't hold are replaced with `_`. A plugin whose `Name` can't be used as a folder name at all, such as an empty one, fails to enable.

## Adding an Event

1. Add a class deriving from `ImlightEvent` to `Imlight.PluginApi/Events`, named `On<Subject><Action>` (e.g. `OnPlayerLevelUp`). Implement `ICancellable` if handlers should be able to stop the action.
2. Fire it where the action happens, and respect cancellation if it applies:

```csharp
var player = PluginPlayer.For(wizard, SessionActor.ActorRef, SessionActor.GetZoneActor());
if (PluginManager.Events.Fire(new OnPlayerChat(player, message)).IsCancelled) {
    return;
}
```

   Always get players through `PluginPlayer.For` rather than `new PluginPlayer(...)`. It returns the same `IPlayer` the zone's `Players` holds, so plugins can compare players across events.

3. Add the event to the [Events](#events) table above.
