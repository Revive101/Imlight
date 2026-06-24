<p align="center">
  <img src="https://i.ibb.co/kQT3jSJ/logo-full.png" />
  <h6 align="center">An independent private server project aimed to reimagine a wizard adventure, written entirely in C#.</h6>
</p>
<p align="center">
    <a href="https://discord.gg/HjJCwm5">
        <img src="https://img.shields.io/discord/940647911182729257?logo=discord"
            alt="chat on Discord"></a>
</p>

---

## About
The "<B>Imlight</B>" project began as a learning exercise to explore the mechanics and architecture of MMORPG server design. We have the utmost respect for the original developers and have taken steps to ensure our project does not infringe on their intellectual property. 

That being saiyd, Imlight has a BYOD (bring-your-own-data) philosophy and does not distribute any copyrighted game files. Users must obtain the original client and any necessary assets independently.

## Disclaimers
Imlight is a framework for building a private server. Do not expect a full game experience OOTB.

The game client does not ship with all of the data required to run a true server, and thus some of it must be recollected manually. You can find the data we've managed to collect in the [spiraldb](https://github.com/Revive101/spiraldb) repository.

If you care to contribute to the data recollection effort, you may use [Imview](#imview). Imview is Imlight's one-stop tool for managing Imlight, as well as data recollection.

### What you can expect
- [x] Character management
- [x] Moving through the world *
- [x] Zone objects
- [x] Mobs
- [x] Inventory and equipment
- [x] Multiplayer
- [x] Chat
- [x] Friends
- [x] Shops *
- [x] Dye Shop
- [x] Mounts
- [ ] Multi-person Mounts
- [x] Spell Training *
- [x] Most parts of combat
- [x] Reagents 
- [x] Minigames
- [x] Creating spell decks
- [ ] Dungeons (exist, but entrance sigils are not yet implemented)
- [x] Cantrips
- [x] In-game commands
- [x] Potions
- [x] NPC interactions
- [x] Bazaar
- [ ] Realms
- [x] Pets (partial)
- [ ] Pet minigames
- [ ] Crafting
- [ ] Housing
- [x] Quests *
- [x] Drop tables *

* Engine is fully implemented. However, actual content depends on [SpiralDB](https://github.com/Revive101/spiraldb) data, which may not be available in all places.

## Getting Started
The only true requirement is the patch server. The rest of the components are optional, but are recommended.

#### Patch Server
You may use our open source option, [Aurorium](https://github.com/Revive101/Aurorium), to host one yourself. Both Imlight and your game client may be pointed to an Aurorium instance to serve game patches and data.

Aurorium's first boot will take time.

#### Player Database
Imlight uses [RavenDB](https://ravendb.net/) for player-generated data such as accounts, characters, chat logs, and friendships. If you do not provide a remote URL in the configuration, Imlight will spool up an embedded instance. If you <i>do</i> provide one, a certificate path must be provided as well.

#### Spiral Database 
World data — NPC inventories, zone transfers, quest templates, drop tables, and other static content the game client does not ship with — is served by **SpiralDB**. On boot, Imlight fetches the latest data from the [spiraldb](https://github.com/Revive101/spiraldb) GitHub repository (or uses a local cache) and loads it directly into memory. No RavenDB instance is needed for world data.

You may control the SpiralDB behaviors in the configuration file, such as changing the remote and cache paths, and whether to fetch the latest data on boot.

### Configuration
Imlight's configuration is stored in `Imlight.ini`. It is recommended to review the configuration file and adjust it to your needs before booting the server. Additionally, an admin account is always created and the details for this account can be found in the configuration file as well. You should change these details before booting the server.

### Client
You can start the game client with a launch argument to connect to your server. `-L 127.0.0.1 12000` will work fine.

## Building

#### Imcodec
[Imcodec](https://github.com/Jooty/Imcodec) is imported as a submodule and provides tooling required by Imlight for handling proprietary game data. 

##### Building Imcodec
Imcodec relies on compile-time source generation to produce C# classes from game data definitions.
1. Obtain a type dump JSON file using [wiztype](https://github.com/wizspoil/wiztype). This file should be saved in
    ```
    /submodule/Imcodec/src/Imcodec.ObjectProperty/GeneratorInput/
    ```
2. Unpack the root archive using Imcodec itself (via `imcodec wad unpack`). Inside, locate the message definition files which can be matched with a wildcard of `*Messages*.xml`. Place these files in:
   ```
   /submodule/Imcodec/src/Imcodec.MessageLayer/GeneratorInput/
   ```

## Information
* The Kronos teams' [Grimoire](https://kronos-project.github.io/grimoire/foreword.html).
* Our [Documentation](https://revive101.github.io/Imlight-docs/) book.

## Contributing
Contributions are welcome from anyone.

For [Semver](https://semver.org/) purposes, we ask that all commits abide by [conventions](https://www.conventionalcommits.org/en/v1.0.0/#summary).
