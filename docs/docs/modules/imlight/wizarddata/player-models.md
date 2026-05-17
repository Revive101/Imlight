# Player Models

Player data is represented through two primary models: **Account** and **Wizard**. These models define the structure of user accounts and player characters, with accounts serving as containers for multiple characters.

:::tip
It's very important to understand the [player architecture](../concepts/playerarchitecture.md) when working with these models. Please review that article before proceeding.
:::

## Quick Implementation Guide

1. Do not edit offline accounts or characters. If necessary, cache the changes and apply them when the player is online. An example might be player A removing player B (while player B is offline) from their friends list:
    * Player A is online, so database changes can be made immediately.
    * Player B is offline, so the change must be cached and applied when they next log in.
2. Never make database calls from behaviors. Let the `Wizard` model change the behavior, and then make the database operation.
3. Use collection classes to access and modify data. 

## Account Model

The `Account` class represents a user's game account and serves as the authentication and character management entity.

### Core Properties

```csharp
public class Account {
    public ulong AccountId { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string HashedPassword { get; set; }
    public AuthLevel AuthLevel { get; set; }
    public AccountFlags AccountFlags { get; set; }
    public ChatMode ChatMode { get; set; }
    public List<Wizard> Characters { get; set; }
}
```

### Authentication Levels

```csharp
public enum AuthLevel {
    None,                // Regular players
    QualityAssurance,    // QA testers
    HallMonitor,         // Community moderators  
    Developer,           // Development team
    Administrator        // Server administrators
}
```

### Account Flags

Account flags control feature access and permissions:

```csharp
[Flags]
public enum AccountFlags {
    IsHallMonitor = 1 << 0,
    CanChat = 1 << 1,
    CanHaveCustomNames = 1 << 2,
    CanOpenChat = 1 << 3,
    CanTrueFriendCode = 1 << 5,
    CanGift = 1 << 6,
    CanReportBugs = 1 << 7,
    CanEarnCrownsOffers = 1 << 11,
    CanEarnCrownsButton = 1 << 12
}
```

### Chat Modes

```csharp
public enum ChatMode {
    Open,      // Full chat functionality
    Filtered,  // Filtered chat with restrictions
    Closed     // No chat allowed
}
```

## Wizard Model

The `Wizard` class represents an individual player character with all associated game data and progression.

### Identity Properties

```csharp
public class Wizard {
    public ulong AccountId { get; set; }    // Parent account reference
    public ulong CharId { get; set; }       // Unique character identifier
    public string Zone { get; set; }        // Current zone location
    public string PreviousZone { get; set; } // Last zone for fallback
    public byte World { get; set; }         // World/realm identifier
}
```

### Location Data

```csharp
public Vector3 Location { get; set; }      // 3D world position
public Vector3 Orientation { get; set; }   // Character facing direction
public Vector3 MarkedLocation { get; set; } // Marked teleport location
public string MarkedZone { get; set; }     // Marked zone for teleport
```

### Behavior System Integration

The `Wizard` model contains modular behavior components that handle specific aspects of character functionality:

```csharp
// Core character behaviors
public ServerMagicSchoolBehavior MagicSchoolBehavior { get; set; }
public ServerWizGameStats GameStatsBehavior { get; set; }
public ServerWizInventoryBehavior InventoryBehavior { get; set; }
public ServerWizEquipmentBehavior EquipmentBehavior { get; set; }
public ServerSpellbookBehavior SpellbookBehavior { get; set; }

// Social and secondary behaviors  
public ServerFriendBehavior FriendBehavior { get; set; }
public ServerPetOwnerBehavior PetOwnerBehavior { get; set; }
public ServerAlchemyBehavior AlchemyBehavior { get; set; }
```

:::warning
Previously mentioned in the [player architecture article](../concepts/playerarchitecture.md#explaining-persistence), behaviors should never make database operations.

Use the `Wizard` class: change the behavior, then save the `Wizard` to persist changes.
:::

### Game Object Integration

Wizards can have an associated game object for active gameplay:

```csharp
[JsonIgnore]
public WizClientObjectCharacter GameObject { get; set; }

// Location properties sync with game object when present
public Vector3 Location {
    get => GameObject?.m_location ?? _location;
    set {
        if (GameObject is not null) {
            GameObject.m_location = value;
        } else {
            _location = value;
        }
    }
}
```

## Data Relationships

### Account-Character Relationship

```csharp
// Account contains characters
public class Account {
    public List<Wizard> Characters { get; set; }
    
    public Wizard GetCharacter(ulong charId) {
        return Characters.FirstOrDefault(c => c.CharId == charId);
    }
}

// Character references parent account
public class Wizard {
    public ulong AccountId { get; set; }
}
```

### Database Storage Pattern

Accounts and characters are stored in separate collections despite their relationship:

```csharp
// Creating an account stores characters separately
public static bool CreateAccount(Account account) {
    using var session = s_store.OpenSession();
    
    // Store each character in WizardCollection
    foreach (var character in account.Characters) {
        WizardCollection.AddCharacter(character);
    }
    
    // Store account in AccountCollection
    session.Store(account);
    session.SaveChanges();
    return true;
}
```