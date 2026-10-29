# Commands

The **Command System** provides a framework for processing administrative and testing commands within the game server.

The system uses reflection to automatically discover and register command protocols, allowing for easy addition of new commands without manual registration.

## Command Structure

Commands follow the format: `[group] [command] [parameters]`

- **Group**: Command category (e.g., "mod", "sb", "debug")
- **Command**: Specific action within the group
- **Parameters**: Optional arguments for the command

## Creating Command Protocols

Command protocols are classes that inherit from `CommandProtocol` and define a group of related commands.

### Basic Protocol Structure

```csharp
internal class CommandModifyProtocol : CommandProtocol {
    
    internal override string Group { get; set; } = "mod";
    
    [Command("levelup")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [Alias("lvlup")]
    private void LevelUpCommand() {
        var newLevel = (byte)(Context.Character.MagicSchoolBehavior.Level + 1);
        
        if (newLevel > MagicLevelsConfig.MaxLevel) {
            InformSenderClient($"Cannot exceed max level ({MagicLevelsConfig.MaxLevel}).");
            return;
        }
        
        var msg = new CHARACTER_103_PROTOCOL.MSG_LEVELUP() { NewLevel = newLevel };
        Context.SessionActor.Tell(msg, null);
    }
}
```

## Command Attributes

### `[Command("name")]`
Defines the command name that players will type. This is the primary identifier for the command.

```csharp
[Command("levelup")]
private void LevelUpCommand() { }
```

### `[Alias("alias1", "alias2")]`
Provides alternative names for the same command. Useful for shortcuts or common variations.

```csharp
[Command("levelup")]
[Alias("lvlup", "level-up")]
private void LevelUpCommand() { }
```

### `[AuthRequired(AuthLevel)]`
Restricts command access based on player authority level. Available levels:
- `AuthLevel.QualityAssurance` - QA testers
- `AuthLevel.Developer` - Developers (debug builds only)
- `AuthLevel.Administrator` - Server administrators

```csharp
[Command("speed")]
[AuthRequired(AuthLevel.QualityAssurance)]
private void SetSpeedCommand(string speedMultiplier) { }
```

### `[Remainder]` Parameter Attribute
Combines all remaining command arguments into a single string parameter. Useful for commands that accept text with spaces.

```csharp
[Command("announce")]
private void AnnounceCommand([Remainder] string message) {
    // message contains all text after "announce"
}
```

## Command Groups

### Sub-Groups Structure
Commands are organized into logical groups based on functionality:

**Modify Commands (`mod`)**
- Character modification (level, speed, stats)
- Quick testing adjustments
- Development utilities

**Spellbook Commands (`sb`)**
- Spell learning and management
- Deck manipulation
- Magic school operations

**Quest Commands (`quest`)**
- `offer`, `grant`, `forcegrant` - show a quest's offer dialog, or start a quest through the normal accept path
- `list`, `info`, `show` - list active quests and goals, or show one quest's goals and progress
- `completegoal`, `completegoalfor`, `complete` - complete the current goal or a whole quest through `QuestService`
- `remove` - remove a quest (active or completed) and clear its registry entries so it can be offered again
- The commands only send a `MSG_QUESTCOMMAND` to the session; `QuestService` runs the action through its normal goal, quest and removal paths. The server does not know which quest the client tracks, so commands without a name use the only active quest.

**Badge Commands (`badge`)**
- `grant <id or name>` - award a badge through the normal award path (the client gets the new badge and the completion results run)
- `progress <counter key> <amount>` - add to a kill counter, for example `badge progress Badges_00002780 49`, so the next win completes the tier
- `list` - print the held badges and their progress
- `reset` - clear the badge state, the selected title and the nameplate title
- The commands only send a `MSG_BADGECOMMAND` to the session; `BadgeService` runs the action through its normal award, counter and title paths. `mod badge <locale id>` sets the nameplate title the same way and needs no relog.

**PvP Rating Commands (`pvp`)**
- `rating show [league]` - print the league, season, rating, rank name and index, wins, losses and draws of the record
- `rating set <rating> [league]` - set the rating (never below the floor of the table); the rank is recomputed, the client gets the records again and the badges of the ranks now reached are awarded
- `rank set <index or name> [league]` - set the rating to the lowest rating of that rank (rank 0 gets the start rating)
- `season reset [league]` - end the active record and start a new one at the carry-over rating of the rank it ended on, with wins and losses at 0
- `rating clear [league]` - delete every record of the league, so the next record starts at the start rating
- The league is the league type name and defaults to the first rated league with a running season that the sender may use. The commands act on the sender and work whether or not `RankedEnabled` is on. They only send a `MSG_RATINGCOMMAND` to the session; `PvPRatingService` does the work.

**DynaMod Commands (`dynamod`)**
- Listing, setting, toggling and removing the character's dynamic mods

**Debug Commands (`debug`)** 
- System information retrieval
- Position and state debugging
- Technical diagnostics

**Cheat Commands (`cheat`)**
- `win` - instantly end the current duel as a player victory
- `cinematic` - toggle instant spell cinematics for the player's own duels (effects still resolve; only the cinematic pacing collapses)
- `nofizzle` - toggle spell fizzling off for the player's own casts (the accuracy roll always succeeds; dispel wards still apply)

## Command Context

The `CommandContext` provides access to execution environment:

```csharp
private void ExampleCommand() {
    var character = Context.Character;        // Player's Wizard object
    var account = Context.Account;           // Player's Account object
    var sessionActor = Context.SessionActor; // Player's SessionActor reference
    var charObject = Context.CharacterObject; // Player's game object
    
    // Send message to client
    InformSenderClient("Command executed successfully.");
}
```

## Parameter Handling

Commands automatically parse and validate parameters:

```csharp
[Command("teleport")]
private void TeleportCommand(string zoneName, string location = "Start") {
    // zoneName is required, location defaults to "Start"
}

[Command("give")]
private void GiveItemCommand(string itemId, string quantity) {
    if (!uint.TryParse(itemId, out var itemIdUint)) {
        InformSenderClient("Invalid item ID.");
        return;
    }
    
    // Process command with validated parameters
}
```