# Plugins

Plugins change how an Imlight server behaves without rebuilding Imlight. Drop a compiled plugin DLL into the `plugins` folder next to the server binary, and the server loads it on boot.

Plugins are event based: the server fires **events** as things happen, and plugins **subscribe** to them to react or to cancel the action. If you have ever developed plugins for Minecraft (Bukkit/Spigot, etc.) you will find this familiar.

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
        => Events.Subscribe<PlayerZoneEnterEvent>(e => e.Player.SendMessage($"Welcome to {e.Zone.Name}!"));

}
```

`OnEnable` runs at boot, before the servers accept connections. `OnDisable` runs when the process exits.

## Events

| Event | Cancellable | Fired |
| --- | --- | --- |
| `PlayerLoginEvent` | Yes | When a player's client attaches from login or after a dropped connection, before they are placed in a zone. `Player.Zone` is null. Cancelling rejects the attach and closes the session. Not fired for zone changes. |
| `PlayerQuitEvent` | No | When a player's session ends (logout, disconnect or kick), before they are removed from their zone. Not fired for zone changes. A player who reconnects after a dropped connection may log in again before the old session's quit fires. |
| `PlayerZoneChangeEvent` | Yes | When a player is about to leave for a different zone, before the destination loads. `Player.Zone` is the zone they are leaving. Cancelling keeps them where they are. Not fired for teleports within a zone. |
| `PlayerZoneEnterEvent` | No | When a player is added to a zone: on first login to a wizard, and after every zone change. |
| `PlayerLevelUpEvent` | Yes | When a player is about to go up a level, from experience or `SetLevel`. `Player.Level` is still the old level. Cancelling keeps them at it; experience that caused it is granted only up to one point short of the next level. |
| `PlayerChatEvent` | Yes | When a player sends zone chat, before it is logged and broadcast. Cancelling drops the message. Not fired for muted players, chat commands, whispers, group chat or quick chat. |
| `PlayerWhisperEvent` | Yes | When a player whispers to an online player, before it is delivered. Cancelling drops the message. Not fired for muted players, blocked or offline targets, quick chat whispers or group chat. |

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

Setters that don't clamp throw `ArgumentOutOfRangeException` for negative values, and `SetMaxHealth` also throws for 0. `SetLevel` and `AddExperience` are applied by the player's session after they return, so `Level` and `Experience` may still show the old value straight afterwards. Every other setter takes effect immediately. Call them from an event or command handler, as the server's own commands do.

## Zones

`IPlayer.Zone` is the `IZone` the player is in, or null while they are between zones. A zone has its `Name`, whether it `IsInstance`, and the `Players` in it:

```csharp
Events.Subscribe<PlayerZoneEnterEvent>(e => {
    foreach (var other in e.Zone.Players) {
        other.SendMessage($"{e.Player.Name} joined. {e.Zone.Players.Count} wizards here now.");
    }
});
```

`Players` is a snapshot, safe to read from any handler; it does not change after you read it, so read it again for fresh data. By the time `PlayerZoneEnterEvent` fires, the entering player is already in it. The `IPlayer`s in it are the same objects events hand you, so `Players.Contains(e.Player)` works. Each dungeon instance is its own `IZone`. An `IZone` lasts only while its zone is loaded, and instances unload once they sit empty, so store `Name` rather than the `IZone`.

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

The handler receives a `CommandInvocation` with the `Player`, the `Name` they typed, and the `Args` after it (`RawArgs` joins them back into one string). Names and aliases are single words, matched case-insensitively.

`Register` throws if a name is already a server command or another plugin's command, and registers nothing in that case. If `OnEnable` throws, every command the plugin had registered is removed. `CommandPermission.Developer` commands are administrator-only outside debug builds, the same as server commands. A handler that throws is logged and the player is told the command failed.

## Adding an Event

1. Add a class deriving from `ImlightEvent` to `Imlight.PluginApi/Events`. Implement `ICancellable` if handlers should be able to stop the action.
2. Fire it where the action happens, and respect cancellation if it applies:

```csharp
var player = PluginPlayer.For(wizard, SessionActor.ActorRef, SessionActor.GetZoneActor());
if (PluginManager.Events.Fire(new PlayerChatEvent(player, message)).IsCancelled) {
    return;
}
```

   Always get players through `PluginPlayer.For` rather than `new PluginPlayer(...)`. It returns the same `IPlayer` the zone's `Players` holds, so plugins can compare players across events.

3. Add the event to the [Events](#events) table above.
