<p align="center">
  <img src="https://i.ibb.co/kQT3jSJ/logo-full.png" />
  <h3 align="center">Wizards Rewriting The Rules of Magic</h3>
  <h6 align="center">An independent private server project aimed to reimagine a wizard adventure, written entirely in C#.</h6>
</p>
<p align="center">
    <a href="https://discord.gg/HjJCwm5">
        <img src="https://img.shields.io/discord/940647911182729257?logo=discord"
            alt="chat on Discord"></a>
</p>

---

## About
The "Imlight" project began as a learning exercise to explore the mechanics and architecture of MMORPG server design. We have the utmost respect for the original developers and have taken steps to ensure our project does not infringe on their intellectual property. That being said, Imlight has a BYOD (bring-your-own-data) philosophy and does not distribute any copyrighted game files. Users must obtain the original client and any necessary assets independently.

## Requirements
Imlight has a few running gears, and expects existing tools to be available at specific locations.

#### Patch Server
Imlight and the game client should both source game files from the same location. You may use our open source option, [Aurorium](https://github.com/Revive101/Aurorium), to host one yourself. However, the default Imlight configuration comes shipped with a URL to an existing patch server available to Revive101 developers.

#### Dragon Database
Imlight uses [RavenDB](https://ravendb.net/) to store its persistent data. There are two databases used by Imlight.
* `WorldData`: The world data, such as zone transfers and active events. It's recommended that the development party should have access to this database to create the relevant data in unison.
* `PlayerData`: The users' account and character data. This is incredibly sensitive, and is only recommended to be use in production deployment scenarios.

If a URL is not present in the configuration, Imlight will instead employ an embedded database for either of the databases.
If a database URL *is* present, _dragon_ requires certificates to be available at `./Imlight/Certificates/`. 

## Information
* The Kronos teams' [Grimoire](https://kronos-project.github.io/grimoire/foreword.html).
* Our [Documentation](https://revive101.github.io/Imlight-docs/) book.

## Contributing
Contributions are welcome and highly encouraged from anyone.

For [Semver](https://semver.org/) purposes, we ask that all commits abide by [conventions](https://www.conventionalcommits.org/en/v1.0.0/#summary).
