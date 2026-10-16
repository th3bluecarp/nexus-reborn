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
| Partial | 54 | 44 |
| Missing | 32 | 53 |
| Only on a side branch | 0 | 3 |
| Features where we are ahead | 34 | |

## Coverage counts

| | Ours | Upstream | Total in client data |
|---|---|---|---|
| Spell effect types with a handler | 52 | 16 | 136 |
| Prerequisite types checked | 42 | 17 | 273 |
| Quest objective types that progress | 22 | 9 | 40 |

## What we have that upstream does not

- Kill XP, open world respawn, creature visibility prerequisites, VirtualCollect quest objectives.
- The maintainers' loot rework (only on their side branch upstream), with the inverted drop roll fixed.
- Spell effects: Heal, HealShields, Absorption (absorb shields), SummonCreature, QuestAdvanceObjective, DespawnUnit, Scale, SpellDispel.
- Quest objectives: CollectItem (from bag counts), SpellSuccess 1 to 4 (use item or ability, target group aware), CompleteQuest, CompleteEvent, Unknown10 target group activations, EnterArea from map marker locations. Reputation and account currency quest rewards.
- 18 more prerequisite types, led by IsObjectiveActive and QuestObjective (5,763 uses between them), plus objective progress, health, faction, reputation, item, zone and target checks.
- Class innate passives (Warrior and Engineer stances, Stalker Nano Skin) applied on login and on change.
- Combat fixes: both creature auto attacks used, physical mitigation offset, corpses no longer heal and slide home.
- Veteran start fix (Bon Voyage completed for Nexus start characters).
- Starting zone content from Jabbithole: 101 spawns, 7 vendors with 150 items, 63 loot tables (see the wildstar-archive repo).

## Biggest gaps for levelling 1 to 15

1. Several quest objective types still never progress (EnterArea outside scripts, CompleteEvent, GatheResource, tradeskill ones).
2. Quest rewards still skip tradeskill XP and account items.
3. Creature AI is generic: two auto attacks, fixed 15 m aggro, straight line chase, no leash distance.
4. No taxis, shuttles, bind points, Recall or /stuck.
5. Paths, tradeskills and harvest nodes do nothing.
6. No zone scripting past the arrival cinematics, and open world public events never start.
7. Death resurrects in place instead of at a holocrypt.

The [CHANGELOG](CHANGELOG.md) lists every change we have made.
