# Changelog (Nexus Reborn)

Newest first. "Live" means running on our test server.

## 2026-10-15

- - **Defensive pet stance** (4d78731f, live). Pets on Defensive now protect you, going after whatever is attacking you instead of copying your target, which is what Assist does.
- - **Exploration XP** (08a64f7d, live). Revealing a new part of the zone map now gives a little XP, about a fifth of a quest's base XP for your level. Retail had exploration XP but the amount is a guess.
- - **Named quest targets spawn once** (8cdb7634, live, published in data/). Named creatures like Golthox and Firewalker Jarrok were spawning up to 12 times because players had pinned them in many places. Targets now spawn as many times as their quest asks for. A third download for quest NPCs never searched brought the world to 874 extra spawns of 489 creatures, with 264 quest givers and turn-ins placed.
- - **Missing quest NPCs, second pass** (410fbcef, live, published in data/). A download of 254 pages for the quest givers and objects still missing brought spawns from 387 to 751, covering 329 creatures the world did not have at all. Vendors 115 with 4,050 items.
- - **World data from the level 1 to 20 Jabbithole download** (647c32b8, live, and published in data/). NPC spawns went from 158 to 387 (more missing quest givers and objects), vendors from 7 to 108 stocking 3,987 items, loot from 101 to 115 creatures, creature abilities from 228 to 1,167 creatures. Checked on a fresh NexusForever world database first, no errors.
- - **Holocrypt respawn** (853ca8ab, live). When you die in the open world you now get the holocrypt option, which brings you back at full health at the nearest friendly transmat. Retail's holocrypt locations were never in the client data, so the transmat stands in for them. Wake here still works as before.
- - **Aggro by level and calls for help** (37b49d2f, live). Creatures well below your level now notice you from closer, and ones 8 or more levels below leave you alone. Pulling one creature in a camp brings its neighbours within 10 m, but not the whole camp. The numbers are guesses.
- - **Leashing** (8709f911, live). Creatures no longer chase you across the zone. Past 50 m from where the fight started they give up, heal and walk home, and they ignore players they walk past on the way. The range is a guess.
- - **Complete Challenges quest objectives** (55bb6a05, live). The two quests that ask you to complete challenges anywhere on Nexus now count them.
- - **Challenge rewards** (78e399bb, live). Finishing a challenge now earns points on that zone's reward track, and every 300 points (up to 1,200) gives you a piece of your class's challenge gear at your level. Retail let you pick from three bags whose contents were never in the client data. The gear sets themselves are in the data, so the reward is real gear, but which piece you get and the point values are my guesses (logged in SEMANTICS.md). Progress is saved.
- - **Pet abilities without a target** (c407e270, live). Bot ability buttons pressed with nothing targeted now fire at whatever the pet is already fighting instead of at the pet itself.
- **SetBusy decoded** (fbc02afc). 1 marks an activatable object busy, 0 frees it; many objects free themselves after a cooldown. Busy objects can no longer be used by a second player at the same time. The quest object despawn special case now only covers objects whose spells give no rule of their own. Evidence in SEMANTICS.md.
- **Missing quest NPCs in the level 1 to 20 zones** (wildstar-archive, live). An audit found many quest givers and turn-in NPCs never spawned (Galeras about a third, Algoroc, Celestion, Deradune, Ellevar, Auroria 10 to 15 percent). 57 placed so far at their Jabbithole map positions, creature abilities now cover 228 creatures and loot 101. A second download for 145 more missing NPCs is queued.
- **Merge build deployed to the test server** (67f08ca5, 18:56). All 8 pending migrations applied, 101 custom spawns kept, all 9 server processes up with no errors (Auth, Sts, CharacterAPI, World, Group, Chat, AccountAPI, CharacterServer, FriendshipServer). Everything marked not deployed below is now live.
- **Code review fixes** (d85cd98d on the merge build; the despawn guard also live as 493f2aa0 on reborn/loot). A review of the new systems found traps that never fired, possible duplicate respawns, combo sequences with wrong timing, timed pets removing their replacement, dispels stripping area auras from everyone, stuck item visuals and challenge message spam. All fixed. Still open: mobs do not notice a Stalker who unstealths beside them until hit, overlapping scale effects reset early.
- **Collect and checklist challenges** (52c38132, not deployed). Collect challenges (kill or activate) and checklist challenges (activate) work like kill challenges.
- **Challenges** (4421510f, not deployed). Kill challenges like Cavern Clearing and Tremor Ridge Defender now unlock in their area, start on your first kill or from the challenge window, count through bronze, silver and gold, and time out or fail if you leave. No rewards yet.
- **Bank and dye windows** (67f79733, not deployed, untested). Talking to a banker or dye vendor now sends the message that should open their window.
- **Stuck menu** (fe48830e, not deployed). The stuck options work: recall to your bind point, recall to your house, or a free suicide.
- **Bind points and Recall** (09e1de83, not deployed). Using a transmat terminal now binds you there and Recall - Transmat takes you back. The recall tooltip may not show the location name yet.
- **Esper channels and combo attacks** (f21b3db6, not deployed). ProxyChannelVariableTime and ProxyLinearAE effects work, which drive Esper Subjugating Blades and Wave Lash and many creature combo attacks.
- **Deploy smoke test passed** (merge build at 61850ecd, PC). Copies of all 5 databases took every pending migration (upstream ones plus PathMissions) cleanly, the 101 custom spawns survived, and the merge WorldServer started on a test port and RabbitMQ vhost and ran 150 s with no errors. The live server was not touched.
- **Open world public events** (cd8d1575, not deployed). Zone events like Pikefall Expedition and Security Avatar Shutdown now start when you walk up to them, and kills and activations count toward them. Events that relied on special script spawns still can't finish.
- **Path XP** (582bf849, not deployed). Finished path missions now give path XP (25 + 5 per character level, a tunable guess since retail values are unknown), so path levels and their rewards move.
- **Explorer scavenger hunts** (b81f8ee9, not deployed). Relic hunts now track each clue: activate the relic or reach the dig spot.
- **Explorer cartography** (3965b625, not deployed). Survey missions like Northern Wilds Survey now fill in as you explore the zone map.
- **Path missions** (e9827cc3, not deployed). Your path's missions for each zone now show up and progress: Soldier assassinations, demolitions and rescues, Explorer vistas, claims, exploration and activations, Settler scout, mayor and sheriff missions. Progress is saved. Scientist scans, Settler building and Soldier holdouts are next.
- **World phases** (00df22ca, not deployed). ChangePhase effects (portals, rituals) now move you into the other world phase.
- **Percent heals and drains** (f4450c84, not deployed). SapVital effects work on health: levelling up fully heals you, food heals by percent, and percent drains and self-destructs hurt.
- **Taunts and threat drops** (1ff69a1c, not deployed). ThreatModification effects now taunt, lower threat (Forget, Detaunt), drop you from a threat list or wipe it.
- **Interrupts** (b4aa206e, not deployed). SpellCounter effects (school interrupts, Head Shot, Suffocate) now interrupt enemy casts once their interrupt armour is broken.
- **Traps, mines and probes** (0049b398, not deployed). Stalker proximity and tether mines and Medic probes and stations now get placed and fire on the first enemy, or ally for healing probes, that comes close.
- **Life drain** (957ee3cd, not deployed). Transference effects (Stalker Bloodthirst and Nano Dart, Warrior Sapping Strikes) now damage the target and heal you.
- **Stalker and Esper damage** (d8c8ecf6, not deployed). DistributedDamage and DistanceDependentDamage effects did no damage, which broke Stalker Shred and Eviscerate and Esper Telekinetic Field. They now hit like normal damage.
- **Invulnerability** (657e1f74, not deployed). Invulnerability and energy shield spells (UnitStateSet state 6) now block all damage while they last.
- **Item visual swaps** (3280d5f4, not deployed). 1,542 ItemVisualSwap effects now show the carried or wielded item model (quest objects, weapon visuals) and restore the old one afterwards.
- **Logout fix** (live as 4a406f39 on reborn/loot, 2c5b07c9 on the merge build). Logging out at the end of the countdown failed with an error and only finished when the game disconnected. The confirm is now accepted.
- **Random proxy spells** (e4c39097, not deployed). 195 ProxyRandomExclusive effects now cast one of their weighted spell choices.
- **Pet stances** (2012f07c, not deployed). The pet bar stance buttons work: passive, stay, assist and aggressive.
- **Group kill credit** (da4e5610, not deployed). Group members near a kill now get the quest credit and XP too, not only those who hit the creature.
- **Creature abilities** (f316daa5, not deployed). 136 starting zone creatures now fight with the spells players saw them use on retail (archived Jabbithole ability lists), including telegraphed specials like the yeti Claw Slam, instead of a generic punch and jab.
- **Quest creatures despawn once used** (live as 03128ca8 on reborn/loot, 60b09a97 on the merge build). A tester could click the same Trapped Survivor over and over to finish the quest in seconds. Creatures that progress your quest when activated now despawn and come back on the respawn timer. DespawnUnit effects went live with it.
- **Episode prerequisites** (0f27d857, not deployed). Content gated on finishing a zone storyline now unlocks when its quests are done.
- **Combat pets** (pets 38700856 and the commit before it, not deployed). Engineer bots, Esper Geist and Phantoms and Settler reinforcements now spawn, follow you, attack your target, use their ability buttons, and their kills count for you.
- **Stealth** (a599103e, not deployed). Stealth and reveal effects work, Stalker attacks break stealth, creatures don't aggro on a stealthed player walking past.
- **Dispels and cleanses** (00c5111f, not deployed). 168 SpellDispel effects (Purge, Cleanse, Buff Strip) now remove dispellable buffs or debuffs from the target.
- **Innate passives** (8c692483, not deployed). The stance half of each class innate (Warrior Onslaught or Juggernaut, Engineer modes, Stalker Nano Skins) was never applied. Now cast on login and swapped when the innate changes.
- **EnterArea objectives** (d4fe7cb5, not deployed). 511 "go to this place" objectives only worked in the tutorial. They now complete when the player stands inside the objective's map marker area (166 of the 180 level 1 to 15 ones have a marker). Retail used trigger volumes we don't have, so this is an approximation.
- **Quest objective activations and events** (cece25b3, not deployed). Activating a creature progresses Unknown10 objectives for its target groups (the Dominion cannons in Shellshock!, Camp Icefur). Winning a public event progresses CompleteEvent objectives. QuestObjective6 prerequisites check objective completion.
- **Scale spell effects** (946a17b5, not deployed). 531 Scale effects resize server controlled units, timed ones revert.
- **DespawnUnit spell effects** (3e436ec7, not deployed). 855 effects remove the target without killing it. World spawns come back after the respawn delay through a new delayed add queue on the map.
- **18 prerequisite types** (4f5ffc66, not deployed). IsObjectiveActive and QuestObjective (5,763 uses) were unhandled and always failed, which hid or blocked quest NPCs, objects and abilities. Added those plus objective progress (QuestObjective0 to 5), Health, TrueLevel, Faction, Reputation, ItemOnCharacter, InSubZone, IsPlayer, IsCreature and InTargetGroup.
- **Jabbithole newest snapshots** (wildstar-archive 87f8917, loaded into the test world DB). 1,400 newer pages merged with the oldest pass. Loot tables went from 63 to 64 (Barkblight Horror and Queen added, one dropped for lack of a spawn), spawn pins refreshed. Takes effect on the next server restart.
- **SummonCreature spell effects** (96370273, not deployed). 1951 summon effects (quest helpers and enemies, boss adds) now summon their creatures around the target at the data distance and angle, timed ones despawn when the effect ends.
- **Absorb shields** (3494a370, not deployed). 581 Absorption effects now give absorb pools, limited to the damage types in the effect mask, that take damage before the shield.
- **QuestAdvanceObjective spell effects** (9b5084aa, not deployed). 251 quest item and quest ability effects now advance their quest objective.
- **Quest rewards and CompleteQuest objectives** (b5f82e46, not deployed). Reputation and account currency rewards are paid on turn-in, CompleteQuest objectives progress.
- **Use item or ability objectives** (136aa952, not deployed). 147 SpellSuccess objectives progress on successful casts, counting only hits from the objective target group when it has one.
- **Collect item objectives** (131e1f0f, not deployed). 327 CollectItem objectives follow the item count in the bags (and drop back if items leave), items are handed in on turn-in.
- **Heal and HealShields spell effects** (ef97b8fd, not deployed). 3098 heal effects in the client data had no handler, so heals and heal potions did nothing. Uses the damage base formula with the healing multipliers, can crit, capped at missing health or shield, writes a heal combat log.
- **Upstream merge** (fcc50297, eaa28541, not deployed). Brought in the 34 game_rework commits we were missing: emotes, friends (new Friendship server), /who on the Character server, crowd control, spell, housing, reward and costume messages, socket shutdown fix. Upstream renamed most spell and crowd control messages; our spell code now uses their names with the same wire format. Needs three new server processes and two new databases on deploy.
- **Combat fixes** (3048f590, live). Physical damage used the magic mitigation offset. Corpses were healed to full and sent back to their leash point when combat ended.
- **Creature auto attacks** (0d4ae95d, live). Creatures only ever used their first auto attack. Added a Combat debug log line for every hit involving a player.
- **Loot startup fix** (df45768f, live). LootHostedService now starts after the database is initialised.
- **Loot rework** (7bf10219, live). Cherry-picked the maintainers' 7 loot-rework commits and fixed the drop roll, which was inverted (a 70 percent item dropped 30 percent of the time).
- **Respawn** (7b3a7113, live). Open world creatures respawn after a configurable delay (60 s).
- **Kill XP** (7e211961, live). Creatures give XP on kill using a tunable formula, rested XP is spent from the rested pool.
- **Visibility prerequisites** (69383e95, live). Creature2 visibility prerequisites hide or show creatures, unhandled prerequisite types fail open.
- **Faction fallback** (ec34f223, live). Spawns without a faction use the creature's faction, which fixed "Invalid faction 0" crashes.
- **Veteran start** (4e45363e, live). Nexus start characters get Bon Voyage completed.
- **VirtualCollect** (f6f20589, live). Quest objectives fed by the reward pane target group now progress on kill and activate.

## Data (wildstar-archive repo)

- Jabbithole Wayback scrape: 101 starting zone spawns, 7 vendors with 150 items, 63 loot tables with observed drop rates. A newer snapshot pass (1,400 of 1,403 pages, mostly 2017) is merged in.
