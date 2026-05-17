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

**Debug Commands (`debug`)** 
- System information retrieval
- Position and state debugging
- Technical diagnostics

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