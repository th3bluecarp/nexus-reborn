# Changelog (Nexus Reborn)

Newest first. "Live" means running on our test server.

## 2026-10-15

- - **Complete Challenges quest objectives** (2824ccf6, live). The two quests that ask you to complete challenges anywhere on Nexus now count them.
- - **Challenge rewards** (9cd25dea, live). Finishing a challenge now earns points on that zone's reward track, and every 300 points (up to 1,200) gives you a piece of your class's challenge gear at your level. Retail let you pick from three bags whose contents were never in the client data. The gear sets themselves are in the data, so the reward is real gear, but which piece you get and the point values are my guesses (logged in SEMANTICS.md). Progress is saved.
- - **Pet abilities without a target** (bdf3ac4d, live). Bot ability buttons pressed with nothing targeted now fire at whatever the pet is already fighting instead of at the pet itself.
- **SetBusy decoded** (e9103c99). 1 marks an activatable object busy, 0 frees it; many objects free themselves after a cooldown. Busy objects can no longer be used by a second player at the same time. The quest object despawn special case now only covers objects whose spells give no rule of their own. Evidence in SEMANTICS.md.
- **Missing quest NPCs in the level 1 to 20 zones** (wildstar-archive, live). An audit found many quest givers and turn-in NPCs never spawned (Galeras about a third, Algoroc, Celestion, Deradune, Ellevar, Auroria 10 to 15 percent). 57 placed so far at their Jabbithole map positions, creature abilities now cover 228 creatures and loot 101. A second download for 145 more missing NPCs is queued.
- **Merge build deployed to the test server** (e89d5aaa, 18:56). All 8 pending migrations applied, 101 custom spawns kept, all 9 server processes up with no errors (Auth, Sts, CharacterAPI, World, Group, Chat, AccountAPI, CharacterServer, FriendshipServer). Everything marked not deployed below is now live.
- **Code review fixes** (edf852a8 on the merge build; the despawn guard also live as 5874231d on reborn/loot). A review of the new systems found traps that never fired, possible duplicate respawns, combo sequences with wrong timing, timed pets removing their replacement, dispels stripping area auras from everyone, stuck item visuals and challenge message spam. All fixed. Still open: mobs do not notice a Stalker who unstealths beside them until hit, overlapping scale effects reset early.
- **Collect and checklist challenges** (97765473, not deployed). Collect challenges (kill or activate) and checklist challenges (activate) work like kill challenges.
- **Challenges** (282813c6, not deployed). Kill challenges like Cavern Clearing and Tremor Ridge Defender now unlock in their area, start on your first kill or from the challenge window, count through bronze, silver and gold, and time out or fail if you leave. No rewards yet.
- **Bank and dye windows** (9ac45026, not deployed, untested). Talking to a banker or dye vendor now sends the message that should open their window.
- **Stuck menu** (af81aaf4, not deployed). The stuck options work: recall to your bind point, recall to your house, or a free suicide.
- **Bind points and Recall** (5ac288ce, not deployed). Using a transmat terminal now binds you there and Recall - Transmat takes you back. The recall tooltip may not show the location name yet.
- **Esper channels and combo attacks** (963c3d81, not deployed). ProxyChannelVariableTime and ProxyLinearAE effects work, which drive Esper Subjugating Blades and Wave Lash and many creature combo attacks.
- **Deploy smoke test passed** (merge build at 50808355, PC). Copies of all 5 databases took every pending migration (upstream ones plus PathMissions) cleanly, the 101 custom spawns survived, and the merge WorldServer started on a test port and RabbitMQ vhost and ran 150 s with no errors. The live server was not touched.
- **Open world public events** (47346471, not deployed). Zone events like Pikefall Expedition and Security Avatar Shutdown now start when you walk up to them, and kills and activations count toward them. Events that relied on special script spawns still can't finish.
- **Path XP** (ac85a872, not deployed). Finished path missions now give path XP (25 + 5 per character level, a tunable guess since retail values are unknown), so path levels and their rewards move.
- **Explorer scavenger hunts** (c335726c, not deployed). Relic hunts now track each clue: activate the relic or reach the dig spot.
- **Explorer cartography** (cb1bcb13, not deployed). Survey missions like Northern Wilds Survey now fill in as you explore the zone map.
- **Path missions** (51fc4df2, not deployed). Your path's missions for each zone now show up and progress: Soldier assassinations, demolitions and rescues, Explorer vistas, claims, exploration and activations, Settler scout, mayor and sheriff missions. Progress is saved. Scientist scans, Settler building and Soldier holdouts are next.
- **World phases** (c5d2afbb, not deployed). ChangePhase effects (portals, rituals) now move you into the other world phase.
- **Percent heals and drains** (82a45c37, not deployed). SapVital effects work on health: levelling up fully heals you, food heals by percent, and percent drains and self-destructs hurt.
- **Taunts and threat drops** (43dffb24, not deployed). ThreatModification effects now taunt, lower threat (Forget, Detaunt), drop you from a threat list or wipe it.
- **Interrupts** (221b271b, not deployed). SpellCounter effects (school interrupts, Head Shot, Suffocate) now interrupt enemy casts once their interrupt armour is broken.
- **Traps, mines and probes** (70d7743d, not deployed). Stalker proximity and tether mines and Medic probes and stations now get placed and fire on the first enemy, or ally for healing probes, that comes close.
- **Life drain** (326e454b, not deployed). Transference effects (Stalker Bloodthirst and Nano Dart, Warrior Sapping Strikes) now damage the target and heal you.
- **Stalker and Esper damage** (22a468a0, not deployed). DistributedDamage and DistanceDependentDamage effects did no damage, which broke Stalker Shred and Eviscerate and Esper Telekinetic Field. They now hit like normal damage.
- **Invulnerability** (ce781a25, not deployed). Invulnerability and energy shield spells (UnitStateSet state 6) now block all damage while they last.
- **Item visual swaps** (8d3a8e2f, not deployed). 1,542 ItemVisualSwap effects now show the carried or wielded item model (quest objects, weapon visuals) and restore the old one afterwards.
- **Logout fix** (live as f2822245 on reborn/loot, 3be7e26f on the merge build). Logging out at the end of the countdown failed with an error and only finished when the game disconnected. The confirm is now accepted.
- **Random proxy spells** (f087144b, not deployed). 195 ProxyRandomExclusive effects now cast one of their weighted spell choices.
- **Pet stances** (9ff33659, not deployed). The pet bar stance buttons work: passive, stay, assist and aggressive.
- **Group kill credit** (48d4bd1b, not deployed). Group members near a kill now get the quest credit and XP too, not only those who hit the creature.
- **Creature abilities** (55e9de65, not deployed). 136 starting zone creatures now fight with the spells players saw them use on retail (archived Jabbithole ability lists), including telegraphed specials like the yeti Claw Slam, instead of a generic punch and jab.
- **Quest creatures despawn once used** (live as 969ad0ed on reborn/loot, a9a68e98 on the merge build). A tester could click the same Trapped Survivor over and over to finish the quest in seconds. Creatures that progress your quest when activated now despawn and come back on the respawn timer. DespawnUnit effects went live with it.
- **Episode prerequisites** (ab3e8e5d, not deployed). Content gated on finishing a zone storyline now unlocks when its quests are done.
- **Combat pets** (pets ca15f9b5 and the commit before it, not deployed). Engineer bots, Esper Geist and Phantoms and Settler reinforcements now spawn, follow you, attack your target, use their ability buttons, and their kills count for you.
- **Stealth** (672a82b8, not deployed). Stealth and reveal effects work, Stalker attacks break stealth, creatures don't aggro on a stealthed player walking past.
- **Dispels and cleanses** (34f5663b, not deployed). 168 SpellDispel effects (Purge, Cleanse, Buff Strip) now remove dispellable buffs or debuffs from the target.
- **Innate passives** (0b2a99c8, not deployed). The stance half of each class innate (Warrior Onslaught or Juggernaut, Engineer modes, Stalker Nano Skins) was never applied. Now cast on login and swapped when the innate changes.
- **EnterArea objectives** (1fd5fd2f, not deployed). 511 "go to this place" objectives only worked in the tutorial. They now complete when the player stands inside the objective's map marker area (166 of the 180 level 1 to 15 ones have a marker). Retail used trigger volumes we don't have, so this is an approximation.
- **Quest objective activations and events** (2e15fb6d, not deployed). Activating a creature progresses Unknown10 objectives for its target groups (the Dominion cannons in Shellshock!, Camp Icefur). Winning a public event progresses CompleteEvent objectives. QuestObjective6 prerequisites check objective completion.
- **Scale spell effects** (a14791ca, not deployed). 531 Scale effects resize server controlled units, timed ones revert.
- **DespawnUnit spell effects** (c023ded4, not deployed). 855 effects remove the target without killing it. World spawns come back after the respawn delay through a new delayed add queue on the map.
- **18 prerequisite types** (5ec9d471, not deployed). IsObjectiveActive and QuestObjective (5,763 uses) were unhandled and always failed, which hid or blocked quest NPCs, objects and abilities. Added those plus objective progress (QuestObjective0 to 5), Health, TrueLevel, Faction, Reputation, ItemOnCharacter, InSubZone, IsPlayer, IsCreature and InTargetGroup.
- **Jabbithole newest snapshots** (wildstar-archive 87f8917, loaded into the test world DB). 1,400 newer pages merged with the oldest pass. Loot tables went from 63 to 64 (Barkblight Horror and Queen added, one dropped for lack of a spawn), spawn pins refreshed. Takes effect on the next server restart.
- **SummonCreature spell effects** (9454b915, not deployed). 1951 summon effects (quest helpers and enemies, boss adds) now summon their creatures around the target at the data distance and angle, timed ones despawn when the effect ends.
- **Absorb shields** (9ead6bd7, not deployed). 581 Absorption effects now give absorb pools, limited to the damage types in the effect mask, that take damage before the shield.
- **QuestAdvanceObjective spell effects** (b8ef0a6e, not deployed). 251 quest item and quest ability effects now advance their quest objective.
- **Quest rewards and CompleteQuest objectives** (634c3f13, not deployed). Reputation and account currency rewards are paid on turn-in, CompleteQuest objectives progress.
- **Use item or ability objectives** (0e8a5d71, not deployed). 147 SpellSuccess objectives progress on successful casts, counting only hits from the objective target group when it has one.
- **Collect item objectives** (d8e79e65, not deployed). 327 CollectItem objectives follow the item count in the bags (and drop back if items leave), items are handed in on turn-in.
- **Heal and HealShields spell effects** (d9dd79d0, not deployed). 3098 heal effects in the client data had no handler, so heals and heal potions did nothing. Uses the damage base formula with the healing multipliers, can crit, capped at missing health or shield, writes a heal combat log.
- **Upstream merge** (1858d2c9, 530731a4, not deployed). Brought in the 34 game_rework commits we were missing: emotes, friends (new Friendship server), /who on the Character server, crowd control, spell, housing, reward and costume messages, socket shutdown fix. Upstream renamed most spell and crowd control messages; our spell code now uses their names with the same wire format. Needs three new server processes and two new databases on deploy.
- **Combat fixes** (f66b2ef4, live). Physical damage used the magic mitigation offset. Corpses were healed to full and sent back to their leash point when combat ended.
- **Creature auto attacks** (6688a157, live). Creatures only ever used their first auto attack. Added a Combat debug log line for every hit involving a player.
- **Loot startup fix** (4306f3a8, live). LootHostedService now starts after the database is initialised.
- **Loot rework** (6630e315, live). Cherry-picked the maintainers' 7 loot-rework commits and fixed the drop roll, which was inverted (a 70 percent item dropped 30 percent of the time).
- **Respawn** (442f9d43, live). Open world creatures respawn after a configurable delay (60 s).
- **Kill XP** (1a5142bd, live). Creatures give XP on kill using a tunable formula, rested XP is spent from the rested pool.
- **Visibility prerequisites** (968d29a3, live). Creature2 visibility prerequisites hide or show creatures, unhandled prerequisite types fail open.
- **Faction fallback** (24c758d3, live). Spawns without a faction use the creature's faction, which fixed "Invalid faction 0" crashes.
- **Veteran start** (d3f7a3c3, live). Nexus start characters get Bon Voyage completed.
- **VirtualCollect** (27d7d0a8, live). Quest objectives fed by the reward pane target group now progress on kill and activate.

## Data (wildstar-archive repo)

- Jabbithole Wayback scrape: 101 starting zone spawns, 7 vendors with 150 items, 63 loot tables with observed drop rates. A newer snapshot pass (1,400 of 1,403 pages, mostly 2017) is merged in.
