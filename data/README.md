# World data

Extra world data for the starting zones and the level 1 to 20 zones, built from archived pages of Jabbithole, the WildStar community database, retrieved through the Internet Archive's Wayback Machine. None of the archived pages are included here, only facts drawn from them.

Thanks to Jabbithole and the players whose uploads built it.

## What is in it

| File | What it adds |
|---|---|
| spawns.sql | 2,711 spawns of 1,581 NPCs, quest objects and Explorer mission objects that quests need but the NexusForever world database does not spawn, mostly quest givers and turn-in NPCs. Entity ids 900000 to 999999. |
| instance_infestation.sql | 132 spawns inside Shiphand: Infestation (world 1232, the X-82 cargo ship) from archived pins of its 2014 version (Salvage Rights), mobs set to level 6. Heights from the map markers. Entity ids 1100000 to 1109999. |
| instance_outpostm13.sql, instance_spacemadness.sql, instance_ragelogic.sql, instance_gauntlet.sql, instance_deepspace.sql, instance_fragmentzero.sql | The other shiphand missions, 123, 269, 137, 233, 244 and 84 spawns, the same way (levels 10, 15, 20, 25, 30 and 50). Entity ids 1110000 to 1169999. Wave creatures stand in place. All of them run their objectives as scripted chains. |
| instance_stormtalon.sql, instance_kelvoreth.sql, instance_skullcano.sql, instance_swordmaiden.sql | The dungeons Stormtalon's Lair, Ruins of Kel Voreth, Skullcano and Sanctuary of the Swordmaiden, 160, 118, 251 and 173 spawns (levels 20, 25, 30 and 35), bosses once each, boss ability objects left out. Entity ids 1170000 to 1209999. Not scripted. |
| loot.sql | Loot tables for 929 creatures, including 59 harvest node types, from the drops Jabbithole recorded per kill, as drops divided by kills plus 5 so creatures seen only a few times do not always drop rare items. Loot group ids from 1000000. Quest items drop only for players with the matching collect objective open, which needs the QuestObjectiveActive loot condition in this fork. |
| settler.sql | 240 Settler expansion entities: depots at improvement build spots that had none, and hub resource nodes (Salvaged Machine Parts and the like) in the hub resource areas on terrain heights. Entity ids 1090000 to 1099999. Built by tools/build_settler.py. |
| item_loot.sql | Contents of loot bag items whose server loot tables are lost, so far the Nexus Survival Kit from the daily login rewards (3 basic medishots and 2 Field Rations, invented). Loot group ids from 2000000. |
| missions.sql | Path mission objects and quest targets missing from the world database (Galeras, Thayd, Farside, Murkmire, Malgrave, Grimvault, Ellevar, Illium), at archived pins where they exist, otherwise at their mission marker or quest location link (guessed). Entity ids 1080000 to 1089999. |
| vendors.sql | Stock for 115 vendors, 4,050 items. Vendors are matched by creature and world. |
| creature_spells.json | Combat spells seen on 1,188 creatures. Instant spells become auto attacks, spells with a cast time are used every 8 to 14 seconds. |

All of it is approximate. NPC positions are player reported map pins with the ground height taken from nearby spawns, or from the terrain in the server map files when no spawn is near (not in Illium and Thayd); quest targets with no page stand at their objective's map marker, drop chances are what players happened to see, and creature abilities are the spells recorded, not the real rotation or conditions. See the "World data from Jabbithole" section of [docs/reborn/SEMANTICS.md](../docs/reborn/SEMANTICS.md).

## Loading it

1. Set up the world database as usual: run the migrations, then the SQL files of [NexusForever.WorldDatabase](https://github.com/NexusForever/NexusForever.WorldDatabase). This data was tested against commit 7397e7f.
2. Load spawns.sql, settler.sql, loot.sql, item_loot.sql, missions.sql and vendors.sql into the world database, after the world database files. Our spawns use fixed ids from 900000, so loading the world database files afterwards would number their entities after ours.
3. Copy creature_spells.json somewhere the world server can read it and set `Realm:CreatureSpellsPath` in WorldServer.json to its path.
4. Restart the world server.

Each SQL file deletes what it added before inserting, so it can be loaded again after an update. To remove the spawns: `DELETE FROM entity_stats WHERE Id BETWEEN 900000 AND 999999; DELETE FROM entity WHERE Id BETWEEN 900000 AND 999999;`

## Rebuilding it

The scripts in [tools](tools) rebuild the files. They expect Python 3.12 and a working folder with the archived pages, JSON dumps of the game tables (`/w/json`) and an export of the world database entity table (`/w/db/entity.tsv`), so they need some path changes to run elsewhere.

* fetch_newest.py asks the Wayback Machine for the newest capture of each NPC and quest page and downloads it, one request at a time.
* parse_jh.py turns the pages into JSON, merge_jh.py merges older and newer captures.
* heights (C#) reads the server .nfmap files and returns terrain height and world area for a list of points. build_spawns.py writes the points it needs to terrain_needed.tsv, reads the answers from terrain_heights.tsv, so run it, then heights, then build_spawns.py again.
* build_spawns.py, build_loot.py, build_vendors.py and build_abilities.py write the files above.
