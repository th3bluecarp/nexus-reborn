# PathArrow

A small WildStar client addon for the 16042 client. It shows a movable window with an arrow that points toward the nearest path mission target for your current path. The targets are the real spots the server checks: the creatures to scan, kill or activate, the vistas and other locations to reach, the depots to build at. They come from `PathArrowData.lua`, so the arrow no longer depends on the client map regions. Under the arrow it shows the mission name and the distance in meters, plus a clock direction (12 o'clock is straight ahead on screen). It can also point at your tracked quest objectives instead.

The list holds every mission of your path in the current world. Missions the client shows as unlocked or started come first, the same test the default ZoneMap addon uses. Missions the client has not unlocked yet are still listed after them with "(locked)" in front of the name, so you can walk to them. Completed missions are left out, and the addon remembers per character which missions it has seen complete.

## Install

1. The game reads addons at login or on a UI reload, so you can copy the files while it runs.
2. Copy this folder so the files sit here:
   `%APPDATA%\NCSOFT\WildStar\Addons\PathArrow\toc.xml`
   `%APPDATA%\NCSOFT\WildStar\Addons\PathArrow\PathArrowData.lua`
   `%APPDATA%\NCSOFT\WildStar\Addons\PathArrow\PathArrow.lua`
   `%APPDATA%\NCSOFT\WildStar\Addons\PathArrow\PathArrow.xml`
3. Log in to a character, or type `/reloadui` if you are already in game.
4. If the window does not show up, press Escape, open Addons and make sure PathArrow is enabled. The toc declares APIVersion 16, which matches the APIVersion in the client's own Addons.xml, so it should not be flagged as out of date.

## Commands

| Command | What it does |
| --- | --- |
| `/patharrow` | Show or hide the window |
| `/patharrow next` | Point at the next mission in distance order |
| `/patharrow prev` | Point at the previous one |
| `/patharrow nearest` | Go back to always pointing at the nearest one |
| `/patharrow quest` | Point at tracked quest objectives instead |
| `/patharrow path` | Point at path missions again |
| `/patharrow hint` | Also fire the game's own hint arrow for the current target |
| `/patharrow flip` | Mirror the arrow left and right, only if it points the mirror way |
| `/patharrow rad` | Switch the arrow rotation units, only if the arrow barely turns or spins wildly |
| `/patharrow reset` | Put the window back in its default spot |

Drag the window to move it. The position, mode and the two fix toggles are saved per character.

The line under the distance shows the position in the list and how many target points the mission has, for example "Path  2/9  12 pts". The arrow always picks the closest of those points as you move. "map" after the count means the mission is missing from the data and the client map region is used instead.

After `next` or `prev` the addon stays on that mission while it re-sorts. When the mission finishes it goes back to the nearest one.

If something is missing it says so in the window. You will see "no path episode in this zone", "no path missions here", "no tracked quests" or "no location" rather than a Lua error. Every client call is wrapped so a nil result or a missing function cannot break it.

## Quick check in game

1. Stand still and look at the clock text, for example "3 o'clock".
2. Turn the camera so that the mission is straight ahead. The text should move to 12 o'clock.
3. The arrow should agree with the text. If it points the mirror way, type `/patharrow flip`. If it hardly rotates at all or spins far too fast, type `/patharrow rad`.

## How the direction is worked out

The addon takes the player position and a point 2 meters toward the target on the ground plane. It projects both onto the screen with `GameLib.WorldLocToScreenPoint` and points the arrow along the screen line between them. That is the same math RaidCore uses to orient its sprites. So the arrow follows the camera, which suits WildStar's action camera. If the probe point ends up behind the camera, for example zoomed all the way in, it falls back to the character facing from `Unit:GetFacing()`.

## API confirmed from public sources

Sources: the Hammster wildstar-api-docs repo, the Carbine default UI addon dumps on GitHub (Zod-/Wildstar-Carbine-Addons Live tree and NexusInstruments/CarbineAddons) and Immemor/RaidCore.

| Call | Where it was confirmed |
| --- | --- |
| `Apollo.RegisterAddon`, `OnLoad`, `XmlDoc.CreateFromFile`, `RegisterCallback("OnDocumentReady")`, `xmlDoc:IsLoaded()` | Carbine Stuck, Collectables |
| `Apollo.RegisterSlashCommand(name, handler, self)` | Carbine Stuck, Contracts |
| `ApolloTimer.Create(seconds, repeat, handler, self)` | Carbine AbilityAMPs and many others |
| `Apollo.LoadForm`, `FindChild`, `Show(b, true)`, `SetText`, `MoveToLocation`, `GetLocation():ToTable()`, `WindowLocation.new` | Carbine Stuck, AbilityVendor |
| `OnSave(eType)`, `OnRestore(eType, tData)`, `GameLib.CodeEnumAddonSaveLevel.Character` | Carbine ZoneMap, AbilityVendor |
| `GameLib.GetPlayerUnit()`, `unit:GetPosition()`, `unit:GetFacing()` | API docs, ZoneMap, RaidCore DrawManager |
| `GameLib.WorldLocToScreenPoint(Vector3)`, `Vector3.New(x, y, z)` | API docs, RaidCore DrawManager, Carbine Login |
| `PlayerPathLib.GetCurrentEpisode()`, `episode:GetMissions()` | Carbine ZoneMap UpdateMissionList |
| `mission:GetMissionState()` with `PathMission.PathMissionState_Unlocked` and `_Started`, `mission:IsComplete()`, `GetName()`, `GetId()`, `GetDistance()` | Carbine ZoneMap, PathTracker, API docs |
| `mission:GetMapRegions()` returning a list of `{ tHexes, tIndicator }` where `tIndicator` is a world position | Carbine ZoneMap OnPlayerPathMissionActivate |
| `mission:ShowHintArrow()`, `quest:ShowHintArrow()` | Carbine PathTracker, ContractTracker |
| `QuestLib.GetTrackedEpisodes(bByDistance)`, `episode:GetTrackedQuests(0, bByDistance)` | Carbine ZoneMap, QuestTracker |
| `quest:GetMapRegions()` returning `{ nWorldId, nWorldZoneId, tRegions, tIndicator }`, `quest:GetState()`, `quest:GetTitle()` | Carbine ZoneMap, QuestTracker, API docs |
| `GameLib.GetCurrentWorldId()` | API docs (listed, no example) |
| `window:AddPixie`, `UpdatePixie`, `DestroyPixie` with `strSprite`, `fRotation`, `loc.fPoints`, `loc.nOffsets` | RaidCore DrawManager, Carbine Crafting |
| Sprite `CRB_Basekit:kitIcon_Holo_UpArrow`, fonts `CRB_InterfaceMedium_B` and `CRB_InterfaceSmall`, sprite `BasicSprites:WhiteFill` | Carbine form XML files |
| `ChatSystemLib.PostOnChannel(ChatSystemLib.ChatChannel_System, text, "")` | Carbine ChatLog |
| APIVersion 16 | the client's own Addons.xml on the install PC |

## Guessed, and how the addon copes

1. `mission:GetMapLocations()` is listed in the API docs but no public addon calls it, so its return shape is unknown. The addon reads it as a list of points or a list of tables holding a point, and tries the likely field names. If it gives nothing useful, `GetMapRegions()` still supplies the location.
2. The unit of `fRotation` for pixies. The API docs and RaidCore use degrees. Two Carbine addons pass values that look like radians. Degrees is the default and `/patharrow rad` switches.
3. The turn direction of the fallback that uses character facing. The world handedness is not documented, so that fallback may be mirrored. It is only used when the camera probe fails, and `/patharrow flip` fixes it.
4. Distance is measured on the ground plane between the player and the closest target point of the mission. For missions missing from the data it is the closest map region marker, which is the center the map shows, so for large area missions you may reach the area before the distance reaches zero. The height difference shows as "up" or "down" when it is more than 10 meters.
5. Missions with no point at all show "no location", with the game's own distance in brackets when the client offers one. In the data that is only the Explorer cartography missions, which ask you to explore the whole zone map, and one Soldier mission whose target never spawns.
6. The player faction comes from `Unit:GetFaction()`, compared with 167 for Exile and 166 for Dominion. If the client gives something else, missions of both factions are listed.
7. `PlayerPathLib.GetPlayerPathType()` is used to match the path numbers in the data (0 Soldier, 1 Settler, 2 Scientist, 3 Explorer). Without it only the missions of the current episode are listed.

## Files

`toc.xml` is the table of contents. `PathArrowData.lua` holds the mission targets and loads before `PathArrow.lua`, which is the code. `PathArrow.xml` is the window form. `gen_data.py` builds `PathArrowData.lua` and is not needed in game.

## Mission data

`PathArrowData.lua` maps each mission id to its world, path, faction, kind, name and up to 25 target points. It covers every path mission the server supports, and it picks the targets the same way `PathMissionManager` does on the server.

| Mission kind | Where the points come from |
| --- | --- |
| Scientist scans, checklists and field studies | spawns of the creatures named by the server scan rules, or by the field study creature and target group |
| Scientist datacube discovery | spawns of the datacube creatures in the server rules |
| Scientist specimen survey | the survey locations |
| Scientist experimentation | spawns of the console creature for that experimentation |
| Explorer vista, claim, exploration and tracking, Settler scout and mayor | the mission locations, or the nodes of its explorer area |
| Explorer scavenger hunt | spawns of each clue creature or target group, or the clue location |
| Explorer, Soldier activate and Soldier kill missions | spawns of the target creature and its target group |
| Settler expansion | the depot display points, then the hub resource spots |
| Settler infrastructure | the builder spawns, then the resource spawns |
| Settler sheriff | the quest givers, then the quest objective markers |
| Soldier holdout | the control point creature spawns |

Points are only kept in the mission's own world. When a mission has more than 25 points they are thinned so the kept points spread over the whole area. When no target creature is spawned in the world database the mission's own locations or quest direction markers are used instead.

To rebuild it, export the creature spawns with `select id, creature, world, x, y, z from entity where creature <> 0` into a tab separated file, then run `gen_data.py` in a throwaway container as its header shows. It prints the missions per path and the ones that have no location.
