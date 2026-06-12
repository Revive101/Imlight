# Combat System

Combat is orchestrated through [zone components](./zone-components.md), with the `CombatDuelComponent` managing entire duels while specialized classes handle spell resolution, damage calculation, and effect application.

## Combat Architecture

### Core Components

**`CombatDuelComponent`**
- Zone entity component that orchestrates entire duels
- Manages participant assignment and combat state transitions  
- Handles messaging between all participants
- Controls duel lifecycle from initiation to resolution

**`CombatDuelSubCircle`**
- Represents individual participant positions within the duel
- Manages participant-specific state (health, mana, pips, effects)
- Handles deck management and spell casting costs
- Tracks applied charms, wards, and hanging effects

**`CombatResolver`**
- Main entry point for processing combat actions during execution phase
- Coordinates spell resolution order and timing
- Manages accuracy checks and fizzle mechanics
- Orchestrates cinematic timing for client synchronization

**`CombatActionResolver`**
- Determines spell targets based on effect types
- Processes random effect selections and X-pip cost calculations
- Manages effect stack for client synchronization
- Bridges between queued actions and actual effect application

**`CombatEffectApplicator`**
- Applies damage, healing, and status effects to targets
- Handles stat-based damage scaling and resistance calculations
- Processes charm and ward modifiers
- Manages hanging effects and effect duration

### Supporting Systems

**`CombatDeck`**
- Manages spell card drawing, discarding, and hand management
- Provides randomized card selection from available spells
- Tracks remaining deck cards and discard pile
- Handles hand replenishment between rounds

**`CombatCharms`**
- Manages offensive modifiers that boost outgoing damage and healing
- School-specific and effect-type-specific bonuses
- Stacking behavior for multiple charm effects
- Distinguishes between beneficial and harmful modifiers

**`CombatWards`**
- Handles defensive modifiers that reduce incoming damage
- Damage absorption shields and resistance effects
- School-based damage reduction and conversion
- Proper application order for multiple ward effects

## Combat Flow

### Duel Initiation

Combat begins through zone interactions. If a player collides with a "dueling" creature, the creature's NPC component will fire `ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL` to the zone:

```csharp
// Hey! I'm a dueling creature and a player just entered my proximity.
// I really don't like that.
var interactionMsg = new ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL {
    StartingParticipants = new Dictionary<IActorRef, CoreObject> {
        { playerActor, playerObj },
        { Entity.SelfRef, Entity.ActiveGameObject },
    },
};
Entity.ZoneRef.Tell(interactionMsg);

// We do nothing further here. This message will be sent to the ZoneSigilSupervisor
// to locate the closest sigil to the player and the creature.
```

The `Zone` will locate the nearest sigil and notify the `CombatDuelComponent` to start a duel.

### Planning Phase

When the game client shows the cards UI and timer, the duel is in the planning phase.

**Hand Management**
1. **Draw Cards**: Each participant draws spell cards from their deck
2. **Spell Selection**: Players choose spells to cast or actions to take
3. **Discard Options**: Players can discard unwanted cards
4. **Action Types**: Attack, Flee, Discard, Pass, ChangeMind

**Move Types**
```csharp
public enum CombatMoveType {
    Attack = 0,     // Cast selected spell
    Flee = 1,       // Attempt to escape combat
    Discard = 2,    // Discard selected cards
    Pass = 3,       // Take no action this round
    ChangeMind = 4  // Modify previous selection
}
```

### Execution Phase

**Action Resolution Order**
1. **Queue Processing**: All participant actions collected and queued
2. **Accuracy Checks**: Determine spell success/fizzle based on accuracy stats
3. **Target Resolution**: Determine final targets based on spell effects
4. **Effect Application**: Apply damage, healing, and status effects in sequence
5. **Cleanup**: Update participant states and prepare for next round

**Spell Resolution Process**
```csharp
// Main resolution flow
var resolver = new CombatResolver(duel, subCircles);
resolver.Reset();
resolver.AddCombatMove(CombatMoveType.Attack, caster, target, spell);
float cinematicTime = resolver.ApplyQueuedCombatActions(out combatActionListObj);
```

## Spell System

### Spell Templates and Effects

**`SpellFactory`**
- Spell templates loaded from Root.wad [shared data](../concepts/shared-data-systems.md)
- Effect definitions specify damage, healing, and other utility effects

**Effect Processing**
```csharp
// Effect application with modifiers
var charms = CombatCharms.FindAppliedCharms(caster, effects);
var wards = CombatWards.FindAppliedWards(target, effect);
int modifiedDamage = CombatEffectApplicator.ApplyEffect(effect, charms, caster, targets);
```

### Accuracy and Fizzling

Imlight has no secrets here: it is a straightforward percentage check against a spell's accuracy rating.

**Accuracy Mechanics**
- Each spell has base accuracy percentage
- Caster stats and equipment modify accuracy
- Failed accuracy checks result in spell fizzling
- Fizzled spells consume pips but produce no effects

### Modifier System

**Charms (Offensive Modifiers)**
- Increase outgoing damage or healing
- Can be school-specific or universal
- Stack additively with proper ordering
- Applied before damage calculation

**Wards (Defensive Modifiers)**  
- Reduce incoming damage or provide absorption
- School-based resistance and universal protection
- Applied after damage calculation
- Can convert damage types or provide immunity

**Effect Stacking**
```csharp
// Charm application for outgoing damage
int modifiedDamage = CombatCharms.GetOutgoingDamageFromCharms(charms, baseDamage);

// Ward application for incoming damage  
int finalDamage = CombatWards.GetIncomingDamageFromWards(wards, modifiedDamage);
```

## Combat AI System

The **Combat AI** provides "journeyman level" decision-making for NPC creatures during combat encounters through the `CombatCreatureAIComponent`. 

:::warning
It's not possible to fully understand how live servers handle combat AI without access to the original configuration files, which are not publicly available. The AI behavior is heavily influenced by these settings, and without them, one can only approximate the intended functionality.
:::

### AI Component Architecture

**Component Dependencies**
- **PathMovementComponent** - Movement control (stops during combat)
- **NpcComponent** - Provides personality factors for decision-making  
- **StatsComponent** - Health/mana calculations and stat-based decisions

**Attachment Criteria**
```csharp
// AI component attaches to entities with both NPC and Duelist behaviors
public static bool ShouldAttachToEntity(CoreTemplate template)
    => template is GameObjectTemplate gameObjectTemplate
    && gameObjectTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate)
    && gameObjectTemplate.m_behaviors.Any(x => x is DuelistBehaviorTemplate);
```

### Personality System

The AI uses three core personality factors that determine behavioral patterns:

**Intelligence Factor**
- Determines chance to use the most damaging spell available
- Smart creatures prioritize high-pip, high-damage spells
- Non-smart creatures choose randomly from damage spells

**Aggressiveness Factor**  
- Controls likelihood of attacking vs "preparing" (buffing/healing/debuffing)
- Aggressive creatures prefer damage spells over support spells
- Non-aggressive creatures focus on defensive actions

**Selfishness Factor**
- When preparing, determines self-buffs vs teammate support
- Selfish creatures prioritize self-buffs over team healing
- Unselfish creatures provide team support when beneficial

:::info
Imlight does not define these factors. They are defined in the NPC's template files. Imlight knows these values, but as mentioned previously must guess as to how they are used.
:::

### Hate Table (Aggro System)

The AI maintains a hate table tracking aggro values for each enemy:

**Aggro Sources**
- **Damage Taken**: Increases hate toward the attacker
- **Enemy Healing**: Increases hate toward healers providing enemy support
- **Provoke Effects**: Taunt spells increase hate toward the caster
- **Pacify Effects**: Reduce hate toward the caster

**Target Selection**
- AI always attacks the enemy with highest hate value
- Hate persists throughout the entire duel
- Ties broken randomly

### Decision-Making Flow

Each combat round follows this process:

1. **Attitude Determination**: Roll against personality factors for this turn's behavior
2. **Stun Check**: If stunned, pass and decrement stun counter
3. **Aggression Check**: If aggressive AND has damage spells → Attack
4. **Defensive Behavior**: Otherwise → Prepare (heal/buff/debuff)

### Aggressive Behavior

When choosing to attack:

```csharp
private COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE DetermineAggressiveBehavior() {
    var castableDamageSpells = GetCastableDamageSpells(_roundHand.m_spellList);
    var targetIdx = GetMostHatedTarget();
    
    // Intelligence determines spell selection
    if (_determinedSmartThisTurn) {
        // Use highest pip-cost damage spell
        var highestPipSpell = castableDamageSpells
            .OrderByDescending(x => x.m_pipCost.m_spellRank)
            .FirstOrDefault();
    } else {
        // Choose random damage spell
        var randomSpell = castableDamageSpells[_random.Next(castableDamageSpells.Count)];
    }
}
```

### Defensive Behavior

When choosing to prepare:

1. **Pass Chance**: Small random chance to pass instead of acting
2. **Healing Priority**: If allies need healing AND healing roll succeeds
3. **Buff/Debuff Selection**: Choose between team support and enemy hindering
4. **Selfishness Check**: Determines targeting for beneficial spells

### Spell Classification

AI categorizes spells by effect type:
- **Damage Spells**: Direct harm effects for aggressive behavior
- **Healing Spells**: Health restoration (including HealOverTime, HealPercent)
- **Buff Spells**: Positive effects for self or teammates
- **Debuff Spells**: Negative effects applied to enemies

### Configuration Integration

AI behavior is configurable through settings:

```ini
[Combat]
HealingThreshold = 0.5           # Health % that triggers healing consideration
HealingPercentChance = 0.3       # Probability of choosing to heal when viable
PreparePassChance = 0.1          # Chance to pass during defensive turns
DamagedAggroIncrease = 10        # Hate increase when taking damage
HealingAggroIncrease = 5         # Hate increase toward enemy healers
ProvokeAggroIncrease = 15        # Hate increase from taunt effects
PacifyAggroDecrease = 8          # Hate decrease from pacify effects
```