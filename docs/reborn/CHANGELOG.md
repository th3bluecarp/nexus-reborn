# Changelog (Nexus Reborn)

Newest first. "Live" means running on the desktop server.

## 2026-10-15

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

- Jabbithole Wayback scrape: 101 starting zone spawns, 7 vendors with 150 items, 63 loot tables with observed drop rates. A newer snapshot pass (1,403 pages, mostly 2017) is downloading.
