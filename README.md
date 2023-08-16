<p align="center">
  <img src="https://i.ibb.co/3m7W22D/imlight-logo.png" />
  <h3 align="center">Wizards Rewriting The Rules of Magic</h3>
</p>
<p align="center">
    <a href="https://discord.gg/HjJCwm5">
        <img src="https://img.shields.io/discord/940647911182729257?logo=discord"
            alt="chat on Discord"></a>
</p>

---

## Introduction
**Imlight** is an independent private server project aimed to reimagine a wizard adventure, written entirely in C#. 

The file hierachy of this project is heavily based on [TrinityCore](https://github.com/TrinityCore/TrinityCore).

## Requirements
Imlight has a few running gears, and expects existing tools to be available at specific locations. These tools will inevitably be available on this repository.

* Imlight sources game data to run the zones. Imlight should be pointed at the same URL direction as the game client for patching.
* A [RavenDB](https://ravendb.net/) cluster should be used with two databases: `Imlight`, which is server-exclusive data and should be available to developers; `Playerdata`, whos certificate should only ever be used in live deployments or production environments. If `Playerdata` certificate is not found, Imlight will use an embedded database instead.

## Information
* Our [Compendium](https://compendium.onrender.com/) book, relating to game client internals.

## Contributing
Contributions are welcome and highly encouraged from anyone.

For [Semver](https://semver.org/) purposes, we ask that all commits abide by [conventions](https://www.conventionalcommits.org/en/v1.0.0/#summary).
