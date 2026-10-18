# Semantics ledger

Every meaning we assigned to an undocumented field, enum value or mechanic, with the evidence, how sure we are, what else it could be and what would settle it. The code follows these entries; when one turns out wrong, fix the entry first, then the code it names.

Confidence: **High** = consistent across all the data we checked and no counter examples. **Medium** = consistent with the examples we looked at, alternatives not ruled out. **Low** = a guess that makes something playable, expect to revisit. **Invented** = no data at all, a tunable number.

## Prerequisites

| Field | Our meaning | Confidence | Evidence | Alternatives / risks | What would settle it | Code |
|---|---|---|---|---|---|---|
| IsObjectiveActive (43) | ObjectId quest, Value objective index; met while the quest is accepted and that objective is incomplete and unlocked (sequential) | Medium | 3,631 uses; prerequisite 4620 pairs it with QuestObjective(same index, NotEqual) | "active" may also cover achieved quests or ignore sequencing; ObjectId 0 (context quest) unhandled | client Lua or binary use of prerequisite 43; NPCs that should appear mid quest | PrerequisiteCheckIsObjectiveActive |
| QuestObjective (68) | ObjectId quest, Value objective index; Equal = that objective is complete | Medium | 4701: obj0 Equal and obj1 NotEqual reads as "first step done, second not" | could mean "objective has any progress" | find a case where partial progress matters | PrerequisiteCheckQuestObjective |
| QuestObjective6 (94) | same as QuestObjective | Low | one creature priority prerequisite (8548) and 4220 read sensibly that way | why two type ids would mean the same thing is unexplained; may differ for checklists or optional objectives | compare all 781 uses against 68 by context | PrerequisiteCheckQuestObjective6 |
| QuestObjective0..5 (22, 23, 33-36) | progress of objective N compared with Value | Medium | numeric comparisons (<=, >) only make sense against a count | objective N might be by objective id order, not index | quests where index and id order differ | PrerequisiteCheckQuestObjective0-5 |
| Health (40) | the evaluated unit's health percent | Medium | values 10, 25, 30, 50, 75 with numeric comparisons | may refer to the target, not the player | spells gated on target health | PrerequisiteCheckHealth |
| IsCreature (38), InTargetGroup (44) | about parameters.Target; no target keeps the caller's default | Medium | Value is a creature id / target group id | most callers pass no target, so these rarely do anything yet | | |
| Unknown50 | UnderSpell on the prerequisite target, else the player | Medium | 450 uses, Value a Spell4 id (336 of 352), Equal/NotEqual, same shape as UnderSpell 15 | may be the caster, not the target | PrerequisiteCheckTargetUnderSpell |
| Unknown71 | ObjectId a Vital, Value a percentage of its maximum | Medium | 166 uses, ObjectIds 1, 3, 6-9, 15 match Health, ShieldCapacity, resources, Focus; values 0-100, numeric comparisons | resource maximum property is a guess | PrerequisiteCheckVitalPercent |
| PublicEvent117 | ObjectId a PublicEvent, Value 1 running on the map, 0 not | Medium | 458 uses, ObjectId a PublicEvent id in 457, Values 0 and 1, Equal/NotEqual | value might be a state rather than a flag | PrerequisiteCheckPublicEvent |
| PublicEventObjective188 (118) | ObjectId a PublicEventObjective, Value its PublicEventStatus | High | 2,422 uses, ObjectId an objective id in 2,392, Values 0-3 line up with Inactive, Active, Succeeded, Failed | | PrerequisiteCheckPublicEventObjective |
| Inventory (246) | Value an Item2 id, NotEqual = the player does not carry it | High | 793 uses, all NotEqual, Value an item in 791 (hoverboards, bags, titles) | ObjectId on 60 rows unknown, ignored | PrerequisiteCheckInventory |
| TradeSkillProfession (19) | ObjectId a TradeskillTier, 1 when that tier of its tradeskill is reached | Medium | 268 uses, ObjectIds are tier ids, Value 0 with GreaterThan/LessThanOrEqual or 1 with Equal | could mean the tier is the current one, not reached | PrerequisiteCheckTradeskillProfession |
| EpisodeState (9) | Value is a QuestState over the episode's quests (3 = all completed) | Medium | 31 of 34 uses are Value 3 | how "accepted" is defined for an episode is a guess | | PrerequisiteCheckEpisodeState |
| Spell59 | **not implemented**: ObjectId a Spell4 id (253 of 260), Value 1 to 5 with numeric comparisons, probably a stack count; no way to count stacks yet | | | | |
| Unknown47, ChallengeRequirement (69), ChallengeLocked (74), PlayerPathMission (54), PathMissionRequirement (67) | **not implemented** | | Value is a spell / challenge / mission id | several readings fit equally well | | unhandled on purpose |

## Quest objectives

| Field | Our meaning | Confidence | Evidence | Alternatives / risks | Code |
|---|---|---|---|---|---|
| Unknown10 | Data is a target group; progress when the player activates a creature in it | Low | Shellshock! cannons (TG 1322 = creature 11251, activate spell on it) and the Roll Call Sheet | 52 objectives, some describe kills or challenges; Data is not always a target group (18876, 30840) | WorldEntity.OnActivateSuccess |
| ActivateEntity (5), SucceedCSI (12), KillCreature (2) with Data 0 | the one creature named in the objective text, $(creature=N) | High | 274 of 1,267 ActivateEntity, 15 of 48 SucceedCSI and 1 of 26 KillCreature such objectives name exactly one creature, e.g. Activate the $(creature=27196) (Ship Controls) | text naming several creatures is left alone | QuestObjectiveInfo.Data |
| ActivateEntity (5) with Data 0 and no creature in text | the creatures of TargetGroupIdRewardPane, counted on activation | Medium | 158 such objectives have a reward pane group, 50 of creatures with an activate spell (Rescue Exile Prisoners: Imprisoned Survivor) | groups of creatures without an activate spell are probably kills or item use, not handled | Quest.RewardPaneObjectiveUpdate |
| ActivateEntity (5) with no target but an indicator location | reach the location, like EnterArea | Low | 261 such objectives, text reads Follow / Investigate / Go to | some may have wanted a click on an unlisted object | QuestManager.UpdateAreaObjectives |
| EnterArea (22) | complete when inside the objective's indicator world location (radius at least 10 m) | Low | 166 of 180 low level objectives have an indicator | retail used trigger volumes (Data); indicator may be the area or just a map hint, so it can complete early or late | QuestManager.UpdateAreaObjectives |
| Unknown28 (rescue) | progress when the player activates a creature of TargetGroupIdRewardPane, the cage | Medium | all 3 reward pane groups are cages or cage panels with an activate spell; 5 objectives all describe rescuing from cages | Data (17 to 218) unknown | WorldEntity.OnActivateSuccess |
| CompleteEvent | progress when a public event is won by the player's team | Medium | Data matches PublicEvent ids | partial credit for losing teams unknown | PublicEvent.Finish |

## Spell effects

| Effect | Our reading | Confidence | Evidence | Open questions |
|---|---|---|---|---|
| DespawnUnit (97) | remove target, world spawns return on the respawn timer | Medium | names (Clearing Weeds, Detonate) | real respawn delay per object unknown |
| Scale (18) | DataBits00 float scale, timed | High | values 1.1 to 2.0 on growth buffs | DataBits01/02 look like blend times, unused |
| SummonPet (98) | DataBits00 creature | High | names match creatures (Repairbot, Geist) | DataBits01 (4, 5, 7, 9) probably a pet slot; limits unenforced |
| PetCastSpell (111) | DataBits00 pet creature, DataBits01 spell | High | bot ability names line up | with no owner target the pet uses its own target, then itself (a guess) |
| Stealth / RemoveStealth (83/84) | stealth state; SpellCastStealthChange 1 breaks it, 2 does not | Medium | 1 is on Stalker attacks, 2 on Stealth and Nano Skin | 0 on non-Stalker spells: does it break stealth? |
| SpellDispel (53) | DataBits03 is the SpellClass removed, DataBits00 the count | Medium | values 36 and 38 match BuffDispellable / DebuffDispellable | Cleanse examples use 36 (buffs), which is odd |
| Transference (5) | DataBits00/01 vitals drained and given, 02/03 damage, 05 return ratio | Medium | damage calculator already used 02/03; Sapping Strikes reads sensibly | DataBits04 (0, 1, 3) unknown |
| SummonTrap (106) | creature, spell, arm delay, ally flag, radius | Medium | Proximity Mine, Medic probe rows | DataBits05-07 unknown (visual / arm spell?) |
| SpellCounter (2) | interrupt; DataBits01 lockout ms, DataBits03 school mask | Medium | interrupt ability names, 7 = all schools | lockouts not applied |
| ThreatModification (46) | modes 1 percent drop, 2 drop, 5 add, 6 wipe | Medium | Forget/Detaunt, Drop Threat, Atomic Attraction, Threat Wipe | modes 0 and 4 unknown, left alone |
| SapVital (30) | DataBits03 mode: 1 +% of max, 0 -% of max, 2 -% of current | Medium | Level-Up 100 mode 1, Creeping Death ticks mode 0 | DataBits01 used instead of 02 on a few rows |
| UnitStateSet (15) | state 6 = invulnerable | Medium | 63 rows, names Invulnerability / Energy Shield | other states (1 Block, 7-9, 16-18, 22) unknown |
| ChangePhase (34) | DataBits00/01 phase masks I perceive / perceive me | Medium | login sends 1/1 in the same message | DataBits02/03 (often -17) unknown; entities are not phase filtered |
| ItemVisualSwap (76) | DataBits00 ItemSlot, DataBits01 display id | High | slot values match the ItemSlot enum | |
| ProxyRandomExclusive (96) | up to five (spell, weight) pairs | High | weights 1/1, 50/50 | |
| ProxyChannelVariableTime (94) | (spell, delay ms) pairs | High | delays sum to the effect duration | |
| ProxyLinearAE (19) | sub spell cast at the target | Low | DataBits01/02 are an angle and distance we ignore | needs positioned casts |
| DistributedDamage, DistanceDependentDamage | plain damage | Low | calculator reads them like Damage | no split or falloff |
| SetBusy (107) | DataBits00 1 busy, 0 free; busy objects refuse activation; 60 s fallback clear | High (meaning), Invented (fallback) | 352 of 508 spells are activate spells on the object; 62 spells set at 0 ms and clear after 2 to 60 s; 0 used by PSI Recharging and Failed | DataBits01 (a creature id on 6 rows) unknown; 275 set-only spells relied on lost scripts |
| NpcExecutionDelay (35) | the casting creature does nothing for the effect duration, a recovery window | Medium | 3,288 effects, 94 percent with no data and 1 to 4 s durations, all on creature spells | DataBits on the other 6 percent unknown |
| PathXpModify (93) | DataBits00 x 10 path xp | Invented | 5 effects, values 1, 10, 30 | |
| Teleport with no location | recall to bind point | Medium | Recall - Transmat (8568) is the only such spell | |

## Tradeskills

| Item | Our meaning | Confidence | Notes |
|---|---|---|---|
| Tradeskill.Flags | 2 gathering, 4 equipment crafting, 8 other crafting, 1 hobby (Cooking, Fishing, Farmer), 16 Runecrafting | Medium | from the names |
| active limit | two crafting tradeskills, gathering and hobbies free | Medium | late retail rule |
| gathering xp | the current tier CraftXp per node destroyed | Invented | TradeskillTier has no harvest xp column |
| known schematics | flag 1 schematics up to the current tier, plus recipe items | Medium | flag 1 schematics are never taught by an item |
| messages | ServerProfessionsLoad, ServerProfessionUpdate and ClientTradeskillLearn layouts come from upstream and are untested with the client | | professions list only sent once something is learned |

## Path missions

| Item | Our meaning | Confidence | Notes |
|---|---|---|---|
| mission type to table | ObjectId points into one table per (path, type) | High | type 0 Soldier → PathSoldierEvent 97 of 98, etc. |
| PathMissionFactionEnum | 1 Exile, 2 Dominion | Medium | from Northern Wilds (Exile start) |
| unlock rule | every mission of the zone episode unlocks on zone entry | Low | retail had per mission unlock (Creature2IdUnlock, proximity) |
| vista / claim / exploration / tracking | reach the mission world locations (tracking: retail followed a creature or trail) | Low | retail required placing a beacon or exploring a set of points |
| cartography | zone map explored percent, done at 95 | Invented | retail threshold unknown |
| scavenger clue types | 0 activate, 1 creature or area, 2 location | Medium | field patterns per type; starting the hunt is skipped |
| Soldier SWAT | kill the target group Count times (retail used a gadget on them) | Low | stand in |
| path XP per mission | 25 + 5 x level | Invented | fitted to the PathLevel curve, retail values unknown |

## Challenge rewards

| Item | Our meaning | Confidence | Evidence | Alternatives / risks |
|---|---|---|---|---|
| Challenge.RewardTrackId | the zone reward track that completing the challenge adds points to | High | 525 of 643 challenges point at one of 20 tracks named "Reward Track - <zone>" | |
| RewardTrack point costs | milestones; reaching one gives a reward | High | 300/600/900/1200 on every zone track | |
| RewardTrackRewards.RewardPointFlags | bit N = milestone N; several rows per bit, each a choice of three items | Medium | bits 1, 2, 4, 8 line up with the four costs; the last bit offers a crate | why several rows share a bit (rotation or random) is unknown |
| challenge gear sets (ItemSourceId 87, 88) | the contents of the "Challenger's Gear Bag" rewards | Medium | green sets levels 12 to 49 covering every class, role and slot, only source ids with that shape | the bags may also have held other items; 87 Exile and 88 Dominion read from names |
| class of each armor piece | armor category gives two classes, id order within slot and level gives which, damage version first | High | matches the name prefix of all 552 armor pieces | |
| points per challenge | 100, 150, 200 for bronze, silver, gold | Invented | | retail values unknown |
| piece given per milestone | the class's damage version, best level at or under the player's, slot rotates by track and milestone | Invented | | retail let you pick from three bags; no support role choice yet |

## World data from Jabbithole

| Item | Our method | Confidence | Risk |
|---|---|---|---|
| missing quest NPC positions | Jabbithole map pins for the page with the same name and zone, quest linked pages first; height and area from the 5 nearest spawns within 40 m, else terrain height and area from the server map files (median 0.19 m off on 2,021 known spawns, not used in the capitals) | Medium | name-only matches can pick the wrong variant; pins are player-reported positions |
| quest targets with no Jabbithole page | placed at the objective indicator world locations, terrain height from the map files | Low | 121 creatures | the marker may be an area centre, not where the creature stood |
| loot tables | observed drops / (kills + 5), a small sample correction; quest items only while their CollectItem objective is open | Medium | small samples on rare creatures; quest item rates are per kill of players who happened to be on the quest |
| creature abilities | spells seen on the Abilities tab; instant ones as auto attacks | Low | not the real rotation or conditions |

## Other invented values

| Value | Ours | Where |
|---|---|---|
| kill XP formula | tunable | KillExperience |
| exploration XP | a whole zone map is worth 10 times the level base quest XP, split over its hex groups | ZoneMapManager |
| respawn delay | 60 s for everything | Realm:Respawn |
| challenge timer, leave-area grace, cooldown | 5 min, 10 s, 30 min | ChallengeManager |
| challenge auto start | flag 0x20 | ChallengeManager (Medium: present on most kill challenges) |
| open world event start | player within 40 m or the location radius, 5 min restart | OpenWorldEventStarter |
| group kill credit range | 100 m, full XP each | UnitEntity.GenerateRewards |
| creature specials interval | 8 to 14 s | CombatAI |
| aggro range | 15 m, 2 m less per level the player is above the creature (min 5 m), none at 8+ levels above | CombatAI |
| calls for help | idle same faction creatures within 10 m join, one ring only, Prime rank (bosses) never answer | CombatAI |
| fall damage | free up to 25 m from the highest point of the Fall state, then 3 percent of max health per metre (lethal near 58 m); none when the landing batch leaves the player swimming | MovementManager (if the client switches to Swim one packet after landing, a water dive still hurts) |
| leash range | 50 m from where combat started, then 10 s ignoring proximity on the walk home | CombatAI |

## Special cases that stand in for an unknown generic mechanism

These fix a visible symptom without knowing the real rule, the kind of patch that piles up. Each should be replaced when the mechanism is understood.

- **Quest creatures despawn after activation** (WorldEntity.OnActivateSuccess). Narrowed in fbc02afc after decoding SetBusy: it now only applies when the activate spell has neither a DespawnUnit nor a timed SetBusy clear, the cases retail finished with server scripts (the Trapped Survivor spell 1817 has only Activate).
- **Hidden script steps complete themselves.** Activate steps with no target or location and either no text (341 game wide) or the Hidden flag with a designer note as text (last step of Contact with Thayd) were finished by lost scripts; they complete once every other step is done (QuestManager.UpdateAreaObjectives).
- **Gather objectives count killing or using the node.** GatheResource objectives (10, such as the Fallen Trees in A Fiery Escape) name a harvest node; real harvesting needs tradeskills, so destroying or activating the creature counts (UnitEntity kill credit, WorldEntity.OnActivateSuccess).
- **Starting zone transports teleport.** The Ship Controls in Northern Wilds and the Navigation Controls in Levian Bay only carry an Activate effect; retail flew the player with a lost script. Using them teleports to the receiver location of the quest that ends on the continent (WorldEntity.ScriptedTransports). The Portal to Celestion (Everstar Grove), the Transport to Everstar Grove (Northern Wilds) and the Crimson Isle Ship Controls (to Artemis Zin in Ellevar, where the next quest starts) work the same way.
- **Survivors climb out of the wreckage.** Freeing a Trapped Survivor (11070) spawns one of the never placed Crash Survivors, which runs 15 m away and is removed (Script.Main NorthernWilds/TrappedSurvivorEntityScript).
- **Holocrypts are the nearest transmat.** Holocrypt locations are not in the client data, so the holocrypt respawn goes to the nearest non-hostile transmat terminal at full health (BaseMap.OnResurrectHolocrypt).
- **Open world events start by proximity.** Retail started them from lost server scripts.
- **Creature abilities from Jabbithole.** Observed spells, not the creature's real rotation or conditions.
- **Vistas count from the ground.** Explorer vista points sit on cliff tops and the climb routes are hard to find, so standing within 45 m of the point on the ground completes it, at any height (PathMissionManager.VistaRadius).
- **Finish Him! moves you aboard.** The Skull's Eye interior (world 930) has no placed creatures or entrance. Meeting the contact at the ship in Algoroc teleports you to the ship's entry marker, Captain Thokov and the escape pod are placed at the quest's own markers, and using the pod returns you to the ship's spot in Algoroc (Script.Main Ships/FinishHimQuestScript). Thokov's crew is not placed. Boss Xagg's Due in Galeras works the same way in the Dust Stalker (world 1138), with Boss Xagg, the bridge controls and the access panel placed (Ships/BossXaggsDueQuestScript).
- **Quest NPCs with no archived position stand at their quest's hub.** Givers and turn-ins that Jabbithole has no usable page for are placed at the location their quest text links to (37 NPCs, such as the Northern Wilds M.A.C. in Settler's Reach). The Explorer, Scientist and Settler union reps stood on the old arkship, which the final client no longer has, so they are in Settler's Reach (and at the Everstar Grove arrival spot for the Aurin start).
- **The Unusual Rock lifts you to the vista.** Below the Direct Survivors to Camp vista in Northern Wilds, the rock's Northern Wilds Platform Activate spell raised a platform in retail that is not in the client data. Using it teleports you to the vista point (WorldEntity.ScriptedTransports).
- **Jump pads launch on contact.** Jumpshrooms in Celestion and Sylvan Glade and the Airbooms at Dead Rock Prison in Whitevale have no activate spell and the triggers that cast their launch spells are lost. Stepping within 2.5 m casts the launch spell named after them on the player, 2 s cooldown per player (guessed values, Script.Main World/LaunchPadEntityScript).
- **AMPs use the full point budget from the start.** AMP points in retail grew with level (the per-level values are not in the client data); here every action set has the 45 points the server always gave. AMPs that need an unlock item are not given. Choosing an AMP applies its passive spell (ClientRequestActionSetChangesHandler, SpellManager.SyncAmpPassives).
- **Taxi flights and shuttles are rapid transports.** Buying a flight at a taxi kiosk or taking a shuttle sends you to the destination node with the rapid transport spell (its cost and cooldown) instead of flying the TaxiRoute spline (ClientTaxiFlightHandler).
- **Shiphand missions are populated from their 2014 versions.** The six other empty shiphand maps (Outpost M-13, Space Madness, Rage Logic, The Gauntlet, Deep Space, Fragment Zero) are filled the same way, at levels 10, 15, 20, 25, 30 and 50 (a guess at the post-launch level bands), named NPCs once, other creatures capped per kind; wave creatures stand in place until the events are scripted.
- **Infestation is populated from its 2014 version.** Shiphand: Infestation (world 1232) had nothing placed. Its creatures stand at the pins archived for Salvage Rights, the same X-82 cargo ship, with the mobs set to level 6, the boss once and at most 20 of each mob. Heights come from the map's markers. Player summons captured on the pages (vendbots, campfires) are left out (data/instance_infestation.sql).
- **Infestation runs as a fixed chain.** The objective order is read from the objective texts (InfestationEventScript). Healing the contaminated shiphands and defending the medbay complete on their own because the medbay attackers exist only as level 50 veterans in the client data.
- **Outpost M-13 runs as a fixed chain.** OutpostM13EventScript orders the objectives from their texts. The Novaburn waves on M-13, the datachrons and the mine search complete on their own when their step is reached.
- **Unscripted expedition steps never stall.** ChainedEventScript completes objectives whose creatures or objects are not placed on the map, and reach-the-area objectives after 90 s if their trigger never fires. Space Madness runs on it (SpaceMadnessEventScript).
- **Rage Logic runs without hoverbikes.** The map starts event 214 (the run) instead of 213 (the vehicle choice), and the objectives chain without the hoverbike phase (RageLogicEventScript).
- **Dungeons are populated but not scripted.** Stormtalon's Lair, Ruins of Kel Voreth, Skullcano and Sanctuary of the Swordmaiden have their 2014 creatures at levels 20, 25, 30 and 35, bosses once each (difficulty 12), boss ability objects left out. Their encounters are not scripted (data/instance_*.sql).
- **Stun breakout takes 6 presses.** Retail filled a breakout gauge whose values are not in the client data. Here 6 direction presses while stunned end the stun (ClientCCStateStunUpdateHandler).
- **Taxi kiosk range is 150 m.** Retail opened the taxi map from the kiosk NPC. Here a flight is accepted when the player is within 150 m of any taxi node of their world, since the kiosk positions are not tied to their nodes in the client data (ClientTaxiFlightHandler).
- **Paths are free to switch and unlock.** Retail charged 60 service tokens to unlock a path and had a 30 minute cooldown on switching (10 tokens to skip it). Here both are free so one character can play every path (ClientPathChangeRequestHandler, ClientPathUnlockHandler). The client still shows only the active path, so it is one path at a time.
- **Scientist scan targets come from the mission texts.** Retail told the client which units belong to a scan mission (ServerPathScientistAddCreatureInfoToCreature) from server data that is lost. Each scan mission has a list of creature names written from its text (PathMissionManager.Scientist.cs ScanRules, Northern Wilds first, zone by zone). Every group of names needs 3 scans of different units, or 1 when the mission lists several groups. The scan is instant once the scanbot is out.
- **Settler improvements are per player.** Retail shared built improvements with everyone in the zone. Here they belong to the settler who built them and last DurationPerBundleMs. Each build adds the group's contribution to the hub's expansion missions, the hub's MissionCount completes them (PathMissionManager.Settler.cs).
- **Settler resource nodes are placed by us.** The hub resource nodes and missing depots are spawned from the hub resource areas and improvement display points (data/settler.sql, tools/build_settler.py), six nodes per area on terrain heights. Activating a node gives one of the hub resource whose item has the node's name.
- **Soldier holdout waves are invented.** The wave creatures were in lost server scripts. Each wave brings 2 more enemies than the last (2 to 5) of the hostile creatures living within 150 m of the control point, timing from PathSoldierEvent. Beat every wave or last the event time to win, dying or going 60 m away loses (PathMissionManager.Soldier.cs).
- **Reach challenges are graded by position.** General challenges that ask to reach a spot start when the player arrives at their start location (or from the challenge window when there is none) and finish within 6 m and 6 m of height of their indicator location. Time tiers count a finish under more limits as a better tier (ChallengeManager.IsReach).
- **Loot ids live far above entity guids.** The client treats LootItem.LootUnitId as a loot unit and removes that unit on ServerLootGrant. Retail loot ids are unknown, ours start at 0x7F000000 so they never name a real unit (LootInstanceIdProvider).
- **Daily login rewards go straight to the bags.** Retail put each day reward (AccountItem) in the account inventory to claim. There is no account inventory here, so the AccountItem item is created in the bags of the first character to enter the world that day (UTC), basic tier only (AccountDailyLoginManager).
- **Datacube discovery counts the datacubes we spawn.** Retail named only the zone (PathScientistDatacubeDiscovery). Here the mission lists the DATACUBE creatures spawned in that zone in the world database (PathMissionManager.Discovery.cs DatacubeRules), opening each counts. Grimvault's sub zone missions share one list.
- **Specimen samples come from any creature near the survey spot.** A specimen survey location is sampled by scanning a creature within 30 m of it, the creature kind is not checked.
- **Settler projects are per player.** Infrastructure (PROJECT) missions count the player's own deliveries to the builder (MissionCount), without the shared project total and timer of retail. Resources are picked up by activating the resource creatures (spawned from archived pins, data/settler.sql) or the creatures named in the objective, and handed in by coming within 8 m of the builder.
- **Quest events that cannot run are held, not fought.** Some CompleteEvent objectives name a public event that is gone from the client data (Gallow Departed, event 92) or sits on a map that does not load. Holding the objective place (marker, or the location linked in its text, 30 m) for 20 s completes them (QuestManager.CanEventRun).
- **Work orders count any craft.** Quest objective type 31 (260 objectives: work orders, crafted deliveries) has no text and its data points at a table that is not in the client. Any successful craft counts one step of it (TradeskillManager, QuestManager.ObjectiveUpdateAny). Objectives of this type with count 0 were already complete.
- **Field studies count scans, not behaviors.** A Scientist field study (mission type 20) asks to watch a creature perform the behaviors of its checklist; the behavior events were server side and are lost, so each checklist entry is one scan of a different unit of the creature (PathMissionManager.Scientist.cs RuleFor).
- **Algoroc biology scans use the local family.** BIOLOGY: Grimstone Girrok, Gronyx and Stumpkins name creatures that are not placed anywhere (the scan-only Gronyx 21926 has no spawn). Their scans count the Grimstone, Augmented Slateform and Pinewrath creatures living at the mission spots (PathMissionManager.Scientist.cs).
- **The Grid and Bull's-Eye are reach challenges.** The Grid completes on reaching the end of the laser grid, touching a beam is not tracked. Bull's-Eye starts at the cannon and completes on landing inside the pond's indicator radius, always at the top tier (distance from the center is not scored). Seek and Destroy counts the Alchemical Supplies by creature id, the challenge has no target group (ChallengeManager.TargetCreatures).
