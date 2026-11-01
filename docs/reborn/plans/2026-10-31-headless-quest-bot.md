# Headless quest bot — plan (2026-10-31, awaiting go)

Status: **PLAN ONLY. Nothing built. Waiting for Brian's go.**

## Why

The parity tracker says what is implemented. It cannot say that a quest actually completes end to end
on our server with the right objective credit and reward. Today the only test is a human playing.
A headless bot that speaks the NexusForever protocol can log in, make a character, walk every quest in
a zone and report what broke, every night, before anyone logs in. No game client, no GPU, no screen
scraping.

Scope rule from Brian: current zone first (Northern Wilds, zone 35, 30 Exile quests), every path, then
the next zone in levelling order. Bug reports from his own play still come first.

## What the repo already gives us (read 2026-10-31)

- Packet framing is simple: `[u32 size][u16 opcode][bit-packed body]`, LSB-first bit stream
  (`NexusForever.Network`: `GamePacketReader`, `GamePacketWriter`, `GameMessageOpcode`).
- Cipher and login math exist and are reusable: `PacketCrypt` (initial key from build 16042 + auth
  message, then the 16-byte session key after `ClientHelloRealm`), `Srp6Provider`.
- Movement command models (`Network.World/Entity/Command/*`) implement both Read and Write.
- **The catch:** every `Client*` message only implements `IReadable` and every `Server*` only
  `IWritable`. The bot must write its own writers for the client messages it sends and readers for the
  server messages it needs. Field orders are documented in the server classes, so this is transcription,
  not reverse engineering.
- Packets are length-prefixed, so a server message the bot cannot parse yet can be skipped whole without
  desyncing the stream. We only parse what we need.
- No anti-cheat: `ClientMovementCommandValidator` is all TODO. A `SetPosition` command is accepted as
  is, so the bot can "walk" by teleporting. GM chat commands (`!tele`, `!quest`, `!character level`,
  `!item add`) exist for everything else.
- No test project, no bot, no packet replayer exists. This is new code.

## Design

**Project:** `Source/NexusForever.Bot` (net10.0 console, in this repo, same solution). References
`Network`, `Network.Auth`, `Network.World`, `Cryptography`, `Game.Static`, `GameTable`. The game tables
give quest data (Quest2, QuestObjective, Creature2.QuestIdGiven / QuestIdReceived); the zone export the
parity dashboard already makes gives NPC spawn positions.

**Where it runs:** on the desktop PC next to the server, built with the SDK already in
`D:\NexusForever\tools\dotnet`, driven from Tower over the existing SSH route. Tower only schedules
runs and collects reports. Reason: the auth database's `server` table redirects clients to the world
host, and the database itself is bound to the PC's loopback. Running on the PC sidesteps both.
(The bot still takes an explicit `--world host:port` and ignores the redirect, so it can run from Tower
later.)

**Login, v1 shortcut:** the auth server only checks that the account's `gameToken` matches. The bot
writes a fresh token into the bot account's row (loopback MariaDB, port 3307), connects to auth on
23115, gets `ServerRealmInfo` (session key), connects to world on 24000. STS/SRP (port 6600) comes in
phase 2 so the real login path gets tested too.

**Session flow the bot implements:**
1. Auth: read `ServerHello` → send `ClientHelloAuth` (encrypted) → read `ServerAuthAccepted`,
   `ServerRealmInfo` (address, port, session key, account id).
2. World: read encrypted `ServerHello` → `ClientHelloRealm` (account id + session key, server switches
   key) → `ClientCharacterList` (required: registers the login queue) → `ClientCharacterCreate`
   (fresh character per run; reply carries the character id) → `ClientCharacterSelect` →
   read `ServerChangeWorld`, `ServerMovementControl`, `ServerPlayerCreate`, `ServerEntityCreate`s →
   `ClientEnteredWorld` (zone id) → `ServerPlayerEnteredWorld`.
3. Keepalive: any packet every < 300 s (`ClientPregameKeepAlive` / entity commands).
4. World model: parse `ServerEntityCreate` (guid, type, model, position from the SetPosition command)
   for NonPlayer and Player types first; other types are skipped by packet length. `ServerEntityDestroy`
   removes. Vision is 128 m, so the bot teleports within range of a spawn and waits for the create.
5. Quest driver, data-driven per quest id:
   - find giver creature id → spawn position → `!tele coordinates` (or SetPosition) → wait for the
     giver's entity create → `ClientEntityInteract` TalkTo → `ClientQuestAccept` → expect
     `ServerQuestStateChange` state 1 (Accepted).
   - objectives by type (v1 covers the types Northern Wilds uses):
     TalkTo / ActivateEntity → interact or `ClientActivateUnit` on the target;
     EnterArea / EnterZone → teleport to the indicator location;
     KillCreature → teleport to a spawn, `ClientEntitySelect`, `ClientCastSpell` from the action set,
     with `!character level` to make it quick (v1), real combat parity later;
     CollectItem → kill + loot in v2; v1 uses `!item add` and marks the run "cheated: loot".
     Progress is confirmed by `ServerQuestObjectiveUpdate`, not assumed.
   - turn in: teleport to the receiver, TalkTo, `ClientQuestComplete` (reward choice 0) → expect state 3
     (Completed), reward items/XP visible in the next updates.
   - every step has a timeout; on timeout or `QuestException` in the server log the quest is marked
     FAILED with the last state, objective index and the server log excerpt.
6. Report: one JSON + one markdown per run (`docs/reborn/botruns/` on Tower, latest also in the parity
   dashboard), per quest: PASS / FAIL(step) / CHEATED(what), server exceptions, duration. Push
   notification on new failures or regressions against the previous run. The character is deleted at
   the end of the run.

**Shortcuts are allowed but always labelled.** Every GM command the bot uses is counted as a "cheat"
in the report, so the number of cheats per quest becomes the next parity metric to drive to zero.

## Phases

**Phase 0 — spike (one night).** Account via `!account create` over the WebSocket command channel,
token shortcut, create an Exile Soldier, enter Northern Wilds, teleport, accept quest 3479
"From the Wreckage" (level 3, tracker status "ok", never verified by a person), complete it.
Done = a log showing quest state 1 → 3 from real packets, with no GM quest commands used.

**Phase 1 — harness (2 to 3 nights).** Data-driven runner over all 30 Northern Wilds Exile quests in
prerequisite order, objective handlers for the types that zone uses, report + dashboard tab + push,
nightly run at 02:00 (outside the 03:30–05:30 chain) only when the PC and server are up and no deploy
is in progress. The run does not stop Brian playing: separate account, no server restarts.

**Phase 2 — honest login and combat.** STS/SRP client so the full retail login path is exercised,
real spell casts at the right level instead of `!character level`, looting, kill quests without
`!quest kill`. Cheat count per quest trends to zero.

**Phase 3 — the rest of the zone, then the next zone.** Path missions (all four paths, one character
per path), challenges, public events. Then Algoroc, Celestion, … in Brian's levelling order. Later: run
the suite after every server deploy as a gate.

## Division of labour

Claude plans, writes each phase as a bounded spec, verifies against the PC server with a throwaway
account, and reports. Codex (Sol) implements the C# from the spec. This is the standing split on Tower.

## Risks

- Server message readers are the grind: `ServerEntityCreate` alone has 37 entity models. Mitigation:
  parse two models, skip the rest by packet length, add models only when a quest needs them.
- Quest NPCs the bot cannot find (missing spawn, wrong faction, phasing). This is a finding, not a bot
  bug, and it is exactly what the report should surface.
- `ClientEnteredWorld` sent while the server thinks loading is over throws and disconnects. The bot
  must wait for `ServerPlayerCreate` first.
- Combat for real needs the ability inventory (action set slots) parsed from `ServerPlayerCreate`.
  Deferred to phase 2.
- PC availability: the bot needs the PC on. Runs are skipped, not failed, when it is off.

## Not in scope

Dungeons, raids, PvP, housing, the game client, screen automation.
