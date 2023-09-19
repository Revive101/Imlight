<p align="center">
  <img src="https://i.ibb.co/z5JXHDz/2023-09-07-15-31.png" />
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
Imlight has a few running gears, and expects existing tools to be available at specific locations.

#### Patch Server
Imlight sources game data to run the zones. Imlight should be pointed at the same URL direction as the game client for patching.

#### Dragon Database
Imlight uses [RavenDB](https://ravendb.net/) to store its persistent data. There are two databases used by Imlight.
* `Imlight`: The world data, such as zone transfers and active events. It's recommended that the development party should have access to this database to create the relevant data in unison.
* `Playerdata`: The users' account and character data. This is incredibly sensitive, and is only recommended to be use in production deployment scenarios.

If a URL is not present in the configuration, Imlight will instead employ an embedded database for either of the databases.
If _dragon's_ `Playerdata` database starts in embedded mode, Imlight will create 1-9 debug accounts.
* The username will be `admin[1-9]`.
* The password will always be `debug`.
* For example, you may log in with username `admin4` and password `debug`.

If a database URL *is* present, _dragon_ requires certificates to be available at `./Imlight/Certificates/`. 

## Configuration
Imlight comes preequipped with a configuration file at `./Config/Imlight.ini`. A default configuration is built with _Imlight.Backend_:
```ini
[Global Settings]
GameRevision = 740872

[Logging]
LogPath = ./logs/log.txt
LogsIncludeTimestamp = True

[Login Server]
; The internal name for the login server. This is not a realm name.
LoginServerName = Imlight.Login
LoginServerPort = 12000
LoginPlayerLimit = 1000
MaxAllowedCharactersPerAccount = 6
; The time in seconds that a user can be AFK before being disconnected.
LoginAfkTimeout = 360
; The time in seconds between AFK checks.
LoginAfkCheckInterval = 60

[Game Server]
; The internal name for the game server. This is not a realm name.
GameServerName = Imlight.Game
GameServerPort = 12333
GameServerPlayerLimit = 1000
; The number of game servers that can be created.
MaxGameServersAllowed = 3
; The time in seconds that a session key is valid.
SessionKeyValidityTime = 28800

[Character]
StartingLevel = 1
StartingZone = WizardCity/WC_Hub
StartingWorld = 1
BaseGoldPouch = 1000000

[Patch Server]
; The internal name for the patch server. This is not a realm name.
PatchServerName = Imlight.Patch
; The port used by the patch server.
PatchServerPort = 12500
; The URL to the patch server.
PatchServerInternalUrl = http://phill030.de
; The internal port used by the patch server.
PatchServerInternalPort = 12369
; The time in seconds that the patch server will wait to reach the endpoint before timing out.
PatchServerInternalTimeout = 10

[Database]
PlayerDatabaseUrl = 
PlayerDatabaseName = Playerdata
PlayerDatabaseCertificatePath = ./Certificates/playerdata.client.certificate.pfx
WorldDatabaseUrl = https://a.voidly.ravendb.community
WorldDatabaseName = Imlight
WorldDatabaseCertificatePath = ./Certificates/worlddata.client.certificate.pfx
DatabaseMaxNumberOfRequestsPerSession = 16
DatabaseRequestTimeoutInSeconds = 90
DatabaseWaitForNonStaleResultsTimeout = 5
; The directory where the embedded database will be stored.
EmbeddedDatabaseDataDirectory = ./ImlightEmbeddedDatabase
; The port used by the embedded database.
EmbeddedDatabasePort = 8080
; The time in seconds that the embedded database will wait to reach the endpoint before timing out.
EmbeddedDatabaseTimeoutTime = 90
; If true, a full RavenDb database will be used. The full database includes a dotnet runtime.
EmbeddedDatabaseUseFull = False
; The path to the full RavenDb. Only used if EmbeddedDatabaseUseFull is true.
EmbeddedDatabaseFullPath = /home/makima/Documents/Projects/RavenDB/Server/

[Advanced]
; Please only change these settings if you know what you are doing.
CharacterUploadIntervalInMinutes = 6
; The salt used to hash the session key.
SessionKeyHashInput = MAGIC_HATTER
PatchServerBufferSize = 4096
PatchServerUserAgent = KingsIsle Patcher
; The size of the buffer used by the session actor.
SessionActorBufferSize = 4096
; The thread pool size used by the session actor to send messages.
SessionActorSendPoolSize = 3
; The thread pool size used by the session actor to receive messages.
SessionActorReceivePoolSize = 3
; If true, the session actor will close on exception.
SessionActorCloseOnException = True
; The number of times the session actor will restart a service before crashing.
SessionActorServiceRetryCount = 3
; The time in seconds that the session actor will wait before restarting.
SessionActorServiceRangeRetry = 30
; The time in seconds that the server will send a heartbeat to the client.
KeepAliveInterval = 60
; The time in seconds the server will wait for a heartbeat response.
KeepAliveRspWaitTime = 4
```
_Note: This configuration is likely not up to date._

## Information
* Our [Compendium](https://compendium.onrender.com/) book, relating to game client internals.

## Contributing
Contributions are welcome and highly encouraged from anyone.

For [Semver](https://semver.org/) purposes, we ask that all commits abide by [conventions](https://www.conventionalcommits.org/en/v1.0.0/#summary).
