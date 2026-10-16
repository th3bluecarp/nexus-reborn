# Changelog (Nexus Reborn)

Newest first. "Live" means running on the desktop server.

## 2026-10-15

- **Code review fixes** (7eb306cf on the merge build; the despawn guard also live as 3001f002 on reborn/loot). A review of the new systems found traps that never fired, possible duplicate respawns, combo sequences with wrong timing, timed pets removing their replacement, dispels stripping area auras from everyone, stuck item visuals and challenge message spam. All fixed. Still open: mobs do not notice a Stalker who unstealths beside them until hit, overlapping scale effects reset early.
- **Collect and checklist challenges** (f8600371, not deployed). Collect challenges (kill or activate) and checklist challenges (activate) work like kill challenges.
- **Challenges** (d5741402, not deployed). Kill challenges like Cavern Clearing and Tremor Ridge Defender now unlock in their area, start on your first kill or from the challenge window, count through bronze, silver and gold, and time out or fail if you leave. No rewards yet.
- **Bank and dye windows** (46cc9d88, not deployed, untested). Talking to a banker or dye vendor now sends the message that should open their window.
- **Stuck menu** (834e88cd, not deployed). The stuck options work: recall to your bind point, recall to your house, or a free suicide.
- **Bind points and Recall** (d497a029, not deployed). Using a transmat terminal now binds you there and Recall - Transmat takes you back. The recall tooltip may not show the location name yet.
- **Esper channels and combo attacks** (98d85604, not deployed). ProxyChannelVariableTime and ProxyLinearAE effects work, which drive Esper Subjugating Blades and Wave Lash and many creature combo attacks.
- **Deploy smoke test passed** (merge build at 72440c1b, PC). Copies of all 5 databases took every pending migration (upstream ones plus PathMissions) cleanly, the 101 custom spawns survived, and the merge WorldServer started on a test port and RabbitMQ vhost and ran 150 s with no errors. The live server was not touched.
- **Open world public events** (338e3e3b, not deployed). Zone events like Pikefall Expedition and Security Avatar Shutdown now start when you walk up to them, and kills and activations count toward them. Events that relied on special script spawns still can't finish.
- **Path XP** (5d7a68db, not deployed). Finished path missions now give path XP (25 + 5 per character level, a tunable guess since retail values are unknown), so path levels and their rewards move.
- **Explorer scavenger hunts** (65ac695b, not deployed). Relic hunts now track each clue: activate the relic or reach the dig spot.
- **Explorer cartography** (b16868f7, not deployed). Survey missions like Northern Wilds Survey now fill in as you explore the zone map.
- **Path missions** (dab0aafd, not deployed). Your path's missions for each zone now show up and progress: Soldier assassinations, demolitions and rescues, Explorer vistas, claims, exploration and activations, Settler scout, mayor and sheriff missions. Progress is saved. Scientist scans, Settler building and Soldier holdouts are next.
- **World phases** (acd6d27b, not deployed). ChangePhase effects (portals, rituals) now move you into the other world phase.
- **Percent heals and drains** (ee533d33, not deployed). SapVital effects work on health: levelling up fully heals you, food heals by percent, and percent drains and self-destructs hurt.
- **Taunts and threat drops** (2fa12fb3, not deployed). ThreatModification effects now taunt, lower threat (Forget, Detaunt), drop you from a threat list or wipe it.
- **Interrupts** (df39f928, not deployed). SpellCounter effects (school interrupts, Head Shot, Suffocate) now interrupt enemy casts once their interrupt armour is broken.
- **Traps, mines and probes** (a954a959, not deployed). Stalker proximity and tether mines and Medic probes and stations now get placed and fire on the first enemy, or ally for healing probes, that comes close.
- **Life drain** (894de141, not deployed). Transference effects (Stalker Bloodthirst and Nano Dart, Warrior Sapping Strikes) now damage the target and heal you.
- **Stalker and Esper damage** (dcdae4ad, not deployed). DistributedDamage and DistanceDependentDamage effects did no damage, which broke Stalker Shred and Eviscerate and Esper Telekinetic Field. They now hit like normal damage.
- **Invulnerability** (011c687e, not deployed). Invulnerability and energy shield spells (UnitStateSet state 6) now block all damage while they last.
- **Item visual swaps** (581acdcc, not deployed). 1,542 ItemVisualSwap effects now show the carried or wielded item model (quest objects, weapon visuals) and restore the old one afterwards.
- **Logout fix** (live as 0fff3356 on reborn/loot, dfa9ed7a on the merge build). Logging out at the end of the countdown failed with an error and only finished when the game disconnected. The confirm is now accepted.
- **Random proxy spells** (7c5fe043, not deployed). 195 ProxyRandomExclusive effects now cast one of their weighted spell choices.
- **Pet stances** (1cd8fc5c, not deployed). The pet bar stance buttons work: passive, stay, assist and aggressive.
- **Group kill credit** (27811a61, not deployed). Group members near a kill now get the quest credit and XP too, not only those who hit the creature.
- **Creature abilities** (dc6f14a3, not deployed). 136 starting zone creatures now fight with the spells players saw them use on retail (archived Jabbithole ability lists), including telegraphed specials like the yeti Claw Slam, instead of a generic punch and jab.
- **Quest creatures despawn once used** (live as 1ef1295d on reborn/loot, 7271a206 on the merge build). A tester could click the same Trapped Survivor over and over to finish the quest in seconds. Creatures that progress your quest when activated now despawn and come back on the respawn timer. DespawnUnit effects went live with it.
- **Episode prerequisites** (1667b3b6, not deployed). Content gated on finishing a zone storyline now unlocks when its quests are done.
- **Combat pets** (pets 65cc0119 and the commit before it, not deployed). Engineer bots, Esper Geist and Phantoms and Settler reinforcements now spawn, follow you, attack your target, use their ability buttons, and their kills count for you.
- **Stealth** (fed7c120, not deployed). Stealth and reveal effects work, Stalker attacks break stealth, creatures don't aggro on a stealthed player walking past.
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
