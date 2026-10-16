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
| EpisodeState (9) | Value is a QuestState over the episode's quests (3 = all completed) | Medium | 31 of 34 uses are Value 3 | how "accepted" is defined for an episode is a guess | | PrerequisiteCheckEpisodeState |
| Unknown47, ChallengeRequirement (69), ChallengeLocked (74), PlayerPathMission (54), PathMissionRequirement (67) | **not implemented** | | Value is a spell / challenge / mission id | several readings fit equally well | | unhandled on purpose |

## Quest objectives

| Field | Our meaning | Confidence | Evidence | Alternatives / risks | Code |
|---|---|---|---|---|---|
| Unknown10 | Data is a target group; progress when the player activates a creature in it | Low | Shellshock! cannons (TG 1322 = creature 11251, activate spell on it) and the Roll Call Sheet | 52 objectives, some describe kills or challenges; Data is not always a target group (18876, 30840) | WorldEntity.OnActivateSuccess |
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
| Teleport with no location | recall to bind point | Medium | Recall - Transmat (8568) is the only such spell | |

## Path missions

| Item | Our meaning | Confidence | Notes |
|---|---|---|---|
| mission type to table | ObjectId points into one table per (path, type) | High | type 0 Soldier → PathSoldierEvent 97 of 98, etc. |
| PathMissionFactionEnum | 1 Exile, 2 Dominion | Medium | from Northern Wilds (Exile start) |
| unlock rule | every mission of the zone episode unlocks on zone entry | Low | retail had per mission unlock (Creature2IdUnlock, proximity) |
| vista / claim / exploration | reach the mission world location | Low | retail required placing a beacon or exploring a set of points |
| cartography | zone map explored percent, done at 95 | Invented | retail threshold unknown |
| scavenger clue types | 0 activate, 1 creature or area, 2 location | Medium | field patterns per type; starting the hunt is skipped |
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
| loot tables | observed drops / (kills + 5), a small sample correction; quest items only while their CollectItem objective is open | Medium | small samples on rare creatures; quest item rates are per kill of players who happened to be on the quest |
| creature abilities | spells seen on the Abilities tab; instant ones as auto attacks | Low | not the real rotation or conditions |

## Other invented values

| Value | Ours | Where |
|---|---|---|
| kill XP formula | tunable | KillExperience |
| exploration XP | 20 percent of the level base quest XP per newly revealed zone map hex group | ZoneMapManager |
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
- **Gather objectives count killing or using the node.** GatheResource objectives (10, such as the Fallen Trees in A Fiery Escape) name a harvest node; real harvesting needs tradeskills, so destroying or activating the creature counts (UnitEntity kill credit, WorldEntity.OnActivateSuccess).
- **Holocrypts are the nearest transmat.** Holocrypt locations are not in the client data, so the holocrypt respawn goes to the nearest non-hostile transmat terminal at full health (BaseMap.OnResurrectHolocrypt).
- **Open world events start by proximity.** Retail started them from lost server scripts.
- **Creature abilities from Jabbithole.** Observed spells, not the creature's real rotation or conditions.
