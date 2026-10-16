# Nexus Reborn

A playable fork of [NexusForever](https://github.com/NexusForever/NexusForever), the WildStar server emulator.

The goal here is different from upstream. NexusForever aims to rebuild the retail server accurately. Nexus Reborn aims for something people can sit down and play: quests that finish, paths that progress, challenges that pay out, creatures that fight back. Where the real retail behaviour is unknown, this fork makes a reasonable guess and writes it down instead of leaving the feature out. It is not a 1:1 copy of the original game and does not claim to be.

## How this was built

Most of the work in this fork was written with an AI coding assistant (Claude), directed and play tested by a human. That matters for how you should read it.

* Many game data fields have no documentation. Their meanings here were inferred from patterns in the client tables, and some numbers are simply invented so a feature works.
* Every one of those calls is recorded in [docs/reborn/SEMANTICS.md](docs/reborn/SEMANTICS.md) with the evidence, a confidence level (High, Medium, Low or Invented), the alternatives and what would settle it. If you work on WildStar emulation, treat anything marked Low or Invented as a guess, not as research.
* Special cases that stand in for an unknown generic mechanism are listed at the end of that file so they can be replaced when the real mechanism is understood.

Nothing in this fork has been offered upstream. If something here is useful to NexusForever, they are welcome to it under the license.

## What is different from upstream

The full feature comparison is in [docs/reborn/PARITY.md](docs/reborn/PARITY.md) and the history is in [docs/reborn/CHANGELOG.md](docs/reborn/CHANGELOG.md). Highlights:

* Path missions for Soldier, Explorer and Settler, with path XP
* Zone challenges with timers, tiers and reward tracks that give challenge gear
* Bind points, Recall and the stuck menu
* About 55 more spell effects, including pets, stealth, traps, dispels and threat
* 41 more prerequisite types, more quest objective types and open world event starts
* Group kill credit and combat pet kill credit
* Creature special abilities from an optional data file

## Setup

Setup is the same as upstream, see the [NexusForever server guide](https://www.emulator.ws/installation/server-guide). This fork adds database migrations, so run the migrations as usual after updating.

Optional settings in WorldServer.json under Realm:

* CreatureSpellsPath: a JSON file of combat spells per creature. Without it creatures use their generic attacks.
* PathMissionXp: base and per level path XP per mission (defaults 25 and 5).

The world spawn, loot, vendor and creature ability data is optional, see World data below and [data/README.md](data/README.md).

## World data

Some of what makes the test server playable is not in the client data at all: where missing quest NPCs stood, what creatures dropped and how often, what vendors sold and which abilities creatures used. For that the test server uses data gathered from archived pages of Jabbithole, the WildStar community database, retrieved through the Internet Archive's Wayback Machine. Drop chances are the percentages players reported there, NPC positions come from its map pins, and creature abilities are the spells it recorded, not the real rotations. All of that is approximate and is described in the "World data from Jabbithole" section of [docs/reborn/SEMANTICS.md](docs/reborn/SEMANTICS.md).

The data is in the [data](data) folder with instructions for loading it. The archived pages themselves are not included, only the facts drawn from them. Without it the server still runs, but with only the spawns in the NexusForever world database (so some quest givers are missing), most creatures dropping no loot, vendors that sell only what that database lists, and creatures using only their basic attacks.

Thanks to Jabbithole and the players whose uploads built it. It kept a record of WildStar that would otherwise be gone.

## License

GNU AGPL v3, the same as NexusForever. See [LICENSE](LICENSE). All credit for the emulator this is built on goes to the NexusForever team and contributors.

WildStar is a trademark of NCSOFT. This project is not affiliated with NCSOFT or Carbine Studios.

---

# Upstream NexusForever README

## NexusForever (upstream)
[![Discord](https://img.shields.io/discord/499473932131500034.svg?style=flat&logo=discord)](https://discord.gg/8wT3GEQ)

### Information
A server emulator for WildStar written in C# that supports build 16042.

### Getting Started
[Server Setup Guide](https://www.emulator.ws/installation/server-guide)

### Requirements
 * Visual Studio 2026 (.NET 10 and C# 14 support required)
 * MySQL Server (or equivalent, eg: MariaDB)
 * Message Broker (RabbitMQ or Azure Service Bus)
 * WildStar 16042 client

### Branches
NexusForever has multiple branches:
* **[Master](https://github.com/NexusForever/NexusForever/tree/master)**  
Latest stable release, develop is merged into master once enough content has accumulated in develop.  
Compiled binary releases are based on this branch.
* **[Game Rework](https://github.com/NexusForever/NexusForever/tree/game_rework)**  
Current active development branch, major refactors and updates to the project are underway in this branch.  
All PR's should be targeted to this branch.  
This branch will eventually be merged back into develop.  
* **[Develop](https://github.com/NexusForever/NexusForever/tree/develop)**  
~~Active development branch with the latest features but may be unstable.  
Any new pull requests must be targed towards this branch.~~

### Links
 * [Website](https://emulator.ws)
 * [Discord](https://discord.gg/8wT3GEQ)
 * [World Database](https://github.com/NexusForever/NexusForever.WorldDatabase)

## Build Status
### Windows
Automated builds that will run on Windows or Windows Server.

Master:  
![Master](https://dev.azure.com/NexusForever/NexusForever/_apis/build/status/NexusForever%20Master%20Windows)  
Game Rework:  
![Game Rework](https://dev.azure.com/NexusForever/NexusForever/_apis/build/status/NexusForever%20Develop%20Windows?branchName=game_rework)  
Development:  
![Development](https://dev.azure.com/NexusForever/NexusForever/_apis/build/status/NexusForever%20Develop%20Windows?branchName=develop)
### Linux
Automated builds that will run on various Linux distributions.  
See the [.NET runtime identifer documentation](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog#linux-rids)  for more information on exact distributions.

Master:  
![Master](https://dev.azure.com/NexusForever/NexusForever/_apis/build/status/NexusForever%20Master%20Linux)  
Game Rework:  
![Game Rework](https://dev.azure.com/NexusForever/NexusForever/_apis/build/status/NexusForever%20Develop%20Linux?branchName=game_rework)  
Development:  
![Development](https://dev.azure.com/NexusForever/NexusForever/_apis/build/status/NexusForever%20Develop%20Linux?branchName=develop)
