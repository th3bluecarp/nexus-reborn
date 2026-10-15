# Changelog (Nexus Reborn)

Newest first. "Live" means running on the desktop server.

## 2026-10-15

- **Dispels and cleanses** (c14b1967, not deployed). 168 SpellDispel effects (Purge, Cleanse, Buff Strip) now remove dispellable buffs or debuffs from the target.
- **Innate passives** (2031daa9, not deployed). The stance half of each class innate (Warrior Onslaught or Juggernaut, Engineer modes, Stalker Nano Skins) was never applied. Now cast on login and swapped when the innate changes.
- **EnterArea objectives** (11ae97eb, not deployed). 511 "go to this place" objectives only worked in the tutorial. They now complete when the player stands inside the objective's map marker area (166 of the 180 level 1 to 15 ones have a marker). Retail used trigger volumes we don't have, so this is an approximation.
- **Quest objective activations and events** (8ef987c4, not deployed). Activating a creature progresses Unknown10 objectives for its target groups (the Dominion cannons in Shellshock!, Camp Icefur). Winning a public event progresses CompleteEvent objectives. QuestObjective6 prerequisites check objective completion.
- **Scale spell effects** (15256f1b, not deployed). 531 Scale effects resize server controlled units, timed ones revert.
- **DespawnUnit spell effects** (6d95305e, not deployed). 855 effects remove the target without killing it. World spawns come back after the respawn delay through a new delayed add queue on the map.
- **18 prerequisite types** (b34e47bc, not deployed). IsObjectiveActive and QuestObjective (5,763 uses) were unhandled and always failed, which hid or blocked quest NPCs, objects and abilities. Added those plus objective progress (QuestObjective0 to 5), Health, TrueLevel, Faction, Reputation, ItemOnCharacter, InSubZone, IsPlayer, IsCreature and InTargetGroup.
- **Jabbithole newest snapshots** (wildstar-archive 87f8917, loaded into the desktop world DB). 1,400 newer pages merged with the oldest pass. Loot tables went from 63 to 64 (Barkblight Horror and Queen added, one dropped for lack of a spawn), spawn pins refreshed. Takes effect on the next server restart.
- **SummonCreature spell effects** (a5d22eac, not deployed). 1951 summon effects (quest helpers and enemies, boss adds) now summon their creatures around the target at the data distance and angle, timed ones despawn when the effect ends.
- **Absorb shields** (2d822f91, not deployed). 581 Absorption effects now give absorb pools, limited to the damage types in the effect mask, that take damage before the shield.
- **QuestAdvanceObjective spell effects** (c4f5140b, not deployed). 251 quest item and quest ability effects now advance their quest objective.
- **Quest rewards and CompleteQuest objectives** (6565b16c, not deployed). Reputation and account currency rewards are paid on turn-in, CompleteQuest objectives progress.
- **Use item or ability objectives** (481039da, not deployed). 147 SpellSuccess objectives progress on successful casts, counting only hits from the objective target group when it has one.
- **Collect item objectives** (1655238f, not deployed). 327 CollectItem objectives follow the item count in the bags (and drop back if items leave), items are handed in on turn-in.
- **Heal and HealShields spell effects** (dc88210b, not deployed). 3098 heal effects in the client data had no handler, so heals and heal potions did nothing. Uses the damage base formula with the healing multipliers, can crit, capped at missing health or shield, writes a heal combat log.
- **Upstream merge** (69686742, 0d34d66b, not deployed). Brought in the 34 game_rework commits we were missing: emotes, friends (new Friendship server), /who on the Character server, crowd control, spell, housing, reward and costume messages, socket shutdown fix. Upstream renamed most spell and crowd control messages; our spell code now uses their names with the same wire format. Needs three new server processes and two new databases on deploy.
- **Combat fixes** (c4b27514, live). Physical damage used the magic mitigation offset. Corpses were healed to full and sent back to their leash point when combat ended.
- **Creature auto attacks** (3bc1325a, live). Creatures only ever used their first auto attack. Added a Combat debug log line for every hit involving a player.
- **Loot startup fix** (6dcd5dbd, live). LootHostedService now starts after the database is initialised.
- **Loot rework** (a51058cf, live). Cherry-picked the maintainers' 7 loot-rework commits and fixed the drop roll, which was inverted (a 70 percent item dropped 30 percent of the time).
- **Respawn** (37a69982, live). Open world creatures respawn after a configurable delay (60 s).
- **Kill XP** (80691080, live). Creatures give XP on kill using a tunable formula, rested XP is spent from the rested pool.
- **Visibility prerequisites** (9c706a61, live). Creature2 visibility prerequisites hide or show creatures, unhandled prerequisite types fail open.
- **Faction fallback** (82dd01a5, live). Spawns without a faction use the creature's faction, which fixed "Invalid faction 0" crashes.
- **Veteran start** (e7995052, live). Nexus start characters get Bon Voyage completed.
- **VirtualCollect** (53f29907, live). Quest objectives fed by the reward pane target group now progress on kill and activate.

## Data (wildstar-archive repo)

- Jabbithole Wayback scrape: 101 starting zone spawns, 7 vendors with 150 items, 63 loot tables with observed drop rates. A newer snapshot pass (1,400 of 1,403 pages, mostly 2017) is merged in.
