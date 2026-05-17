# Commands
A number of commands are available for game masters.

Security tiers:
* `0` - Normal Player
* `1` - Quality Assurance
* `2` - Hall Monitors
* `3` - Developers (_only available in debug build_)
* `4` - Game Master

If Imlight was built in _release_ mode, any command with a developer security level required will instead be forwarded to Game Master tier.

## Ungrouped Commands
| Command | Security | Syntax | Description |
| ------- | -------- | ------ | ----------- |
| teleport | Quality Assurance | `.teleport $zonename` | Teleports you to a specific zone. If a zone is not found, it will guess. |

## Account Commands

| Command | Security | Syntax | Description |
| ------- | -------- | ------ | ----------- |
| account | Hall Monitor | `.account` | Shows all the available account commands. |
| account create | Hall Monitor | `.account create $username $password` | Creates an account with a username and password. |
| account delete | Game Master | `.account delete $username` | Deletes an account associated with a username. |
| account lock | Hall Monitor | `.account lock $username` | Prevents an account from logging in. |
| account unlock | Hall Monitor | `.account unlock $username` | Unlocks an account. |
| account password | Game Master | `.account password $username $password $password` | Changes the password for an account. |
| account authlevel | Game Master | `.account authlevel $username $newlevel` | Sets the authority level for an account, |
| account info | Hall Monitor | `.account info $username` | Shows information about an account. | 
| account infractions | Hall Monitor | `.account infractions $username` | Shows the infractions set on an account. |
| account warn | Hall Monitor | `.account warn $username $reason` | Adds an infraction record to an account. |
| account removewarn | Hall Monitor | `.account removewarn $username` | Removes an infraction record from an account. |
| ban | Hall Monitor | `.ban` | Shows all of the available ban commands. |
| ban account | Hall Monitor | `.ban account $username $bantime <e.g. 4d20h3s> $reason` | Bans an account for an amount of time. |
| ban ip | Hall Monitor | `.ban ip $ip $bantime <e.g. 4d20h3s> $reason` | Bans an IP for an amount of time. |
| ban machine | Hall Monitor | `.ban machine $machineid $bantime <e.g. 4d20h3s> $reason` | Bans a machine Id for an amount of time. |
| ban info | Hall Monitor | `.ban info $username` | Shows information about the most recent ban on an account. |
| unban | Hall Monitor | `.unban` | Shows all of the available unban commands. |
| unban account | Hall Monitor | `.unban account $username` | Unbans an account. |
| unban ip | Hall Monitor | `.unban ip $ip` | Unbans an IP. |
| unban machine | Hall Monitor | `.unban machine $machineid` | Unbans a machine ID. |

## Context Commands
The commands below require a player to be selected to use.

:::warning
Your command context will not drop. If you select a player, that player will still be in your context no matter how much time has passed.
:::

| Command | Security | Syntax | Description |
| ------- | -------- | ------ | ----------- |
| mute | Hall Monitor | `.mute $duration <e.g. 4d20h3s> $reason` | Mutes the entire account. |
| unmute | Hall Monitor | `.unmute` | Unmutes the account. |
| kick | Hall Monitor | `.kick` | Kicks the player from the current server. |
| warn | Hall Monitor | `.warn $reason` | Adds an infraction record to the user's account. |
| info | Hall Monitor | `.info` | Shows account information about the selected character. |

## Modification Commands

| Command | Security | Syntax | Description |
| ------- | -------- | ------ | ----------- |
| levelup | Quality Assurance | `.mod levelup` | Levels up your character. |
| level | Quality Assurance | `.mod level $level` | Levels up your character to a certain level. |
| speed | Quality Assurance | `.mod speed $speed`| Changes your speed. The number matches the ingame, so `40` would give you mount speed. |
| additem | Quality Assurance | `.mod additem $id` | Adds an item to your inventory by ID. |
| addsnack | Quality Assurance | `.mod addsnack $id` | Adds a snack to your pet snack bag. |
| name | Quality Assurance | `.mod name $newName` | Adds a new name override to your character. |
| maxgold | Quality Assurance | `.mod maxgold $newMaxGold` | Changes the amount of max gold you can carry. |
| addgold | Quality Assurance | `.mod addgold $gold` | Adds gold to your wizard. |
| maxhealth | Quality Assurance | `.mod maxhealth $newHealth` | Changes your base health value. |
| maxmana | Quality Assurance | `.mod maxmana $newMana` | Changes your base mana value. |
| maxenergy | Quality Assurance | `mod maxenergy $newValue` | Changes the maximum energy of your character. |
| currenthealth | Quality Assurance | `.mod currenthealth $newHealth` | Changes your current health value. |
| currentmana | Quality Assurance | `.mod currentmana $newMana` | Changes your current mana value. |
| currentenergy | Quality Assurance | `mod currentenergy $newValue` | Changes the current energy of your character. |
| heal | Quality Assurance | `.mod heal` | Heals your wizard to full. |
| rejuv | Quality Assurance | `.mod rejuv` | Fills your wizard's mana to full. |
| energize | Quality Assurance | `mod energize` | Refills the energy of your character. |
| cantriplevelup | Quality Assurance | `mod cantriplevelup` | Levels up your cantrip level. |
| cantriplevel | Quality Assurance | `mod cantriplevel $level` | Changes your cantrip level. |
| badge | Quality Assurance | `mod badge $locale_id` | Changes your badge. This must be a locale id to function. |


## Spellbook Commands
| Command | Security | Syntax | Description |
| ------- | -------- | ------ | ----------- |
| add | Quality Assurance | `.sb add $spellId` | Adds a temporary spell to your spellbook. |
| remove | Quality Assurance | `.sb remove $spellId` | Removes a temporary spell from your spellbook. |
| learn | Quality Assurance | `.sb learn $spellId` | Permanently learn a new spell. |
| unlearn | Quality Assurance | `.sb unlearn $spellId` | Unlearn a spell. |
| unlearnall | Quality Assurance | `.sb unlearnall` | Unlearns all spells. |


## Debug Commands

| Command | Security | Syntax | Description |
| ------- | -------- | ------ | ----------- |
| gps | Quality Assurance | `.debug gps` | Shows your current location on the server. |
