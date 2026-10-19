# PathArrow

A small WildStar client addon for the 16042 client. It shows a movable window with an arrow that points toward the nearest active path mission for your current path. Under the arrow it shows the mission name and the distance in meters, plus a clock direction (12 o'clock is straight ahead on screen). It can also point at your tracked quest objectives instead.

Active means the mission is unlocked or started and not complete. That is the same test the default ZoneMap addon uses for its mission list.

## Install

1. The game reads addons at login or on a UI reload, so you can copy the files while it runs.
2. Copy this folder so the files sit here:
   `%APPDATA%\NCSOFT\WildStar\Addons\PathArrow\toc.xml`
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

After `next` or `prev` the addon stays on that mission while it re-sorts. When the mission finishes it goes back to the nearest one.

If something is missing it says so in the window. You will see "no path episode in this zone", "no active path missions here", "no tracked quests" or "no location" rather than a Lua error. Every client call is wrapped so a nil result or a missing function cannot break it.

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
4. Distance is measured on the ground plane between the player and the closest map region marker of the mission. A mission marker is the center the map shows, so for large area missions you may reach the area before the distance reaches zero. The height difference shows as "up" or "down" when it is more than 10 meters.
5. Missions with no map region (some Explorer and Scientist types) show "no location" with the game's own distance in brackets when the client offers one.

## Files

`toc.xml` is the table of contents. `PathArrow.lua` is the code. `PathArrow.xml` is the window form.
