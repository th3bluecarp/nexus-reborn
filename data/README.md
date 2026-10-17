# World data

Extra world data for the starting zones and the level 1 to 20 zones, built from archived pages of Jabbithole, the WildStar community database, retrieved through the Internet Archive's Wayback Machine. None of the archived pages are included here, only facts drawn from them.

Thanks to Jabbithole and the players whose uploads built it.

## What is in it

| File | What it adds |
|---|---|
| spawns.sql | 2,251 spawns of 1,296 NPCs, quest objects and Explorer mission objects that quests need but the NexusForever world database does not spawn, mostly quest givers and turn-in NPCs. Entity ids 900000 to 999999. |
| loot.sql | Loot tables for 929 creatures, including 59 harvest node types, from the drops Jabbithole recorded per kill, as drops divided by kills plus 5 so creatures seen only a few times do not always drop rare items. Loot group ids from 1000000. Quest items drop only for players with the matching collect objective open, which needs the QuestObjectiveActive loot condition in this fork. |
| vendors.sql | Stock for 115 vendors, 4,050 items. Vendors are matched by creature and world. |
| creature_spells.json | Combat spells seen on 1,188 creatures. Instant spells become auto attacks, spells with a cast time are used every 8 to 14 seconds. |

All of it is approximate. NPC positions are player reported map pins with the ground height taken from nearby spawns, or from the terrain in the server map files when no spawn is near (not in Illium and Thayd); quest targets with no page stand at their objective's map marker, drop chances are what players happened to see, and creature abilities are the spells recorded, not the real rotation or conditions. See the "World data from Jabbithole" section of [docs/reborn/SEMANTICS.md](../docs/reborn/SEMANTICS.md).

## Loading it

1. Set up the world database as usual: run the migrations, then the SQL files of [NexusForever.WorldDatabase](https://github.com/NexusForever/NexusForever.WorldDatabase). This data was tested against commit 7397e7f.
2. Load spawns.sql, loot.sql and vendors.sql into the world database, after the world database files. Our spawns use fixed ids from 900000, so loading the world database files afterwards would number their entities after ours.
3. Copy creature_spells.json somewhere the world server can read it and set `Realm:CreatureSpellsPath` in WorldServer.json to its path.
4. Restart the world server.

Each SQL file deletes what it added before inserting, so it can be loaded again after an update. To remove the spawns: `DELETE FROM entity_stats WHERE Id BETWEEN 900000 AND 999999; DELETE FROM entity WHERE Id BETWEEN 900000 AND 999999;`

## Rebuilding it

The scripts in [tools](tools) rebuild the files. They expect Python 3.12 and a working folder with the archived pages, JSON dumps of the game tables (`/w/json`) and an export of the world database entity table (`/w/db/entity.tsv`), so they need some path changes to run elsewhere.

* fetch_newest.py asks the Wayback Machine for the newest capture of each NPC and quest page and downloads it, one request at a time.
* parse_jh.py turns the pages into JSON, merge_jh.py merges older and newer captures.
* heights (C#) reads the server .nfmap files and returns terrain height and world area for a list of points. build_spawns.py writes the points it needs to terrain_needed.tsv, reads the answers from terrain_heights.tsv, so run it, then heights, then build_spawns.py again.
* build_spawns.py, build_loot.py, build_vendors.py and build_abilities.py write the files above.
