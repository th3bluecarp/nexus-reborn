# WildStar parity tracker

Goal: get this server as close to retail WildStar (1:1) as we can, starting with levels 1 to 15.
The full feature list is in [parity.csv](parity.csv), which Gitea shows as a sortable table.

Columns in parity.csv:
- **reborn**: status on our branch. "(not deployed)" means merged and built but not yet running on the desktop server.
- **upstream**: status on upstream NexusForever `game_rework`.
- **ahead**: yes when we have something upstream does not.
- **commits**: our commits for that row.
- **priority_1_15**: high when it matters for levelling 1 to 15 and is not done yet.

## Scoreboard (153 features)

| | Ours | Upstream |
|---|---|---|
| Implemented | 66 | 50 |
| Partial | 55 | 44 |
| Missing | 31 | 53 |
| Only on a side branch | 0 | 3 |
| Features where we are ahead | 33 | |

## Coverage counts

| | Ours | Upstream | Total in client data |
|---|---|---|---|
| Spell effect types with a handler | 52 | 16 | 136 |
| Prerequisite types checked | 42 | 17 | 273 |
| Quest objective types that progress | 22 | 9 | 40 |

## What we have that upstream does not

Generated from parity.csv (rows marked ahead), 33 features.

- **Character:** Levelling and XP.
- **Movement:** Bind point and Recall (transmat to bindpoint), Stuck command.
- **Spells:** Healing spells, Absorb and shield spells, Innate abilities and passives, Class pets (Engineer bots, Esper), Stealth (Stalker), Traps, mines and probes, Despawn and scale effects, Dispels and cleanses.
- **Spawns:** Open world respawn, Visibility by prerequisite, World phases.
- **Quests:** Quest turn-in and rewards, Collect item objectives, Virtual collect (loot pane) objectives, Use item / ability objectives, Enter area / zone objectives, Other objective types, Quest objective prerequisites, Episodes.
- **Public Events:** Open world public events.
- **Loot:** Creature loot generation, Group loot rules, Loot bags and boxes.
- **Items/Inventory:** Use consumables, Bank.
- **Paths:** Path missions.
- **Social (chat/friends/guild/group):** Group XP and quest sharing.
- **Achievements:** Challenges.
- **Reputation:** Reputation standings, Reputation and faction prerequisites.
- Starting zone content from Jabbithole: 101 spawns, 7 vendors with 150 items, 64 loot tables, observed creature abilities for 136 creatures (see the wildstar-archive repo).

## Biggest gaps for levelling 1 to 15

1. Nothing on the merge build has been played yet. It passed a startup smoke test on database copies but needs a tester to deploy and test in game.
2. Taxis and shuttles need packet research (no message layout for the flight path window).
3. Tradeskills, gathering and crafting are missing (data for auto-learned schematics and XP is unclear).
4. Creature AI still has a fixed 15 m aggro, straight line chase and no distance leash; per creature abilities only cover 136 starting zone creatures until the level 1 to 15 Jabbithole pass finishes.
5. Path missions miss Scientist scans, Settler building, Soldier holdouts and Explorer tracking.
6. Challenges give no rewards, and open world events that need script-spawned creatures cannot finish.
7. Death resurrects in place instead of at a holocrypt (no holocrypt locations in the data).

The [CHANGELOG](CHANGELOG.md) lists every change we have made.
