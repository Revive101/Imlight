# Imlight Todo

## Bottlenecks & Performance
- [ ] Synchronous Socket I/O
- [ ] Database should use in-memory cache with periodic DB sync
  - [ ] Use pooled sessions rather than instantiation each time
- [ ] ReceiveProtocolDispatcher cache
- [ ] Improving ReceiveProtocolDispatcher
- [ ] ZoneActor batch zone broadcast
  - Player A moves → MoveService processes instantly → adds to zone broadcast batch
  - Every 10ms → zone sends batched updates to all players
  - Each player receives 1 packet with multiple moves instead of multiple individual packets
- [ ] GetActiveWizard can be cached
- [ ] GetActiveAccount can be cached

## Combat
- [ ] Combat hand:
  - [ ] Treasure cards
  - [ ] Commands to add/remove a spell (TC)
  - [ ] "X" out item cards in deck
- [ ] Combat effects:
  - [x] Fizzling
  - [x] Damage
  - [x] Healing
  - [x] Area-of-effect spells
  - [x] Reshuffle
  - [x] Pacify
  - [x] Provoke
  - [x] Global spells
  - [x] Stealing hanging effects
  - [ ] Self damage (Sacrifice and Immolate)
  - [ ] Per pip spells
  - [ ] Pip donation spells
- [ ] Minions:
  - [ ] Minion cards
  - [ ] Spawning minions
  - [ ] Minion AI
  - [ ] Stealing health from minion
  - [ ] Sacrifice minion for pips
  - [ ] Sacrifice minion for health
- [ ] Reserved spots for group members
- [ ] Critical strikes
- [ ] Critical blocks
- [ ] PvP
  - [ ] Casual matches
  - [ ] Ranked matches

## Social
- [ ] Friends List:
  - [x] Adding/removing a friend
  - [ ] Teleporting to a friend
  - [ ] Buddy stats
  - [ ] Buddy Stats CRC
- [ ] Reporting others
- [ ] Ignoring others
- [ ] Groups
- [ ] Group chat
- [ ] Trading treasure cards
- [ ] Best friends
- [ ] Whispering

## Shops
- [ ] Treasure card shops
- [ ] Potion shop
- [ ] Gardening vendors
- [ ] Recipe vendors
- [x] Bazaar
- [x] Professors

## Zones
- [ ] Being kicked off a mount in non-mount areas
- [ ] Dungeons
  - [ ] Tower sigils
- [ ] Krokotopia secret shop
- [x] Chests
- [ ] Silver chests
- [ ] Realms
- [ ] Changing realms

## Drop Tables
- [x] After combat rewards
- [ ] Tool to remake drop tables
- [ ] Minigame rewards

## Pets
- [ ] Make an understanding of how the client expects the pet behavior to be shipped
- [ ] Looking through pet objects to find hatch status
- [ ] Equipping pets
- [ ] Discarding pets
- [ ] Pet renaming
- [ ] Pet dyes
- [ ] Pets being capable of leveling up (or aging as the game puts it)
- [ ] Pets being able to add to canonical effects
- [ ] Pet snacks
- [ ] Pet Training
- [ ] Pet talents
- [ ] Pet powers
- [ ] Feeding pets snacks
- [ ] Pet Minigames:
  - [ ] Dance Game
  - [ ] Gobbler Drop
  - [ ] Cannon Game
  - [ ] Maze Game
  - [ ] Grumpy Gobblers
  - [ ] Way of the Ninja Pig
- [ ] Pet Derby:
  - [ ] Practice Pet Derby
  - [ ] Ranked Pet Derby

## Progression
- [ ] Starting with school spells
- [x] Leveling up naturally
- [x] Gaining experience
- [ ] Badges
- [ ] Titles

## Quests
- [ ] Tutorial
- [ ] Quest Givers:
  - [x] '!' above NPC heads
  - [x] Quest requirements
  - [x] Changing NPC wizbang on quest status change
  - [x] Accepting quests from NPCs
- [x] Saving quests persistently
- [x] Seeing quests on game server attach
- [ ] Quest helper
- [ ] Request quest dialog button
- [ ] Quest start results
- [ ] Quest end results
- [ ] Automatically starting dialog of next quest
- [ ] Goals:
  - [x] Activation results
  - [x] Completion results
  - [x] "New Goal" text
  - [x] Kill mobs
  - [x] Kill & collect
  - [x] Speak to NPC
  - [ ] Madlibs
  - [ ] Achieve rank (level)
  - [ ] Waypoint:
    - [x] Enter zone
    - [ ] Exit zone
    - [ ] Enter volume
    - [ ] Exit volume

## Equipment
- [ ] Seamstress
- [ ] Banking
- [ ] Elixirs
- [ ] Time limited mounts
- [ ] Jewel socketing

## Crafting
- [ ] Recipes
- [ ] Crafting station
- [ ] Crafting slots
- [ ] Crafting titles

## Player Housing
::: tip
Imlight should have social elements before starting this branch.
:::
- [x] A `Wizard` keeps the lot of every house item it was issued, and each house is its own `Houses/{lot}` document
- [x] Houses, their zones and their starting layouts come from the game data
- [x] House decorations: placing, moving, rotating, scaling and picking up objects
- [x] House decorations being saved persistently
- [x] Pick Up All Items and the attic
- [x] Teleporting home (to the starter house)
- [x] Teleporting to your own house through its doors, deeds and scripted teleports
- [x] Teleporting to a friend's or group mate's house
- [x] Sending visitors away and hiding from friends
- [x] Equipping a house in the `Islands` and `Structure` slots
- [x] Deleting a character deletes its houses and attic
- [x] `.housegrant` gives a character any house for testing
- [ ] Home goes to the equipped house (needs a packet capture of equipping a castle)
- [ ] Island limit and switching castles (needs a packet capture)
- [ ] Buying a house (needs a packet capture of a house purchase)
- [ ] Selling a house (needs a packet capture)
- [ ] Housing previews
- [ ] Objects that need their own state: music, teleporters, castle blocks, pet guests and the other families
- [ ] Castle Tours
- [ ] Refusing placements outside the extent of a house zone (optional)
- [ ] PvP sigils

## Gardening
- [ ] Seeds
- [ ] Plant needs
- [ ] Plant phases (young/mature/elder)
- [ ] Gardening titles

## Guilds
- [ ] Creating a guild
- [ ] Guild chat
- [ ] Guild halls

## Additional Features
- [ ] Fishing
- [ ] Castle Magic
- [ ] Rate my stitch
- [ ] Multi-person mounts
- [ ] Raids
- [ ] Pixie Post
- [ ] Team Up system
- [ ] True friend codes
- [ ] Patch Server

## Packet Captures Needed
- [ ] Using treasure cards
- [ ] Treasure card vendor
- [ ] Sunken City played in full
- [ ] PvP duel
- [x] Opening a wooden chest
- [ ] Playing a silver chest minigame
- [ ] Seamstress