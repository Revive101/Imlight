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

That being said, Imlight has a BYOD (bring-your-own-data) philosophy and does not distribute any copyrighted game files. Users must obtain the original client and any necessary assets independently.

## Disclaimers
Imlight is a framework for building a private server. Do not expect a full game experience OOTB.

The game client does not ship with all of the data required to run a true server, and thus some of it must be recollected manually. You can find the data we've managed to collect in the [spiraldb](https://github.com/Revive101/spiraldb) repository.

### Status
Most of the core game works: characters, combat, quests, dungeons, pets and housing. How much content you get depends on [SpiralDB](https://github.com/Revive101/spiraldb) data, which may not be available in all places.

## Getting Started
### Requirements
The only true requirement is a file host. The rest of the components are optional, but are recommended.

- The [.NET 10 SDK](https://dotnet.microsoft.com/download).
- A copy of the original game client.
- A file host (required).
- A player database (optional).
- Spiral Database (optional, recommended).

### File Host
Imlight does not ship game files. It downloads them from a file host and keeps them in a local cache, and it tells game clients where to find the same files. You may use our open source option, [Aurorium](https://github.com/Revive101/Aurorium), to host one yourself.

Point `PatchServerInternalUrl` in the `[Patch Server]` section of the configuration at your host. It points at a local host by default: `http://127.0.0.1:12369`. Imlight looks for the files under `<PatchServerInternalUrl>/V_<revision>`. Downloaded files are stored in `LocalWadCachePath`. If the host cannot be reached, Imlight uses the local cache only. Without a host and without a filled cache, it cannot start.

Aurorium's first boot takes a while, and the game files take up roughly 25 GB of disk space.

### Player Database
Imlight uses [RavenDB](https://ravendb.net/) for player-generated data such as accounts, characters, chat logs, and friendships. If `PlayerDatabaseUrl` is empty, Imlight will spool up an embedded instance. If you <i>do</i> provide one, set `PlayerDatabaseCertificatePath` to its client certificate (`.pfx`) when the server requires one.

### Spiral Database
World data (NPC inventories, zone transfers, quest templates, drop tables, and other static content the game client does not ship with) is served by **SpiralDB**. On boot, Imlight fetches the latest data from the [spiraldb](https://github.com/Revive101/spiraldb) GitHub repository (or uses a local cache) and loads it directly into memory. No RavenDB instance is needed for world data.

You may control the SpiralDB behaviors in the `[Database]` section of the configuration: `SpiralDBRemoteUrl`, `SpiralDBBranch`, `SpiralDBLocalPath`, `SpiralDBAutoFetch` and `SpiralDBDisableRemote`.

### Configuration
Imlight's configuration is stored in `Imlight.ini`, found in `src/Imlight.Director/Config/` and copied to `Config/` in the build output (the copy is what the server reads). Review it and adjust it to your needs before booting the server. The settings you will most likely touch:

- `[Login Server]`: `LoginServerPort` (12000 by default).
- `[Game Server]`: `GameServerIP`, `GameServerPort`, `GameServerCount` and `RealmNames`.
- `[Patch Server]`: `PatchServerInternalUrl` and `LocalWadCachePath`.
- `[Database]`: the player database and SpiralDB settings above, and `AdminAccountUsername` and `AdminAccountPassword`.

An admin account is always created from the `AdminAccountUsername` and `AdminAccountPassword` settings. Change them before booting the server.

### Client
You can start the game client with a launch argument to connect to your server. `-L 127.0.0.1 12000` will work fine.

## Building
Imlight needs the [.NET 10 SDK](https://dotnet.microsoft.com/download). [Imcodec](https://github.com/Jooty/Imcodec) is imported as a submodule and provides tooling required by Imlight for handling proprietary game data, so clone with submodules:

```bash
git clone --recurse-submodules <repository url>
```

If you already cloned without them:

```bash
git submodule update --init --recursive
```

Imcodec relies on compile-time source generation to produce C# classes from the client's data definitions, and that data is not distributed. Point `ImcodecClientDir` at your client install and the build extracts what it needs for you:

```bash
export ImcodecClientDir=<client install directory>
dotnet build src/Imlight.sln
```

To supply the inputs by hand instead, or to refresh them after a client update, see the "Building" section of the [Imcodec README](submodule/Imcodec/README.md).

## Running
Make sure the file host is up and `Imlight.ini` is configured, then start the Director:

```bash
dotnet run --project src/Imlight.Director
```

The Director starts the login, game and patch servers. The first boot takes a while as game files are downloaded and cached.

## Information
* The Kronos teams' [Grimoire](https://kronos-project.github.io/grimoire/foreword.html).
* Our [Documentation](https://revive101.github.io/Imlight-docs/) book.

## Contributing
Contributions are welcome from anyone.

For [Semver](https://semver.org/) purposes, we ask that all commits abide by [conventions](https://www.conventionalcommits.org/en/v1.0.0/#summary).
