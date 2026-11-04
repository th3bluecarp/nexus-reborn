using System.Diagnostics;
using System.Numerics;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Loot;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.Bot;

internal sealed class BotRunner
{
    private static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromSeconds(75); // cross-world teleports wait on the server loading map grids
    private static readonly TimeSpan CombatTimeout = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan PerQuestTimeout = TimeSpan.FromMinutes(8);
    private static readonly TimeSpan PerPathMissionTimeout = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan MinimumCombatCastInterval = TimeSpan.FromMilliseconds(500);
    // the server's global cooldown is 0.75 s; a player presses the next ability as soon as it ends
    private static readonly TimeSpan GlobalCooldownWait = TimeSpan.FromMilliseconds(800);
    // an instant ability reports its start at once; rapid-tap abilities only finish after their 4 s threshold window,
    // so the bot stops waiting for the finish after this and lets the global cooldown pace the next cast
    private static readonly TimeSpan CombatCastFinishWait = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan SlotCooldownWait = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan AllSlotsBusyWait = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CombatFacingRefreshInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CombatLocalTargetSearchTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CombatSpawnTargetSearchTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan EntityStreamTimeout = TimeSpan.FromSeconds(15);
    // a gathered settler resource node despawns and comes back on the server's respawn timer; wait that long at the nearest node
    private static readonly TimeSpan SettlerNodeRespawnWait = TimeSpan.FromSeconds(100);
    private static readonly TimeSpan SettlerMissionTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan QuestNpcConfirmationTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CorpseLootTimeout = TimeSpan.FromSeconds(5);
    // a quest started by a dropped item (A Heart of Stone: Stonefist's Heart, 74 % of kills; The Soul of Gloomstone: 60 %): the
    // creature has one spawn point and comes back 60 s after its corpse goes, so a miss costs a respawn wait plus a fight; five
    // tries leave a 1 % chance of failing on bad luck (the 75 s default allowed one kill, the 300 s combat budget three)
    private static readonly TimeSpan ItemDropAcceptTimeout = TimeSpan.FromSeconds(840);
    private static readonly TimeSpan SingleTargetKillRetryTimeout = TimeSpan.FromSeconds(660);
    private const float CombatTargetMovementThreshold = 2f;
    private const float CombatStandOffDistance = 2f;
    private const int MaximumCombatSpawnVisits = 2;

    private readonly RunOptions options;
    private readonly RunReport report;
    private readonly BotLog log;
    private readonly EntityTracker entities = new();
    private readonly Dictionary<uint, uint> objectiveProgress = [];
    private readonly Dictionary<(ushort QuestId, uint Index), uint> allObjectiveProgress = [];
    private readonly Dictionary<ushort, QuestState> questStates = [];
    private readonly Dictionary<ushort, QuestDefinition> questDefinitions = [];
    private readonly HashSet<ushort> completedQuests = [];
    private readonly HashSet<ushort> finishedQuests = [];
    private readonly HashSet<ushort> reportedSkippedQuests = [];
    private readonly HashSet<uint> respondedCsiSpellIds = [];
    private readonly HashSet<uint> pendingActivationCastIds = [];
    private readonly Dictionary<uint, SpellCastResult> activationCastResults = [];
    private readonly HashSet<uint> finishedSpellIds = [];
    private readonly List<string> objectiveCastResultDetails = [];
    private readonly Dictionary<byte, ushort[]> actionSetCombatSlots = [];
    private readonly HashSet<uint> finishedCombatSpellIds = [];
    private readonly Dictionary<ushort, DateTimeOffset> combatSlotBusyUntil = [];
    private readonly HashSet<ushort> combatSlotDiscoveryLoggedQuests = [];
    private readonly Dictionary<uint, ItemDropSources> itemDropSources = [];
    private readonly Dictionary<ulong, InventoryItemInfo> inventory = [];
    private readonly Dictionary<uint, LootNotification> lootNotifications = [];
    private readonly Dictionary<uint, Dictionary<uint, LootItemInfo>> lootGrants = [];
    private readonly List<LootEvidence> combatLoot = [];
    private readonly HashSet<uint> lootedItemIds = [];
    private GenericError? lastGenericError;
    private GenericError? lastItemError;
    // set when the server answers a teleport command with "You have a pending teleport!" (a previous teleport never completed)
    private bool teleportRefused;
    private const int TeleportRefusedRetries = 5;
    private readonly Dictionary<ushort, PathMissionStatus> pathMissionStates = [];
    private readonly List<uint> questNpcAttemptCreatureIds = [];
    private readonly List<string> questNpcAttemptDetails = [];
    /// <summary>
    /// Progress of the current drop-started quest's item hunt, for the accept-step timeout detail ("attempted creature
    /// ids=[none]" said nothing about the 33 kills Carcass Discovery made before its 840 s ran out).
    /// </summary>
    private string? itemHuntProgress;
    private readonly List<string> holdoutProgressPackets = [];
    private readonly List<string> resurrectionPacketsAfterAccept = [];
    private readonly HashSet<uint> usedSettlerResourceSpawnIds = [];

    private BotGameData? gameData;
    private QuestDefinition? quest;
    private QuestRunReport? currentQuestReport;
    private PathMissionRunReport? currentMissionReport;
    private QuestObjectiveDefinition? activeObjective;
    private ResurrectionShow? resurrectionShow;
    private DateTimeOffset resurrectionShowReceivedAt;
    private IReadOnlyList<Spawn> spawns = [];
    private IReadOnlyList<QuestDefinition> questPlan = [];
    private IReadOnlyList<PathMissionDefinition> pathMissionPlan = [];
    private ProtocolClient? world;
    private RealmInfo? realm;
    private ulong characterId;
    private uint playerGuid;
    private uint controlGuid;
    private ushort currentWorldId;
    private Vector3 lastPosition;
    private ushort questWorldId;
    private bool worldLoadPending;
    private bool enteredWorldSent;
    private bool botDied;
    private uint currentLevel = 1u;
    private uint? currentHealth;
    private uint? currentMaxHealth;
    private bool playerInCombat;
    private uint? combatTargetGuid;
    private DateTimeOffset combatStartedAt;
    private uint combatEnemyCasts;
    private uint combatDamageTaken;
    private long? combatFirstEnemyActionMs;
    private bool resurrectionRecoveryActive;
    private string? resurrectionConfirmation;
    private bool resurrectionAcceptSent;
    private int resurrectionPacketOverflow;
    private QuestState questState;
    private ushort chatMessageId;
    private uint spellCastId = 1;
    private byte activeActionSet;
    private bool activeActionSetKnown;
    private Guid gameToken;
    private uint? pendingCombatClientId;
    private SpellCastResult? pendingCombatCastResult;
    private uint? pendingCombatServerId;
    private bool combatTargetDestroyed;
    private bool combatTargetConfirmedDead;
    private DateTimeOffset nextCombatCastAllowedAt;
    private CancellationTokenSource? keepAliveCancellation;
    private Task? keepAliveTask;

    // a frozen bot (the Explorer leg of 2026-11-02: no packet read or written, no keep-alive, for 45 minutes until the
    // nightly's outer timeout killed it) now writes its report and exits 3 from a thread of its own
    private const int WatchdogStallSeconds = 180;
    private volatile bool watchdogStop;
    private volatile string watchdogStep = "";

    private void StartWatchdog()
    {
        var thread = new Thread(() =>
        {
            while (!watchdogStop)
            {
                Thread.Sleep(5000);
                ProtocolClient? client = world;
                if (watchdogStop || client is null || !client.Connected)
                    continue;
                double idle = (DateTime.UtcNow - client.LastActivity).TotalSeconds;
                if (idle < WatchdogStallSeconds)
                    continue;

                string message = $"watchdog: no packet read or written for {idle:0} s (the keep-alive stopped too, the process is frozen); step \"{watchdogStep}\"; quest {currentQuestReport?.QuestId.ToString() ?? "-"}; mission {currentMissionReport?.MissionId.ToString() ?? "-"}; dropped log lines {log.DroppedLines}";
                try { File.WriteAllText(options.Report + ".watchdog.txt", message + Environment.NewLine); }
                catch { }
                try { Console.Error.WriteLine(message); }
                catch { }
                try
                {
                    report.Result = "FAIL";
                    report.Failure = message;
                    report.WriteAsync(options.Report).Wait(TimeSpan.FromSeconds(15));
                }
                catch { }
                Environment.Exit(3);
            }
        }) { IsBackground = true, Name = "bot-watchdog" };
        thread.Start();
    }
    private NexusForever.Game.Static.PlayerPath.Path activePath = NexusForever.Game.Static.PlayerPath.Path.None;
    private bool pathInitialiseReceived;
    private ushort currentPathEpisodeId;
    private PlayerPathSoldierResult? holdoutResult;
    private ushort? activeHoldoutEventId;
    private int? currentHoldoutWave;
    private DateTimeOffset? holdoutNoTargetSince;
    private DateTimeOffset holdoutLastNoTargetLog = DateTimeOffset.MinValue;
    private const float HoldoutTargetRadius = 60f;
    private string? holdoutPreparationDetail;
    private bool scientistScanbotSummoned;
    private ScientistExperimentationInvoke? latestExperimentationInvoke;

    private enum ActivationOutcome
    {
        NoProgress,
        Activated,
        Progressed,
        TargetNotUsable,
        Stunned
    }

    private sealed record ActivationCastResponse(SpellCastStart? Start, SpellCastResult? Result);
    private sealed record CombatFacing(Vector3 TargetPosition, DateTimeOffset SentAt);
    private sealed record QuestNpcUseResult(TrackedEntity Entity, string Attempts);

    private sealed class CombatTargetUnavailableException(string message) : Exception(message);

    private enum CombatCastTransition
    {
        NoResponse,
        CastResult,
        Started,
        TargetDestroyed,
        Progressed
    }

    public BotRunner(RunOptions options, RunReport report, BotLog log)
    {
        this.options = options;
        this.report = report;
        this.log = log;
        gameToken = options.Token ?? Guid.Empty;
        report.PathName = options.Path.ToString().ToLowerInvariant();
    }

    public async Task<int> RunAsync()
    {
        using var wholeRun = new CancellationTokenSource(TimeSpan.FromMinutes(options.MaxMinutes));
        int exitCode = 2;
        bool setupComplete = false;

        try
        {
            await TimedStep.RunAsync(report, "load quest data", DefaultStepTimeout, wholeRun.Token, LoadQuestDataAsync);
            if (options.Sts is not null)
                await LoginToStsAsync(wholeRun.Token);
            await TimedStep.RunAsync(report, "authenticate", DefaultStepTimeout, wholeRun.Token, AuthenticateAsync);
            await TimedStep.RunAsync(report, "connect world", DefaultStepTimeout, wholeRun.Token, ConnectWorldAsync);
            await TimedStep.RunAsync(report, "create character", DefaultStepTimeout, wholeRun.Token, CreateCharacterAsync);
            await TimedStep.RunAsync(report, "enter world", DefaultStepTimeout, wholeRun.Token, EnterWorldAsync);
            setupComplete = true;

            StartKeepAlive(wholeRun.Token);
            StartWatchdog();

            if (options.Paths && options.MissionsFirst)
            {
                // the Explorer, Scientist and Settler legs of a zone get an hour and Algoroc's quest phase alone runs
                // 95 minutes: their missions come first and the quests fill whatever budget is left
                await RunPathMissionsAsync(wholeRun.Token);
                await RunRequestedQuestAsync(wholeRun.Token);
            }
            else
            {
                await RunRequestedQuestAsync(wholeRun.Token);
                if (options.Paths)
                    await RunPathMissionsAsync(wholeRun.Token);
            }

            bool failed = report.Quests.Any(questReport => questReport.Result == "FAIL")
                || report.Missions.Any(missionReport => missionReport.Result == "FAIL");
            report.Result = failed ? "FAIL" : "PASS";
            report.Failure = failed
                ? $"{report.Quests.Count(questReport => questReport.Result == "FAIL")} quest(s) and {report.Missions.Count(missionReport => missionReport.Result == "FAIL")} path mission(s) failed"
                : null;
            exitCode = failed ? 1 : 0;
        }
        catch (Exception exception)
        {
            string failure = exception is OperationCanceledException && wholeRun.IsCancellationRequested
                ? $"whole run timeout after {options.MaxMinutes} minutes"
                : exception.Message;
            report.Result = "FAIL";
            report.Failure = failure;
            log.Info($"FAIL: {failure}");
            exitCode = setupComplete ? 1 : 2;
        }
        finally
        {
            watchdogStop = true;
            await StopKeepAliveAsync();
            await TryCleanupAsync(wholeRun.Token);
            if (world is not null)
                await world.DisposeAsync();
            await report.WriteAsync(options.Report);
        }

        return exitCode;
    }

    private async Task<string> LoadQuestDataAsync(CancellationToken cancellationToken)
    {
        gameData = await Task.Run(() => new BotGameData(options.Tables)).WaitAsync(cancellationToken);
        IReadOnlyList<uint> branchGiverIds = gameData.GetPrerequisiteBranchGiverCreatureIds(options);
        IReadOnlySet<uint> spawnedBranchGivers = await BotDatabase.LoadSpawnedCreatureIdsAsync(
            options.Database, branchGiverIds, cancellationToken);
        questPlan = gameData.GetRunPlan(options, spawnedBranchGivers);
        if (options.Paths)
        {
            pathMissionPlan = gameData.GetPathMissions(
                options.ZoneId ?? throw new BotFailureException("path missions require a zone run"),
                options.Path,
                options.Faction);
        }
        foreach (QuestDefinition definition in questPlan)
            questDefinitions.TryAdd(definition.Id, definition);
        foreach (ushort questId in pathMissionPlan.SelectMany(mission => mission.QuestIds).Distinct())
            questDefinitions.TryAdd(questId, gameData.GetQuest(questId));

        QuestDefinition[] allQuestDefinitions = questDefinitions.Values.ToArray();

        IReadOnlyDictionary<uint, ItemDropSources> loadedItemSources = await BotDatabase.LoadItemDropSourcesAsync(
            options.Database,
            allQuestDefinitions.Select(StartItem)
                .Concat(allQuestDefinitions.SelectMany(definition => definition.Objectives
                    .Where(objective => objective.Type == QuestObjectiveType.CollectItem)
                    .Select(objective => objective.Data))),
            cancellationToken);
        foreach ((uint itemId, ItemDropSources sources) in loadedItemSources)
            itemDropSources[itemId] = sources;

        QuestDefinition? requested = questPlan.Count > 0 ? questPlan[0] : null;
        report.Title = options.ZoneId is uint zoneId ? $"Zone {zoneId} ({options.Faction}, {options.Path})" : requested?.Title ?? "Character only (--quest none)";
        foreach (QuestObjectiveDefinition objective in options.ZoneId is null && options.QuestIds.Count == 1 && requested is not null ? requested.Objectives : [])
        {
            report.Objectives.Add(new ObjectiveReport
            {
                Index = objective.Index,
                Type = objective.Type.ToString(),
                Data = objective.Data,
                Count = objective.Count
            });
        }

        IEnumerable<uint> objectiveCreatures = allQuestDefinitions.SelectMany(definition => definition.Objectives)
            .SelectMany(GetObjectiveCreatureIds);
        IEnumerable<uint> itemSourceCreatures = itemDropSources.Values.SelectMany(sources => sources.CreatureIds);
        IEnumerable<uint> pathCreatures = pathMissionPlan.SelectMany(mission => mission.ObjectiveCreatureIds)
            .Concat(pathMissionPlan.Select(mission => mission.Entry.Creature2IdUnlock))
            .Concat(pathMissionPlan.SelectMany(mission => mission.HoldoutConsoleCreatureIds));
        uint[] creatureIds = allQuestDefinitions.SelectMany(definition => definition.GiverCreatureIds)
            .Concat(allQuestDefinitions.SelectMany(definition => definition.ReceiverCreatureIds))
            .Concat(objectiveCreatures)
            .Concat(itemSourceCreatures)
            .Concat(pathCreatures)
            .Distinct()
            .ToArray();
        IReadOnlyList<Spawn> relevantSpawns = await BotDatabase.LoadSpawnsAsync(options.Database, creatureIds, cancellationToken);
        IReadOnlyList<Spawn> specimenSpawns = await BotDatabase.LoadSpawnsNearLocationsAsync(
            options.Database,
            pathMissionPlan
                .Where(mission => mission.Type == PathMissionType.SpecimenSurvey)
                .SelectMany(mission => mission.Locations),
            30f,
            cancellationToken);
        spawns = relevantSpawns
            .Concat(specimenSpawns)
            .GroupBy(spawn => spawn.Id)
            .Select(group => group.First())
            .ToArray();
        string selfCheck = "";
        QuestDefinition? quest3479 = questPlan.FirstOrDefault(definition => definition.Id == 3479);
        if (quest3479 is not null)
        {
            bool receiverIdMatches = quest3479.ReceiverCreatureIds.Contains(12959u);
            bool receiverSpawnMatches = spawns.Any(spawn => spawn.CreatureId == 12959u
                && spawn.WorldId == 426
                && spawn.DistanceSquared(4486.46f, -724.84f, -5391.26f) <= 100d);
            if (!receiverIdMatches || !receiverSpawnMatches)
                throw new BotFailureException("quest 3479 receiver self-check failed for creature 12959 near world 426 (4486.46, -724.84, -5391.26)");
            selfCheck = "; receiver self-check OK";
        }

        string selection = options.ZoneId is uint selectedZone
            ? $"zone {selectedZone} {options.Faction}"
            : $"quest list {string.Join(',', options.QuestIds)}";
        return $"{selection}; {questPlan.Count} quest(s), {pathMissionPlan.Count} {options.Path} path mission(s) loaded; {spawns.Count} relevant spawns{selfCheck}";
    }

    private IEnumerable<uint> GetObjectiveCreatureIds(QuestObjectiveDefinition objective)
    {
        if (objective.Type == QuestObjectiveType.ActivateEntity)
            return GetActivateCreatureIds(objective);
        // CollectItem: Data is the Item2 id; the creatures that drop it come from the world database loot tables
        if (objective.Type == QuestObjectiveType.CollectItem)
            return itemDropSources.TryGetValue(objective.Data, out ItemDropSources? dropSources) ? dropSources.CreatureIds : [];
        if (objective.Type == QuestObjectiveType.VirtualCollect)
            return GetVirtualCollectCreatureIds(objective);
        if (objective.Type is QuestObjectiveType.ActivateTargetGroup
            or QuestObjectiveType.ActivateTargetGroupChecklist
            or QuestObjectiveType.Unknown10 or QuestObjectiveType.Unknown28)
            return GetTargetGroupActivateCreatureIds(objective);
        if (objective.Type is QuestObjectiveType.KillTargetGroup or QuestObjectiveType.KillTargetGroups
            or QuestObjectiveType.TalkToTargetGroup)
            return RequireGameData().ResolveTargetGroup(RequireGameData().GetObjectiveTargetGroup(objective));
        if (objective.Type is QuestObjectiveType.TalkTo
            or QuestObjectiveType.KillCreature
            or QuestObjectiveType.KillCreature2
            or QuestObjectiveType.SucceedCSI
            or QuestObjectiveType.GatheResource)
            return [objective.Data];
        return [];
    }

    private IReadOnlyList<uint> GetActivateCreatureIds(QuestObjectiveDefinition objective)
    {
        BotGameData data = RequireGameData();
        var ids = new List<uint>();
        if (data.IsCreature(objective.Data))
            ids.Add(objective.Data);
        ids.AddRange(data.ResolveTargetGroup(objective.Data));
        ids.AddRange(data.ResolveTargetGroup(objective.RewardTargetGroupId));
        ids.AddRange(data.GetCreatureIdsFromText(objective.Text));
        return ids.Where(data.IsCreature).Distinct().ToArray();
    }

    private IReadOnlyList<uint> GetTargetGroupActivateCreatureIds(QuestObjectiveDefinition objective)
    {
        BotGameData data = RequireGameData();
        return data.ResolveTargetGroup(objective.Data)
            .Concat(data.ResolveTargetGroup(objective.RewardTargetGroupId))
            .Concat(data.GetCreatureIdsFromText(objective.Text))
            .Where(data.IsCreature)
            .Distinct()
            .ToArray();
    }

    private IReadOnlyList<uint> GetVirtualCollectCreatureIds(QuestObjectiveDefinition objective)
    {
        BotGameData data = RequireGameData();
        uint[] ids = data.ResolveTargetGroup(objective.RewardTargetGroupId).ToArray();
        if (ids.Length == 0)
            ids = data.ResolveTargetGroup(objective.Data).ToArray();
        if (ids.Length == 0)
            ids = data.GetCreatureIdsFromText(objective.Text).ToArray();
        if (ids.Length == 0 && data.IsCreature(objective.Data))
            ids = [objective.Data];
        return ids.Distinct().ToArray();
    }

    private async Task<string> AuthenticateAsync(CancellationToken cancellationToken)
    {
        if (gameToken == Guid.Empty)
            throw new BotFailureException("No game token is available from STS or --token.");

        await using var auth = new ProtocolClient(log);
        await auth.ConnectAsync(options.Auth, 23115, cancellationToken);
        await ReadUntilAsync(auth, packet => packet.Opcode == GameMessageOpcode.ServerHello, cancellationToken);
        await auth.SendEncryptedAsync(GameMessageOpcode.ClientHelloAuth,
            PacketCodec.ClientHelloAuth(options.Email.Trim().ToLowerInvariant(), gameToken), cancellationToken);

        bool accepted = false;
        while (realm is null)
        {
            ProtocolPacket packet = await auth.ReadAsync(cancellationToken);
            switch (packet.Opcode)
            {
                case GameMessageOpcode.ServerAuthAccepted:
                    accepted = true;
                    break;
                case GameMessageOpcode.ServerAuthDenied:
                    throw new BotFailureException($"authentication denied with result {PacketCodec.ParseAuthDenied(packet.Body)}");
                case GameMessageOpcode.ServerRealmInfo:
                    realm = PacketCodec.ParseRealmInfo(packet.Body);
                    break;
            }
        }

        if (!accepted)
            throw new BotFailureException("received realm information without ServerAuthAccepted");
        return $"account {realm.AccountId}; realm {realm.RealmName}; advertised port {realm.Port}";
    }

    private async Task LoginToStsAsync(CancellationToken cancellationToken)
    {
        Endpoint endpoint = options.Sts ?? throw new InvalidOperationException("STS endpoint is unavailable.");
        string password = options.Password ?? throw new InvalidOperationException("STS password is unavailable.");
        await using var sts = new StsClient(endpoint, options.Email, password);

        await RunStsStepAsync("sts connect", cancellationToken, sts.ConnectAsync);
        await RunStsStepAsync("sts login start", cancellationToken, sts.LoginStartAsync);
        await RunStsStepAsync("sts key data", cancellationToken, sts.SendKeyDataAsync);
        await RunStsStepAsync("sts login finish", cancellationToken, sts.LoginFinishAsync);
        await RunStsStepAsync("sts game token", cancellationToken, sts.RequestGameTokenAsync);
        gameToken = sts.GameToken;
    }

    private async Task RunStsStepAsync(
        string name,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<string>> action)
    {
        int reportIndex = report.Steps.Count;
        try
        {
            await TimedStep.RunAsync(report, name, DefaultStepTimeout, cancellationToken, action);
        }
        finally
        {
            if (report.Steps.Count > reportIndex)
            {
                StepReport step = report.Steps[reportIndex];
                log.Info($"{name}: {(step.Ok ? "OK" : "FAIL")} ({step.ElapsedMs} ms): {step.Detail}");
            }
        }
    }

    private async Task<string> ConnectWorldAsync(CancellationToken cancellationToken)
    {
        RealmInfo realmInfo = realm ?? throw new InvalidOperationException("Realm information is unavailable.");
        world = new ProtocolClient(log);
        await world.ConnectAsync(options.World, realmInfo.Port == 0 ? 24000 : realmInfo.Port, cancellationToken);
        await ReadUntilAsync(world, packet => packet.Opcode == GameMessageOpcode.ServerHello, cancellationToken);

        await world.SendEncryptedAsync(GameMessageOpcode.ClientHelloRealm,
            PacketCodec.ClientHelloRealm(realmInfo.AccountId, realmInfo.SessionKey, options.Email), cancellationToken);
        world.SetTicketKey(realmInfo.SessionKey);
        await world.SendEncryptedAsync(GameMessageOpcode.ClientCharacterList, [], cancellationToken);
        await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerCharacterList, cancellationToken);
        return $"connected to {options.World.Host}:{options.World.GetPort(realmInfo.Port == 0 ? 24000 : realmInfo.Port)}; character list received";
    }

    private async Task<string> CreateCharacterAsync(CancellationToken cancellationToken)
    {
        CharacterCreationEntry creation = RequireGameData().GetBotCharacterCreation(options.Start, options.Faction);
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            // --keep-character: a named character that stays on the account (the GUI click-through logs in as it)
            report.CharacterName = options.KeepCharacter ?? $"Bot {Random.Shared.Next(0, 10000):0000}";
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientCharacterCreate,
                PacketCodec.ClientCharacterCreate(creation, report.CharacterName, options.Path), cancellationToken);

            ProtocolPacket packet = await ReadWorldUntilAsync(
                candidate => candidate.Opcode == GameMessageOpcode.ServerCharacterCreate, cancellationToken);
            CharacterCreateResult result = PacketCodec.ParseCharacterCreate(packet.Body);
            if (result.Result == (byte)CharacterModifyResult.CreateFailed_UniqueName && attempt < 5)
                continue;
            if (result.Result != (byte)CharacterModifyResult.CreateOk)
                throw new BotFailureException($"character creation failed with result {result.Result}");
            if (result.CharacterId == 0)
                throw new BotFailureException("character creation returned character id 0");

            characterId = result.CharacterId;
            return $"{report.CharacterName}; character {characterId}; path {options.Path}; creation {creation.Id} ({creation.CharacterCreationStartEnum}); world {result.WorldId}";
        }

        throw new BotFailureException("character creation exhausted name retries");
    }

    private async Task<string> EnterWorldAsync(CancellationToken cancellationToken)
    {
        worldLoadPending = true;
        enteredWorldSent = false;
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientCharacterSelect,
            PacketCodec.ClientCharacterSelect(characterId), cancellationToken);
        await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerPlayerEnteredWorld, cancellationToken);
        if (worldLoadPending)
            throw new BotFailureException("ServerPlayerEnteredWorld arrived without completing the world-load state");
        if (playerGuid == 0)
            throw new BotFailureException("world entry completed without a player unit guid");
        await DrainWorldPacketsAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
        return $"world {currentWorldId}; player guid {playerGuid}; player entity ready";
    }

    private async Task RunRequestedQuestAsync(CancellationToken cancellationToken)
    {
        // reputation quests last: the zone's other quests pay the reputation they ask for
        QuestDefinition[] orderedPlan = questPlan
            .Where(definition => !HasReputationObjective(definition))
            .Concat(questPlan.Where(HasReputationObjective))
            .ToArray();
        for (int planIndex = 0; planIndex < orderedPlan.Length; planIndex++)
        {
            QuestDefinition definition = orderedPlan[planIndex];
            cancellationToken.ThrowIfCancellationRequested();
            // the report is written after every quest so a run in progress can be watched (the dashboard shows it as running)
            if (planIndex > 0)
            {
                try
                {
                    await report.WriteAsync(options.Report);
                }
                catch (Exception exception)
                {
                    log.Info($"progress report write failed: {exception.Message}");
                }
            }
            if (botDied)
            {
                // a dead character cannot accept or fight: try the resurrection once more, else stop the run honestly
                // instead of failing every remaining quest with a misleading reason (Algoroc 2026-11-02)
                QuestRunReport deadReport = CreateQuestReport(definition);
                await TryRecoverFromDeathAsync(deadReport, cancellationToken);
                if (botDied)
                {
                    string reason = "not run: the character is dead and resurrection failed twice";
                    log.Info($"{reason}; {orderedPlan.Length - planIndex} quest(s) left");
                    deadReport.Result = "FAIL";
                    deadReport.Failure = reason;
                    deadReport.EndTimestamp = DateTimeOffset.UtcNow;
                    report.Quests.Add(deadReport);
                    foreach (QuestDefinition rest in orderedPlan.Skip(planIndex + 1))
                    {
                        QuestRunReport restReport = CreateQuestReport(rest);
                        restReport.Result = "FAIL";
                        restReport.Failure = reason;
                        restReport.EndTimestamp = DateTimeOffset.UtcNow;
                        report.Quests.Add(restReport);
                    }
                    break;
                }
                log.Info($"character resurrected before quest {definition.Id}");
            }
            if (StartItem(definition) is uint startItem && startItem != 0u
                && (!itemDropSources.TryGetValue(startItem, out ItemDropSources? sources)
                    || sources.CreatureIds.Count == 0))
            {
                AddItemStartedQuestReport(definition, $"SKIPPED: item {startItem} has no drop source in the world database");
                finishedQuests.Add(definition.Id);
                continue;
            }
            // a start item that only drops from group content (A Miner's Trophy: a Chunk of Stalagmight from the public event's
            // 5-man tank) is never farmed by a lone bot
            if (StartItem(definition) is uint groupItem && groupItem != 0u
                && itemDropSources.TryGetValue(groupItem, out ItemDropSources? groupSources)
                && groupSources.CreatureIds.Count > 0
                && groupSources.CreatureIds.All(id => RequireGameData().GroupSizeOfCreature(id) > 0u))
            {
                uint groupSize = groupSources.CreatureIds.Max(id => RequireGameData().GroupSizeOfCreature(id));
                AddItemStartedQuestReport(definition, $"SKIPPED: item {groupItem} drops only from group content creatures [{string.Join(',', groupSources.CreatureIds)}] ({groupSize}-player difficulty); the bot plays alone");
                finishedQuests.Add(definition.Id);
                continue;
            }
            var prerequisiteContext = new PrerequisiteContext(
                BotGameData.BotClass,
                options.Path,
                Math.Max(currentLevel, definition.Entry.ConLevel + 1u),
                id => questStates.GetValueOrDefault(id, QuestState.Unknown));
            if ((RequireGameData().QuestSkipReason(definition.Entry)
                ?? InstanceSkipReason(definition)
                ?? RequireGameData().PrerequisiteBlockReason(definition.Entry, prerequisiteContext)) is string skipReason)
            {
                AddNotForCharacterQuestReport(definition, skipReason);
                finishedQuests.Add(definition.Id);
                continue;
            }
            if (completedQuests.Contains(definition.Id))
            {
                AddSkippedQuestReport(definition);
                finishedQuests.Add(definition.Id);
                continue;
            }

            ushort? missingPrerequisite = GetMissingPrerequisite(definition);
            if (missingPrerequisite is not null)
            {
                AddBlockedQuestReport(definition, missingPrerequisite.Value);
                finishedQuests.Add(definition.Id);
                continue;
            }
            TimeSpan questTimeout = QuestBudget(definition);
            using var questBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            questBudget.CancelAfter(questTimeout);
            try
            {
                await RunQuestAsync(definition, questBudget.Token);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                QuestRunReport failedReport = currentQuestReport
                    ?? throw new InvalidOperationException($"Quest {definition.Id} failed without a report.");
                if (exception is OperationCanceledException && questBudget.IsCancellationRequested)
                {
                    failedReport.Failure = $"per-quest timeout after {questTimeout.TotalMinutes:0.#} minutes";
                    failedReport.Steps.Add(new StepReport
                    {
                        Name = "quest budget",
                        Ok = false,
                        ElapsedMs = (long)questTimeout.TotalMilliseconds,
                        Detail = failedReport.Failure
                    });
                }
                await TryRecoverFromDeathAsync(failedReport, cancellationToken);
                await TryAbandonFailedQuestAsync(definition, failedReport, cancellationToken);
                try
                {
                    await DrainWorldPacketsAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
                }
                catch (Exception drainException)
                {
                    log.Info($"quest {definition.Id} trailing packet drain stopped: {drainException.Message}");
                }
                log.Info($"quest {definition.Id} FAIL: {failedReport.Failure}");
            }
            finally
            {
                finishedQuests.Add(definition.Id);
                if (currentQuestReport is not null)
                    currentQuestReport.EndTimestamp = DateTimeOffset.UtcNow;
                currentQuestReport = null;
            }
        }
    }

    private async Task RunPathMissionsAsync(CancellationToken cancellationToken)
    {
        if (!pathInitialiseReceived)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (!pathInitialiseReceived)
            {
                TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero
                    || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                    throw new BotFailureException("ServerPathInitialise was not received before the path mission phase");
            }
        }
        var expectedPath = (NexusForever.Game.Static.PlayerPath.Path)(byte)options.Path;
        if (activePath != expectedPath)
            throw new BotFailureException($"character active path is {activePath}, expected {expectedPath}");

        foreach (PathMissionDefinition definition in pathMissionPlan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentMissionReport = new PathMissionRunReport
            {
                MissionId = definition.Id,
                Path = definition.Path.ToString().ToLowerInvariant(),
                Name = definition.Name,
                Type = definition.TypeName
            };
            report.Missions.Add(currentMissionReport);

            if (definition.Type is PathMissionType.Checklist or PathMissionType.FieldStudy && definition.ScanCreatureGroups.Count == 0)
            {
                currentMissionReport.Result = "UNSUPPORTED";
                currentMissionReport.FailedAt = "objective";
                currentMissionReport.Detail = $"UNSUPPORTED: no scan rule for mission type {definition.RawType} ({definition.TypeName}) mission {definition.Id}";
                currentMissionReport.EndTimestamp = DateTimeOffset.UtcNow;
                log.Info($"path mission {definition.Id}: {currentMissionReport.Detail}");
                currentMissionReport = null;
                continue;
            }

            if (GetPathMissionStatus(definition.Id)?.Completed == true)
            {
                currentMissionReport.Result = "PASS";
                currentMissionReport.Detail = FormatAlreadyCompletedPathMission(definition);
                log.Info($"path mission {definition.Id} PASS: {currentMissionReport.Detail}");
                currentMissionReport.EndTimestamp = DateTimeOffset.UtcNow;
                currentMissionReport = null;
                continue;
            }

            if (definition.Type is not (PathMissionType.Holdout
                or PathMissionType.Assassination
                or PathMissionType.SWAT
                or PathMissionType.Demolition
                or PathMissionType.Rescue
                or PathMissionType.StakingClaim
                or PathMissionType.Surveillance
                or PathMissionType.Vista
                or PathMissionType.Tracking
                or PathMissionType.Cartography
                or PathMissionType.Scan
                or PathMissionType.Expansion
                or PathMissionType.Mayor
                or PathMissionType.Scout
                or PathMissionType.Sheriff
                or PathMissionType.Infrastructure
                or PathMissionType.Exploration
                or PathMissionType.ScavengerHunt
                or PathMissionType.Experimentation
                or PathMissionType.SpecimenSurvey
                or PathMissionType.DatacubeDiscovery
                or PathMissionType.Checklist
                or PathMissionType.FieldStudy))
            {
                currentMissionReport.Result = "UNSUPPORTED";
                currentMissionReport.FailedAt = "objective";
                currentMissionReport.Detail = $"UNSUPPORTED: unsupported mission type {definition.TypeName} ({definition.RawType})";
                currentMissionReport.EndTimestamp = DateTimeOffset.UtcNow;
                log.Info($"path mission {definition.Id}: {currentMissionReport.Detail}");
                currentMissionReport = null;
                continue;
            }
            // Brian 2026-11-03: the Sheriff missions stay group content. Algoroc's three PUBLIC SAFETY missions each want a
            // WANTED [GROUP 2+] boss (difficulty 6) that a solo character cannot kill; the bot reports them skipped instead
            // of dying three times per settler leg
            if (definition.Type == PathMissionType.Sheriff)
            {
                string[] groupQuests = definition.QuestIds
                    .Select(questId => (questDefinitions.GetValueOrDefault(questId) ?? RequireGameData().GetQuest(questId)).Entry)
                    .Where(entry => entry.GroupSize >= 2u)
                    .Select(entry => $"{entry.Id} ({RequireGameData().QuestSkipReason(entry)})")
                    .ToArray();
                if (groupQuests.Length > 0)
                {
                    currentMissionReport.Result = "SKIPPED";
                    currentMissionReport.FailedAt = "objective";
                    currentMissionReport.Detail = $"SKIPPED: group content by decision (2026-11-03): sheriff quest(s) [{string.Join(", ", groupQuests)}]";
                    currentMissionReport.EndTimestamp = DateTimeOffset.UtcNow;
                    log.Info($"path mission {definition.Id}: {currentMissionReport.Detail}");
                    currentMissionReport = null;
                    continue;
                }
            }
            if (definition.Type == PathMissionType.Cartography && definition.CartographyPoints.Count == 0)
            {
                currentMissionReport.Result = "UNSUPPORTED";
                currentMissionReport.FailedAt = "objective";
                currentMissionReport.Detail = "UNSUPPORTED: client zone-map packet ClientZoneChange (0x063A) carries only previous/new zone ids; no client chunk-report packet exists and no MapZone hex grid could be resolved";
                currentMissionReport.EndTimestamp = DateTimeOffset.UtcNow;
                log.Info($"path mission {definition.Id}: {currentMissionReport.Detail}");
                currentMissionReport = null;
                continue;
            }

            TimeSpan missionTimeout = definition.Type is PathMissionType.Expansion
                or PathMissionType.Sheriff
                or PathMissionType.Infrastructure
                ? SettlerMissionTimeout
                : PerPathMissionTimeout;
            using var missionBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            missionBudget.CancelAfter(missionTimeout);
            string phase = "location";
            try
            {
                if (definition.Locations.Count == 0
                    && definition.Type is not (PathMissionType.Cartography
                        or PathMissionType.Sheriff
                        or PathMissionType.Infrastructure
                        or PathMissionType.Exploration
                        or PathMissionType.ScavengerHunt
                        or PathMissionType.Experimentation
                        or PathMissionType.DatacubeDiscovery))
                    throw new BotFailureException("mission has no WorldLocation2 location");
                questWorldId = checked((ushort)definition.Episode.WorldId);
                if (definition.Locations.Count > 0)
                {
                    await TeleportToLocationAsync(definition.Locations[0], 0f, missionBudget.Token);
                    await DrainWorldPacketsAsync(TimeSpan.FromMilliseconds(500), missionBudget.Token);
                }

                phase = "unlock";
                await UnlockPathMissionIfNeededAsync(definition, missionBudget.Token);
                if (GetPathMissionStatus(definition.Id)?.Completed == true)
                {
                    currentMissionReport.Result = "PASS";
                    currentMissionReport.Detail = FormatCompletedPathMission(definition, "while unlocking");
                    continue;
                }

                phase = "objective";
                string? objectiveDetail = null;
                switch (definition.Type)
                {
                    // a SWAT mission with a retail gadget uses its targets (Fire in the Hole: Burrow Busters on canimid burrows,
                    // objects no attack lands on; the bot hit one for six minutes); the server counts the use as the gadget
                    case PathMissionType.SWAT when definition.GadgetTargets:
                        await RunPathActivateMissionAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.Assassination:
                    case PathMissionType.SWAT:
                        await RunPathKillMissionAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.Demolition:
                    case PathMissionType.Rescue:
                    case PathMissionType.Exploration:
                        await RunPathActivateMissionAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.Holdout:
                        await RunHoldoutMissionAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.StakingClaim:
                    case PathMissionType.Surveillance:
                    case PathMissionType.Vista:
                    case PathMissionType.Tracking:
                    case PathMissionType.Mayor:
                    case PathMissionType.Scout:
                        objectiveDetail = await RunExplorerLocationsAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.Cartography:
                        objectiveDetail = await RunExplorerCartographyAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.Scan:
                    case PathMissionType.Checklist:
                    case PathMissionType.FieldStudy:
                        objectiveDetail = await RunScientistScanMissionAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.Expansion:
                        objectiveDetail = await RunSettlerExpansionMissionAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.Sheriff:
                        objectiveDetail = await RunSettlerSheriffMissionAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.ScavengerHunt:
                        objectiveDetail = await RunExplorerScavengerHuntAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.DatacubeDiscovery:
                        objectiveDetail = await RunScientistDatacubeMissionAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.SpecimenSurvey:
                        objectiveDetail = await RunScientistSpecimenSurveyMissionAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.Infrastructure:
                        objectiveDetail = await RunSettlerInfrastructureMissionAsync(definition, missionBudget.Token);
                        break;
                    case PathMissionType.Experimentation:
                        objectiveDetail = await RunScientistExperimentationMissionAsync(definition, missionBudget.Token);
                        break;
                }

                if (definition.Type != PathMissionType.Holdout
                    && GetPathMissionStatus(definition.Id)?.Completed != true)
                    throw new BotFailureException("server did not report the mission complete");
                currentMissionReport.Result = "PASS";
                PathMissionStatus? status = GetPathMissionStatus(definition.Id);
                currentMissionReport.Detail = objectiveDetail ?? (definition.Type == PathMissionType.Holdout
                    ? $"holdout end={holdoutResult}; progress={status?.Progress.ToString() ?? "not-received"}/{definition.Count}; episode={definition.Episode.Id}"
                    : $"complete; progress={status!.Progress}/{definition.Count}; episode={definition.Episode.Id}");
                if (definition.Type == PathMissionType.Holdout)
                    currentMissionReport.Detail += FormatHoldoutMissionDetail();
                log.Info($"path mission {definition.Id} PASS: {currentMissionReport.Detail}");
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                currentMissionReport.Result = "FAIL";
                currentMissionReport.FailedAt = phase;
                bool holdoutDeath = definition.Type == PathMissionType.Holdout
                    && (botDied || holdoutResult == PlayerPathSoldierResult.FailDeath);
                currentMissionReport.Detail = holdoutDeath
                    ? $"FAIL: died in wave {FormatCurrentHoldoutWave()}"
                    : exception is OperationCanceledException && missionBudget.IsCancellationRequested
                        ? $"mission timeout after {missionTimeout.TotalMinutes:0} minutes"
                        : exception.Message;
                if (holdoutDeath && !botDied)
                {
                    try
                    {
                        DateTimeOffset deathDeadline = DateTimeOffset.UtcNow.AddSeconds(2);
                        while (!botDied && DateTimeOffset.UtcNow < deathDeadline)
                        {
                            TimeSpan remaining = deathDeadline - DateTimeOffset.UtcNow;
                            if (remaining <= TimeSpan.Zero
                                || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                                break;
                        }
                    }
                    catch (BotFailureException deathException)
                    {
                        log.Info($"path mission {definition.Id}: death confirmation: {deathException.Message}");
                    }
                }
                if (botDied)
                {
                    var recoveryReport = new QuestRunReport
                    {
                        QuestId = 0,
                        Title = $"path mission {definition.Id}",
                        CharacterName = report.CharacterName
                    };
                    await TryRecoverFromDeathAsync(recoveryReport, cancellationToken);
                    StepReport? recovery = recoveryReport.Steps.LastOrDefault(step => step.Name == "recover from death");
                    if (recovery is not null)
                        currentMissionReport.Detail += $"; death recovery {(recovery.Ok ? "succeeded" : "failed")}: {recovery.Detail}";
                }
                try
                {
                    await DrainWorldPacketsAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
                }
                catch (Exception drainException)
                {
                    log.Info($"path mission {definition.Id} trailing packet drain stopped: {drainException.Message}");
                }
                if (definition.Type == PathMissionType.Holdout)
                    currentMissionReport.Detail += FormatHoldoutMissionDetail();
                log.Info($"path mission {definition.Id} FAIL at {phase}: {currentMissionReport.Detail}");
            }
            finally
            {
                currentMissionReport.EndTimestamp = DateTimeOffset.UtcNow;
                currentMissionReport = null;
                holdoutResult = null;
                activeHoldoutEventId = null;
                currentHoldoutWave = null;
                holdoutProgressPackets.Clear();
                holdoutPreparationDetail = null;
            }
        }

        if (scientistScanbotSummoned)
        {
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientPathScientistDismissScanbot,
                PacketCodec.ClientPathScientistDismissScanbot(), cancellationToken);
            scientistScanbotSummoned = false;
        }
    }

    private string FormatAlreadyCompletedPathMission(PathMissionDefinition definition)
        => FormatCompletedPathMission(definition, "before mission phase", completedDuringQuestPhase: true);

    private string FormatCompletedPathMission(
        PathMissionDefinition definition,
        string timing,
        bool completedDuringQuestPhase = false)
    {
        PathMissionStatus? status = GetPathMissionStatus(definition.Id);
        string prefix = completedDuringQuestPhase
            ? $"completed during the quest phase (server progress={status?.Progress.ToString() ?? "?"}/{definition.Count} before the mission phase; a player walking the zone completes it the same way)"
            : $"mission completed {timing} (server progress={status?.Progress.ToString() ?? "?"}/{definition.Count})";
        return definition.Type switch
        {
            PathMissionType.Mayor or PathMissionType.Scout =>
                $"locations reached {definition.Locations.Count}/{definition.Locations.Count} {timing}: "
                + $"{string.Join(", ", definition.Locations.Select((location, index) => FormattableString.Invariant($"{index + 1}:{location.Id}@({location.Position0:0.###},{location.Position1:0.###},{location.Position2:0.###}) already-set")))}; episode={definition.Episode.Id}",
            PathMissionType.Sheriff =>
                $"quests done=[{string.Join(',', definition.QuestIds)}] {timing}; progress=0x{status?.Progress ?? 0u:X}; episode={definition.Episode.Id}",
            PathMissionType.ScavengerHunt =>
                $"clues solved {definition.ScavengerClues.Count}/{definition.ScavengerClues.Count} {timing}: "
                + $"{string.Join(", ", definition.ScavengerClues.Select(clue => clue.CreatureIds.Count > 0 ? $"clue={clue.Id}:activated/already-set" : $"clue={clue.Id}:location={clue.Location?.Id.ToString() ?? "unknown"}/already-set"))}; progress=0x{status?.Progress ?? 0u:X}; episode={definition.Episode.Id}",
            PathMissionType.DatacubeDiscovery =>
                $"datacubes opened {definition.ObjectiveCreatureIds.Count}/{definition.ObjectiveCreatureIds.Count} {timing}: [{string.Join(',', definition.ObjectiveCreatureIds)}]; progress=0x{status?.Progress ?? 0u:X}; episode={definition.Episode.Id}",
            PathMissionType.SpecimenSurvey =>
                $"locations sampled {definition.Locations.Count}/{definition.Locations.Count} {timing}: [{string.Join(',', definition.Locations.Select(location => location.Id))}]; progress=0x{status?.Progress ?? 0u:X}; episode={definition.Episode.Id}",
            PathMissionType.Infrastructure =>
                $"gathers=not-observed; deliveries={status?.Progress.ToString() ?? "?"}/{definition.Count} {timing}; episode={definition.Episode.Id}",
            PathMissionType.Experimentation =>
                $"code=not-observed; attempt=not-observed; completed {timing}; episode={definition.Episode.Id}",
            _ => $"{prefix}; episode={definition.Episode.Id}"
        };
    }

    private async Task<string> RunExplorerLocationsAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        PathMissionRunReport missionReport = currentMissionReport
            ?? throw new InvalidOperationException("Location mission report is unavailable.");

        for (int index = 0; index < definition.Locations.Count; index++)
        {
            uint bit = 1u << index;
            WorldLocation2Entry location = definition.Locations[index];
            string reached = FormattableString.Invariant(
                $"{index + 1}:{location.Id}@({location.Position0:0.###},{location.Position1:0.###},{location.Position2:0.###})");
            if ((GetPathMissionStatus(definition.Id)?.Progress & bit) != 0u)
            {
                missionReport.LocationsReached.Add(reached + " already-set");
                continue;
            }

            await TeleportToLocationAsync(location, 0f, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            await DrainWorldPacketsAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
            bool set = await WaitForPathMissionAsync(
                definition.Id,
                status => (status.Progress & bit) != 0u || status.Completed,
                TimeSpan.FromSeconds(7),
                cancellationToken);
            if (!set)
            {
                log.Info($"path mission {definition.Id}: location bit {index} did not set in 10 s; nudging 5 m");
                await TeleportToLocationAsync(location, 5f, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                await DrainWorldPacketsAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
                set = await WaitForPathMissionAsync(
                    definition.Id,
                    status => (status.Progress & bit) != 0u || status.Completed,
                    TimeSpan.FromSeconds(7),
                    cancellationToken);
                reached += " nudge=5m";
            }

            if (!set)
                throw new BotFailureException($"location {index + 1}/{definition.Locations.Count} did not set progress bit 0x{bit:X}; reached=[{string.Join(", ", missionReport.LocationsReached)}]");
            missionReport.LocationsReached.Add(reached);
        }

        PathMissionStatus? status = GetPathMissionStatus(definition.Id);
        return $"locations reached {missionReport.LocationsReached.Count}/{definition.Locations.Count}: {string.Join(", ", missionReport.LocationsReached)}; progress=0x{status?.Progress ?? 0u:X}; episode={definition.Episode.Id}";
    }

    private async Task<string> RunExplorerScavengerHuntAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.ScavengerClues.Count == 0)
            throw new BotFailureException($"PathExplorerScavengerHunt {definition.Entry.ObjectId} resolved to no clues");

        var solved = new List<string>();
        for (int index = 0; index < definition.ScavengerClues.Count; index++)
        {
            uint bit = 1u << index;
            ScavengerClueDefinition clue = definition.ScavengerClues[index];
            if ((GetPathMissionStatus(definition.Id)?.Progress & bit) != 0u)
            {
                solved.Add($"clue={clue.Id}:already-set");
                continue;
            }

            if (clue.CreatureIds.Count > 0)
            {
                IReadOnlyList<Spawn> candidates = SpawnsFor(clue.CreatureIds, checked((ushort)definition.Episode.WorldId));
                if (candidates.Count == 0)
                    throw new BotFailureException($"scavenger clue {clue.Id} has no spawn for creatures [{string.Join(',', clue.CreatureIds)}]");
                // the clue's own map location, when it has one, says which of the creature's spawns is meant: those within its
                // radius first; the hunt of 2026-11-02 walked 30 spawns of a common creature for six minutes and timed out
                Spawn[] ordered = clue.Location is { } clueLocation
                    ? candidates
                        .OrderBy(candidate => candidate.DistanceSquared(clueLocation.Position0, clueLocation.Position1, clueLocation.Position2))
                        .Take(ScavengerClueSpawnAttempts)
                        .ToArray()
                    : candidates
                        .OrderBy(candidate => candidate.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
                        .Take(ScavengerClueSpawnAttempts)
                        .ToArray();
                log.Info($"path mission {definition.Id}: clue {clue.Id} ({index + 1}/{definition.ScavengerClues.Count}) creature(s) [{string.Join(',', clue.CreatureIds)}], {candidates.Count} spawn(s), trying {ordered.Length}"
                    + (clue.Location is { } at ? FormattableString.Invariant($" nearest location {at.Id}@({at.Position0:0.#},{at.Position1:0.#},{at.Position2:0.#})") : ""));

                bool progressed = false;
                foreach (Spawn spawn in ordered)
                {
                    TrackedEntity? entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
                    if (entity is null)
                    {
                        log.Info($"path mission {definition.Id}: clue {clue.Id} spawn {spawn.Id} (creature {spawn.CreatureId}) not visible");
                        continue;
                    }
                    ActivationOutcome outcome = await RunPathActivationFlowAsync(
                        entity,
                        () => (GetPathMissionStatus(definition.Id)?.Progress & bit) != 0u || IsPathMissionComplete(definition.Id),
                        TimeSpan.FromSeconds(8),
                        cancellationToken);
                    log.Info($"path mission {definition.Id}: clue {clue.Id} spawn {spawn.Id} guid {entity.Guid}: {outcome}; progress=0x{GetPathMissionStatus(definition.Id)?.Progress ?? 0u:X}");
                    if (outcome == ActivationOutcome.Progressed)
                    {
                        solved.Add($"clue={clue.Id}:activated creature={spawn.CreatureId}/guid={entity.Guid}");
                        progressed = true;
                        break;
                    }
                }
                if (!progressed)
                    throw new BotFailureException($"scavenger clue {clue.Id} activation produced no progress bit 0x{bit:X}; solved=[{string.Join(", ", solved)}]");
            }
            else
            {
                WorldLocation2Entry location = clue.Location
                    ?? throw new BotFailureException($"scavenger clue {clue.Id} has neither a creature/target group nor WorldLocation2IdMiniMap");
                log.Info(FormattableString.Invariant($"path mission {definition.Id}: clue {clue.Id} ({index + 1}/{definition.ScavengerClues.Count}) stand at location {location.Id}@({location.Position0:0.#},{location.Position1:0.#},{location.Position2:0.#}) radius {Math.Max(location.Radius, clue.ActiveRadius):0.#}"));
                await TeleportToLocationAsync(location, 0f, cancellationToken);
                bool progressed = await WaitForPathMissionAsync(
                    definition.Id,
                    status => (status.Progress & bit) != 0u || status.Completed,
                    TimeSpan.FromSeconds(5),
                    cancellationToken);
                if (!progressed)
                    throw new BotFailureException($"scavenger clue {clue.Id} location {location.Id} (active radius {clue.ActiveRadius:0.###}) produced no progress bit 0x{bit:X}; solved=[{string.Join(", ", solved)}]");
                solved.Add(FormattableString.Invariant(
                    $"clue={clue.Id}:location={location.Id}@({location.Position0:0.###},{location.Position1:0.###},{location.Position2:0.###})/radius={Math.Max(location.Radius, clue.ActiveRadius):0.###}"));
            }
        }

        PathMissionStatus? status = GetPathMissionStatus(definition.Id);
        return $"clues solved {solved.Count}/{definition.ScavengerClues.Count}: {string.Join(", ", solved)}; progress=0x{status?.Progress ?? 0u:X}; episode={definition.Episode.Id}";
    }

    private async Task<string> RunExplorerCartographyAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        int visited = 0;
        foreach (CartographyPoint point in definition.CartographyPoints)
        {
            if (IsPathMissionComplete(definition.Id))
                break;

            bool includeWorld = currentWorldId != point.WorldId;
            if (includeWorld)
                entities.Clear();
            var destination = new Vector3(point.X, point.Y, point.Z);
            string command = PacketCodec.TeleportCommand(destination);
            if (includeWorld)
                command += $" {point.WorldId}";
            await SendTeleportAsync(command, includeWorld, cancellationToken);
            if (includeWorld)
                await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerPlayerEnteredWorld, cancellationToken);
            lastPosition = destination + new Vector3(2f, 0f, 2f);
            visited++;

            uint before = GetPathMissionStatus(definition.Id)?.Progress ?? 0u;
            _ = await WaitForPathMissionAsync(
                definition.Id,
                status => status.Progress > before || status.Completed,
                TimeSpan.FromSeconds(3),
                cancellationToken);
        }

        _ = await WaitForPathMissionAsync(
            definition.Id,
            status => status.Completed || status.Progress >= definition.Count,
            TimeSpan.FromSeconds(3),
            cancellationToken);
        PathMissionStatus? final = GetPathMissionStatus(definition.Id);
        if (final?.Completed != true)
            throw new BotFailureException($"cartography visited {visited}/{definition.CartographyPoints.Count} MapZone hex groups but server progress is {final?.Progress.ToString() ?? "not-received"}%/{definition.Count}%");
        return $"cartography complete; visited {visited}/{definition.CartographyPoints.Count} MapZone hex groups; progress={final.Progress}%/{definition.Count}%; server ZoneMapManager.OnRelocate (no client zone-map chunk packet); episode={definition.Episode.Id}";
    }

    private async Task<string> RunScientistScanMissionAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        PathMissionRunReport missionReport = currentMissionReport
            ?? throw new InvalidOperationException("Scientist mission report is unavailable.");
        if (definition.ScanCreatureGroups.Count == 0 || definition.ScansPerGroup == 0u)
            throw new BotFailureException($"no server ScanRules mapping was resolved for scientist mission {definition.Id}");

        await EnsureScientistScanbotAsync(cancellationToken);

        var usedSpawnIds = new HashSet<uint>();
        var usedEntityGuids = new HashSet<uint>();
        for (int groupIndex = 0; groupIndex < definition.ScanCreatureGroups.Count && !IsPathMissionComplete(definition.Id); groupIndex++)
        {
            IReadOnlyList<uint> group = definition.ScanCreatureGroups[groupIndex];
            for (uint scan = 0u; scan < definition.ScansPerGroup && !IsPathMissionComplete(definition.Id); scan++)
            {
                Spawn[] candidates = SpawnsFor(group, checked((ushort)definition.Episode.WorldId))
                    .Where(spawn => !usedSpawnIds.Contains(spawn.Id))
                    .OrderBy(spawn => spawn.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
                    .ToArray();
                TrackedEntity? entity = null;
                Spawn? selectedSpawn = null;
                foreach (Spawn candidate in candidates)
                {
                    usedSpawnIds.Add(candidate.Id);
                    entity = await TeleportAndTryFindWithRetryAsync(candidate, cancellationToken, usedEntityGuids);
                    if (entity is not null)
                    {
                        selectedSpawn = candidate;
                        break;
                    }
                }

                if (entity is null || selectedSpawn is null)
                {
                    foreach (uint creatureId in group.Where(id => !missionReport.CreaturesNeverFound.Contains(id)))
                        missionReport.CreaturesNeverFound.Add(creatureId);
                    break;
                }

                uint before = GetPathMissionStatus(definition.Id)?.Progress ?? 0u;
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                    PacketCodec.ClientEntitySelect(entity.Guid), cancellationToken);
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientPathScientistScan,
                    PacketCodec.ClientPathScientistScan(), cancellationToken);
                bool progressed = await WaitForPathMissionAsync(
                    definition.Id,
                    status => status.Progress > before || status.Completed,
                    TimeSpan.FromSeconds(10),
                    cancellationToken);
                if (!progressed)
                    throw new BotFailureException($"scan of creature {selectedSpawn.CreatureId} guid {entity.Guid} produced no ServerPathMissionUpdate in 10 s; scanned=[{string.Join(',', missionReport.CreaturesScanned)}]");
                missionReport.CreaturesScanned.Add(selectedSpawn.CreatureId);
                usedEntityGuids.Add(entity.Guid);
            }
        }

        if (missionReport.CreaturesNeverFound.Count > 0 || !IsPathMissionComplete(definition.Id))
            throw new BotFailureException($"scientist scan incomplete; scanned=[{string.Join(',', missionReport.CreaturesScanned)}]; never found=[{string.Join(',', missionReport.CreaturesNeverFound)}]; progress={GetPathMissionStatus(definition.Id)?.Progress.ToString() ?? "not-received"}");
        return $"creatures scanned=[{string.Join(',', missionReport.CreaturesScanned)}]; never found=[{string.Join(',', missionReport.CreaturesNeverFound)}]; progress={GetPathMissionStatus(definition.Id)!.Progress}; episode={definition.Episode.Id}";
    }

    private async Task EnsureScientistScanbotAsync(CancellationToken cancellationToken)
    {
        if (scientistScanbotSummoned)
            return;
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientPathScientistRequestScanbot,
            PacketCodec.ClientPathScientistRequestScanbot(), cancellationToken);
        scientistScanbotSummoned = true;
        await DrainWorldPacketsAsync(TimeSpan.FromSeconds(1), cancellationToken);
    }

    private async Task<string> RunScientistSpecimenSurveyMissionAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.Locations.Count == 0)
            throw new BotFailureException($"PathScientistSpecimenSurvey {definition.Entry.ObjectId} resolved to no locations");
        await EnsureScientistScanbotAsync(cancellationToken);

        var sampled = new List<string>();
        for (int index = 0; index < definition.Locations.Count; index++)
        {
            uint bit = 1u << index;
            WorldLocation2Entry location = definition.Locations[index];
            if ((GetPathMissionStatus(definition.Id)?.Progress & bit) != 0u)
            {
                sampled.Add($"location={location.Id}:already-set");
                continue;
            }

            float range = Math.Max(location.Radius, 30f);
            Spawn[] candidates = spawns
                .Where(candidate => candidate.WorldId == location.WorldId
                    && candidate.DistanceSquared(location.Position0, location.Position1, location.Position2) <= range * range)
                .OrderBy(candidate => candidate.DistanceSquared(location.Position0, location.Position1, location.Position2))
                .ThenBy(candidate => candidate.Id)
                .Take(SpecimenSpawnAttempts)
                .ToArray();
            if (candidates.Length == 0)
                throw new BotFailureException($"specimen location {location.Id} has no non-player creature spawn within {range:0.###} m");

            // one sample can fail (the unit wandered out of the scan range, a scan cooldown): the next nearest creature is tried
            bool progressed = false;
            var tried = new List<string>();
            foreach (Spawn spawn in candidates)
            {
                TrackedEntity? entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
                if (entity is null)
                {
                    tried.Add($"{spawn.CreatureId}@spawn {spawn.Id}: not visible");
                    continue;
                }
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                    PacketCodec.ClientEntitySelect(entity.Guid), cancellationToken);
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientPathScientistScan,
                    PacketCodec.ClientPathScientistScan(), cancellationToken);
                progressed = await WaitForPathMissionAsync(
                    definition.Id,
                    status => (status.Progress & bit) != 0u || status.Completed,
                    TimeSpan.FromSeconds(10),
                    cancellationToken);
                log.Info($"path mission {definition.Id}: specimen location {location.Id} scan of creature {spawn.CreatureId} guid {entity.Guid}: {(progressed ? "sampled" : "no bit")}");
                if (progressed)
                {
                    sampled.Add($"location={location.Id}:creature={spawn.CreatureId}/spawn={spawn.Id}/range={range:0.###}");
                    break;
                }
                tried.Add($"{spawn.CreatureId}@spawn {spawn.Id} guid {entity.Guid}: no bit");
            }
            if (!progressed)
                throw new BotFailureException($"no scan produced specimen bit 0x{bit:X} for location {location.Id}: [{string.Join("; ", tried)}]; sampled=[{string.Join(", ", sampled)}]");
        }

        PathMissionStatus? status = GetPathMissionStatus(definition.Id);
        return $"locations sampled {sampled.Count}/{definition.Locations.Count}: {string.Join(", ", sampled)}; progress=0x{status?.Progress ?? 0u:X}; episode={definition.Episode.Id}";
    }

    private async Task<string> RunScientistDatacubeMissionAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.ObjectiveCreatureIds.Count == 0)
            throw new BotFailureException($"no DatacubeRules mapping was resolved for scientist mission {definition.Id}");

        var opened = new List<string>();
        for (int index = 0; index < definition.ObjectiveCreatureIds.Count; index++)
        {
            uint bit = 1u << index;
            uint creatureId = definition.ObjectiveCreatureIds[index];
            if ((GetPathMissionStatus(definition.Id)?.Progress & bit) != 0u)
            {
                opened.Add($"{creatureId}:already-set");
                continue;
            }

            Spawn spawn = ChooseNearestSpawn([creatureId], checked((ushort)definition.Episode.WorldId));
            TrackedEntity entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken)
                ?? throw new BotFailureException($"datacube creature {creatureId} at spawn {spawn.Id} was not visible");
            ActivationOutcome outcome = await RunPathActivationFlowAsync(
                entity,
                () => (GetPathMissionStatus(definition.Id)?.Progress & bit) != 0u || IsPathMissionComplete(definition.Id),
                TimeSpan.FromSeconds(8),
                cancellationToken);
            if (outcome != ActivationOutcome.Progressed)
                throw new BotFailureException($"datacube creature {creatureId} guid {entity.Guid} produced no progress bit 0x{bit:X}; opened=[{string.Join(',', opened)}]");
            opened.Add($"{creatureId}/guid={entity.Guid}");
        }

        PathMissionStatus? status = GetPathMissionStatus(definition.Id);
        return $"datacubes opened {opened.Count}/{definition.ObjectiveCreatureIds.Count}: [{string.Join(", ", opened)}]; progress=0x{status?.Progress ?? 0u:X}; episode={definition.Episode.Id}";
    }

    private async Task<string> RunSettlerSheriffMissionAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.QuestIds.Count == 0)
            throw new BotFailureException($"PathSettlerSheriff {definition.Entry.ObjectId} resolved to no quests");

        var done = new List<string>();
        for (int index = 0; index < definition.QuestIds.Count; index++)
        {
            ushort questId = definition.QuestIds[index];
            uint bit = 1u << index;
            if (completedQuests.Contains(questId))
            {
                bool credited = await WaitForPathMissionAsync(
                    definition.Id,
                    status => (status.Progress & bit) != 0u || status.Completed,
                    TimeSpan.FromSeconds(3),
                    cancellationToken);
                if (!credited)
                    throw new BotFailureException($"sheriff quest {questId} is already completed but the server did not set mission bit 0x{bit:X}; quests done=[{string.Join(", ", done)}]");
                done.Add($"{questId}:already-completed");
                continue;
            }

            QuestDefinition questDefinition = questDefinitions.GetValueOrDefault(questId)
                ?? RequireGameData().GetQuest(questId);
            try
            {
                await RunQuestAsync(questDefinition, cancellationToken);
            }
            finally
            {
                finishedQuests.Add(questId);
                if (currentQuestReport is not null)
                    currentQuestReport.EndTimestamp = DateTimeOffset.UtcNow;
                currentQuestReport = null;
            }
            if (!completedQuests.Contains(questId))
                throw new BotFailureException($"sheriff quest {questId} did not reach Completed; quests done=[{string.Join(", ", done)}]");
            done.Add($"{questId}:completed");

            _ = await WaitForPathMissionAsync(
                definition.Id,
                status => (status.Progress & bit) != 0u || status.Completed,
                TimeSpan.FromSeconds(3),
                cancellationToken);
        }

        PathMissionStatus? status = GetPathMissionStatus(definition.Id);
        return $"quests done {done.Count}/{definition.QuestIds.Count}: [{string.Join(", ", done)}]; progress=0x{status?.Progress ?? 0u:X}; episode={definition.Episode.Id}";
    }

    private async Task<string> RunSettlerExpansionMissionAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        PathMissionRunReport missionReport = currentMissionReport
            ?? throw new InvalidOperationException("Settler mission report is unavailable.");
        SettlerHubDefinition hub = definition.SettlerHub
            ?? throw new BotFailureException($"PathSettlerHub {definition.Entry.ObjectId} was not resolved");
        if (hub.Builds.Count == 0)
            throw new BotFailureException($"hub {hub.HubId} has no usable PathSettlerImprovementGroup tiers");
        // the quest phase leaves the bags full of loot; a gathered resource with no bag slot is lost (ServerItemError ItemInventoryFull)
        var keepItems = hub.Resources.Select(resource => resource.ItemId).ToHashSet();
        int freedBefore = await FreeLootedJunkAsync(keepItems, $"settler mission {definition.Id}", cancellationToken);
        if (freedBefore > 0)
            missionReport.Detail = (missionReport.Detail ?? "") + $"freed {freedBefore} looted item(s) ";

        foreach (SettlerBuildDefinition build in hub.Builds)
        {
            if (IsPathMissionComplete(definition.Id))
                break;

            foreach ((uint itemId, uint required) in build.Costs)
            {
                SettlerResourceDefinition resource = hub.Resources.FirstOrDefault(candidate => candidate.ItemId == itemId)
                    ?? throw new BotFailureException($"hub {hub.HubId} build group {build.GroupId} requires unknown resource item {itemId}");
                while (GetInventoryItemCount(itemId) < required)
                {
                    Spawn[] candidates = SpawnsFor(resource.CreatureIds, checked((ushort)definition.Episode.WorldId))
                        .Where(spawn => !usedSettlerResourceSpawnIds.Contains(spawn.Id))
                        .OrderBy(spawn => spawn.DistanceSquared(
                            build.DisplayLocation.Position0,
                            build.DisplayLocation.Position1,
                            build.DisplayLocation.Position2))
                        .ToArray();
                    if (candidates.Length == 0)
                    {
                        // every node has been gathered once; they come back on the respawn timer, so wait at the nearest one
                        Spawn[] all = SpawnsFor(resource.CreatureIds, checked((ushort)definition.Episode.WorldId))
                            .OrderBy(spawn => spawn.DistanceSquared(
                                build.DisplayLocation.Position0,
                                build.DisplayLocation.Position1,
                                build.DisplayLocation.Position2))
                            .ToArray();
                        if (all.Length == 0)
                            throw new BotFailureException($"resource {resource.Name} item {itemId} needs {required}, inventory has {GetInventoryItemCount(itemId)}, and no spawn of creatures [{string.Join(',', resource.CreatureIds)}] exists in world {definition.Episode.WorldId}; gathered=[{FormatResources(missionReport)}]");
                        log.Info($"settler resource {resource.Name}: all {all.Length} nodes gathered once; waiting up to {SettlerNodeRespawnWait.TotalSeconds:0} s for respawns");
                        foreach (Spawn node in all)
                            usedSettlerResourceSpawnIds.Remove(node.Id);
                        missionReport.Detail = (missionReport.Detail ?? "") + $"respawn-wait({resource.Name}) ";
                        TrackedEntity? respawned = await TeleportAndTryFindAsync(all[0], SettlerNodeRespawnWait, cancellationToken);
                        if (respawned is null)
                            throw new BotFailureException($"resource {resource.Name} item {itemId} needs {required}, inventory has {GetInventoryItemCount(itemId)}; all {all.Length} nodes gathered and none respawned within {SettlerNodeRespawnWait.TotalSeconds:0} s at spawn {all[0].Id}; gathered=[{FormatResources(missionReport)}]");
                        continue;
                    }

                    Spawn? spawn = null;
                    TrackedEntity? entity = null;
                    foreach (Spawn candidate in candidates)
                    {
                        usedSettlerResourceSpawnIds.Add(candidate.Id);
                        entity = await TeleportAndTryFindWithRetryAsync(candidate, cancellationToken);
                        if (entity is not null)
                        {
                            spawn = candidate;
                            break;
                        }
                    }
                    if (spawn is null || entity is null)
                        throw new BotFailureException($"resource {resource.Name} item {itemId} needs {required}, inventory has {GetInventoryItemCount(itemId)}, and none of {candidates.Length} remaining spawn(s) was visible; gathered=[{FormatResources(missionReport)}]");
                    uint before = GetInventoryItemCount(itemId);
                    lastItemError = null;

                    // Resource nodes are activated directly by the retail client. If the creature has a cast activation,
                    // complete that normal interaction too so OnActivateSuccess can award the item.
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnit,
                        PacketCodec.ClientActivateUnit(entity.Guid), cancellationToken);
                    bool gathered = await WaitForInventoryCountAsync(itemId, before, TimeSpan.FromSeconds(2), cancellationToken);
                    if (!gathered)
                    {
                        _ = await RunPathActivationFlowAsync(
                            entity,
                            () => GetInventoryItemCount(itemId) > before,
                            TimeSpan.FromSeconds(8),
                            cancellationToken);
                        gathered = GetInventoryItemCount(itemId) > before;
                    }
                    if (!gathered && lastItemError == GenericError.ItemInventoryFull)
                    {
                        int freed = await FreeLootedJunkAsync(keepItems, $"settler mission {definition.Id} (bags full at node {entity.Guid})", cancellationToken);
                        log.Info($"settler resource {resource.Name}: node {entity.Guid} gathered into full bags; freed {freed} looted item(s), moving to the next node");
                        missionReport.Detail = (missionReport.Detail ?? "") + $"bags-full@{entity.Guid}(freed {freed}) ";
                        if (freed > 0)
                            continue;
                    }
                    if (!gathered)
                        throw new BotFailureException($"activation of resource creature {spawn.CreatureId} guid {entity.Guid} did not add item {itemId}{(lastItemError is null ? "" : $" (server item error {lastItemError})")}");

                    uint gained = GetInventoryItemCount(itemId) - before;
                    missionReport.ResourcesGathered[itemId] = missionReport.ResourcesGathered.GetValueOrDefault(itemId) + gained;
                }
            }

            Spawn depotSpawn = SpawnsFor([build.DepotCreatureId], checked((ushort)definition.Episode.WorldId))
                .OrderBy(spawn => spawn.DistanceSquared(
                    build.DisplayLocation.Position0,
                    build.DisplayLocation.Position1,
                    build.DisplayLocation.Position2))
                .FirstOrDefault()
                ?? throw new BotFailureException($"no depot spawn exists for group {build.GroupId}, creature {build.DepotCreatureId}");
            TrackedEntity depot = await TeleportAndTryFindWithRetryAsync(depotSpawn, cancellationToken)
                ?? throw new BotFailureException($"depot creature {build.DepotCreatureId} for group {build.GroupId} was never visible");
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                PacketCodec.ClientEntitySelect(depot.Guid), cancellationToken);

            uint beforeProgress = GetPathMissionStatus(definition.Id)?.Progress ?? 0u;
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientPathSettlerImprovementBuildTier,
                PacketCodec.ClientPathSettlerImprovementBuildTier(build.GroupId, build.Tier), cancellationToken);
            bool built = await WaitForPathMissionAsync(
                definition.Id,
                status => status.Progress > beforeProgress || status.Completed,
                TimeSpan.FromSeconds(10),
                cancellationToken);
            if (!built)
                throw new BotFailureException($"build group {build.GroupId} tier {build.Tier} at depot {depot.Guid} produced no mission progress in 10 s; builds=[{string.Join(',', missionReport.Builds)}]; resources=[{FormatResources(missionReport)}]");
            missionReport.Builds.Add($"group={build.GroupId}/tier={build.Tier}/contribution={build.Contribution}");
        }

        if (!IsPathMissionComplete(definition.Id))
            throw new BotFailureException($"hub {hub.HubId} exhausted {hub.Builds.Count} build tiers without completing {definition.Count}; progress={GetPathMissionStatus(definition.Id)?.Progress.ToString() ?? "not-received"}");
        return $"hub={hub.HubId}; resources gathered=[{FormatResources(missionReport)}]; builds done={missionReport.Builds.Count} [{string.Join(", ", missionReport.Builds)}]; progress={GetPathMissionStatus(definition.Id)!.Progress}/{definition.Count}; episode={definition.Episode.Id}";
    }

    private async Task<string> RunSettlerInfrastructureMissionAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        SettlerInfrastructureDefinition infrastructure = definition.Infrastructure
            ?? throw new BotFailureException($"PathSettlerInfrastructure {definition.Entry.ObjectId} was not resolved");
        if (infrastructure.ResourceCreatureIds.Count == 0)
            throw new BotFailureException($"infrastructure {infrastructure.Id} resolved no resource creatures from its Creature2IdResource fields or objective text");
        if (infrastructure.DepotCreatureId == 0u)
            throw new BotFailureException($"infrastructure {infrastructure.Id} has no Creature2IdDepot");

        uint gatherActivations = 0u;
        uint gathers = 0u;
        uint deliveries = GetPathMissionStatus(definition.Id)?.Progress ?? 0u;
        var attemptedResourceSpawns = new HashSet<uint>();
        while (!IsPathMissionComplete(definition.Id))
        {
            uint progressBefore = GetPathMissionStatus(definition.Id)?.Progress ?? 0u;
            uint remaining = infrastructure.MissionCount > progressBefore
                ? infrastructure.MissionCount - progressBefore
                : 1u;
            uint batchTarget = Math.Min(infrastructure.MaxStackCount, remaining);
            uint batchGathered = 0u;
            while (batchGathered < batchTarget)
            {
                Spawn[] all = SpawnsFor(infrastructure.ResourceCreatureIds, checked((ushort)definition.Episode.WorldId))
                    .OrderBy(spawn => spawn.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
                    .ThenBy(spawn => spawn.Id)
                    .ToArray();
                if (all.Length == 0)
                    throw new BotFailureException($"infrastructure {infrastructure.Id} has no resource spawn for creatures [{string.Join(',', infrastructure.ResourceCreatureIds)}]; gather activations={gatherActivations}, confirmed gathers={gathers}, deliveries={deliveries}");
                Spawn[] candidates = all.Where(spawn => !attemptedResourceSpawns.Contains(spawn.Id)).ToArray();
                if (candidates.Length == 0)
                {
                    attemptedResourceSpawns.Clear();
                    candidates = all;
                }

                bool gathered = false;
                foreach (Spawn spawn in candidates)
                {
                    attemptedResourceSpawns.Add(spawn.Id);
                    TrackedEntity? resource = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
                    if (resource is null)
                        continue;
                    ActivationOutcome outcome = await RunPathActivationFlowAsync(
                        resource,
                        () => false, // gathering changes only server-side carried state; delivery emits the mission update
                        TimeSpan.FromMilliseconds(500),
                        cancellationToken,
                        acceptConfirmedActivationWithoutProgress: true);
                    if (outcome != ActivationOutcome.Activated)
                        continue;
                    batchGathered++;
                    gatherActivations++;
                    gathered = true;
                    break;
                }
                if (!gathered)
                {
                    attemptedResourceSpawns.Clear();
                    log.Info($"infrastructure {infrastructure.Id}: no remaining resource spawn was visible; waiting up to {SettlerNodeRespawnWait.TotalSeconds:0} s at spawn {all[0].Id}");
                    TrackedEntity? respawned = await TeleportAndTryFindAsync(all[0], SettlerNodeRespawnWait, cancellationToken);
                    if (respawned is null)
                        throw new BotFailureException($"infrastructure {infrastructure.Id} could not activate another resource and none respawned within {SettlerNodeRespawnWait.TotalSeconds:0} s; batch={batchGathered}/{batchTarget}, gather activations={gatherActivations}, confirmed gathers={gathers}, deliveries={deliveries}");
                }
            }

            Spawn depotSpawn = ChooseNearestSpawn([infrastructure.DepotCreatureId], checked((ushort)definition.Episode.WorldId));
            _ = await TeleportAndTryFindWithRetryAsync(depotSpawn, cancellationToken)
                ?? throw new BotFailureException($"infrastructure depot creature {infrastructure.DepotCreatureId} at spawn {depotSpawn.Id} was not visible; gather activations={gatherActivations}, confirmed gathers={gathers}, deliveries={deliveries}");
            bool delivered = await WaitForPathMissionAsync(
                definition.Id,
                status => status.Progress > progressBefore || status.Completed,
                TimeSpan.FromSeconds(5),
                cancellationToken);
            if (!delivered)
                throw new BotFailureException($"infrastructure delivery produced no progress within 5 s after {batchGathered} gather activation(s); gather activations={gatherActivations}, confirmed gathers={gathers}, deliveries={deliveries}");
            uint progressAfter = GetPathMissionStatus(definition.Id)?.Progress ?? progressBefore;
            uint deliveredNow = progressAfter - progressBefore;
            gathers += deliveredNow;
            deliveries += deliveredNow;
        }

        PathMissionStatus? status = GetPathMissionStatus(definition.Id);
        return $"gathers this run={gathers} confirmed ({gatherActivations} activation(s)); deliveries total={deliveries}/{infrastructure.MissionCount}; max carried={infrastructure.MaxStackCount}; resource creatures=[{string.Join(',', infrastructure.ResourceCreatureIds)}]; depot={infrastructure.DepotCreatureId}; progress={status?.Progress.ToString() ?? "not-received"}; episode={definition.Episode.Id}";
    }

    private async Task<string> RunScientistExperimentationMissionAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.ExperimentationConsoleCreatureIds.Count == 0)
            throw new BotFailureException($"no Creature2 row has PathScientistExperimentationId {definition.Entry.ObjectId}");

        TrackedEntity console = await FindPathCreatureAsync(
            definition.ExperimentationConsoleCreatureIds,
            definition,
            cancellationToken);
        latestExperimentationInvoke = null;
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
            PacketCodec.ClientEntitySelect(console.Guid), cancellationToken);
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
            PacketCodec.ClientEntityInteract(console.Guid, InteractionType.ScientistActivate), cancellationToken);

        DateTimeOffset invokeDeadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (latestExperimentationInvoke?.UnitId != console.Guid)
        {
            TimeSpan remaining = invokeDeadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                throw new BotFailureException($"experiment console {console.CreatureId} guid {console.Guid} produced no ServerPathScientistExperimentationInvoke in 10 s");
        }

        uint[] code = latestExperimentationInvoke.CurrentPattern.ToArray();
        await RequireWorld().SendEncryptedAsync(
            GameMessageOpcode.ClientPathMissionAttemptScientistExperimentation,
            PacketCodec.ClientPathMissionAttemptScientistExperimentation(code),
            cancellationToken);
        bool completed = await WaitForPathMissionAsync(
            definition.Id,
            status => status.Completed,
            TimeSpan.FromSeconds(10),
            cancellationToken);
        if (!completed)
            throw new BotFailureException($"experimentation attempt [{string.Join(',', code)}] did not complete mission {definition.Id}");
        return $"code=[{string.Join(',', code)}]; attempt=[{string.Join(',', code)}]; console creature={console.CreatureId}/guid={console.Guid}; episode={definition.Episode.Id}";
    }

    private async Task<bool> WaitForPathMissionAsync(
        ushort missionId,
        Func<PathMissionStatus, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            if (GetPathMissionStatus(missionId) is PathMissionStatus status && predicate(status))
                return true;
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return false;
        }
    }

    private async Task<bool> WaitForInventoryCountAsync(
        uint itemId,
        uint before,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (GetInventoryItemCount(itemId) <= before)
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return false;
        }
        return true;
    }

    private static string FormatResources(PathMissionRunReport missionReport)
        => string.Join(',', missionReport.ResourcesGathered.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));

    private async Task UnlockPathMissionIfNeededAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (pathMissionStates.ContainsKey(definition.Id))
            return;
        if (definition.Entry.Creature2IdUnlock == 0u)
            throw new BotFailureException("mission was not present in the server episode state and has no unlock creature");

        TrackedEntity entity = await FindPathCreatureAsync(
            [definition.Entry.Creature2IdUnlock],
            definition,
            cancellationToken);
        _ = await RunPathActivationFlowAsync(
            entity,
            () => pathMissionStates.ContainsKey(definition.Id),
            TimeSpan.FromSeconds(8),
            cancellationToken);
        if (!pathMissionStates.ContainsKey(definition.Id))
            throw new BotFailureException($"activation of unlock creature {definition.Entry.Creature2IdUnlock} did not unlock mission {definition.Id}");
    }

    private async Task RunPathKillMissionAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.ObjectiveCreatureIds.Count == 0)
            throw new BotFailureException($"{definition.TypeName} ObjectId {definition.Entry.ObjectId} resolved to no creature or target-group members");
        IReadOnlyList<Spawn> candidates = SpawnsFor(definition.ObjectiveCreatureIds, questWorldId);
        if (candidates.Count == 0)
            throw new BotFailureException($"No spawn in world {questWorldId} for path targets {string.Join(',', definition.ObjectiveCreatureIds)}.");

        IReadOnlySet<uint> creatureIds = definition.ObjectiveCreatureIds.ToHashSet();
        var finishedGuids = new HashSet<uint>();
        var spawnVisits = new Dictionary<uint, int>();
        while (!IsPathMissionComplete(definition.Id))
        {
            TrackedEntity? target = await TryFindVisibleCombatTargetAsync(
                creatureIds, finishedGuids, CombatLocalTargetSearchTimeout, cancellationToken);
            if (target is null)
            {
                Spawn? spawn = ChooseNextCombatSpawn(candidates, spawnVisits);
                if (spawn is null)
                {
                    spawnVisits.Clear();
                    continue;
                }
                spawnVisits[spawn.Id] = spawnVisits.GetValueOrDefault(spawn.Id) + 1;
                target = await TeleportAndTryFindCombatTargetAsync(
                    spawn, creatureIds, finishedGuids, CombatSpawnTargetSearchTimeout, cancellationToken);
                if (target is null)
                    continue;
            }

            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                PacketCodec.ClientEntitySelect(target.Guid), cancellationToken);
            if (await FightUntilDeathAsync(target.Guid, cancellationToken))
                finishedGuids.Add(target.Guid);
        }
    }

    private async Task RunPathActivateMissionAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.ObjectiveCreatureIds.Count == 0)
            throw new BotFailureException($"{definition.TypeName} ObjectId {definition.Entry.ObjectId} resolved to no creature or target-group members");
        IReadOnlyList<Spawn> candidates = SpawnsFor(definition.ObjectiveCreatureIds, questWorldId);
        if (candidates.Count == 0)
            throw new BotFailureException($"No spawn in world {questWorldId} for activation targets {string.Join(',', definition.ObjectiveCreatureIds)}.");

        var attemptedSpawns = new HashSet<uint>();
        int rounds = 0;
        log.Info($"path mission {definition.Id}: activation target creature(s) [{string.Join(',', definition.ObjectiveCreatureIds)}], {candidates.Count} spawn(s), count {definition.Count}");
        while (!IsPathMissionComplete(definition.Id))
        {
            if (attemptedSpawns.Count >= candidates.Count)
            {
                // every spawn visited: a unit shown only while the mission runs (Rockridge Recovery's Dead Explorers) may appear
                // after the server starts the mission at its indicator; three rounds, not a six minute loop on one spawn
                if (++rounds >= ActivateMissionSpawnRounds)
                    throw new BotFailureException($"activation targets [{string.Join(',', definition.ObjectiveCreatureIds)}] never visible at their {candidates.Count} spawn(s) in {rounds} rounds; progress={GetPathMissionStatus(definition.Id)?.Progress ?? 0u}");
                attemptedSpawns.Clear();
            }
            Spawn spawn = ChooseNearestSpawn(definition.ObjectiveCreatureIds, questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity? entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
            if (entity is null)
                log.Info($"path mission {definition.Id}: spawn {spawn.Id} (creature {spawn.CreatureId}) not visible");
            if (entity is null)
                continue;
            uint before = GetPathMissionStatus(definition.Id)?.Progress ?? 0u;
            ActivationOutcome outcome = await RunPathActivationFlowAsync(
                entity,
                () => IsPathMissionComplete(definition.Id)
                    || (GetPathMissionStatus(definition.Id)?.Progress ?? 0u) > before,
                TimeSpan.FromSeconds(8),
                cancellationToken);
            if (outcome == ActivationOutcome.NoProgress && attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException($"activation targets for mission {definition.Id} produced no further progress");
        }
    }

    private async Task RunHoldoutMissionAsync(
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.HoldoutConsoleCreatureIds.Count == 0)
            throw new BotFailureException($"no Creature2 row names mission {definition.Id} as PathMissionIdSoldier");
        TrackedEntity console = await FindPathCreatureAsync(
            definition.HoldoutConsoleCreatureIds,
            definition,
            cancellationToken);
        holdoutProgressPackets.Clear();
        currentHoldoutWave = null;
        activeHoldoutEventId = checked((ushort)definition.Entry.ObjectId);
        holdoutPreparationDetail = await PrepareForHoldoutAsync(cancellationToken);
        IReadOnlySet<uint> existingNonPlayers = entities.GetVisibleNonPlayerGuids();
        holdoutResult = null;
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnit,
            PacketCodec.ClientActivateUnit(console.Guid), cancellationToken);
        log.Info($"path mission {definition.Id}: activated holdout console creature {console.CreatureId} guid {console.Guid}");

        var ignoredGuids = existingNonPlayers.ToHashSet();
        ignoredGuids.Add(console.Guid);
        while (holdoutResult is null)
        {
            TrackedEntity? target = await FindNextHoldoutTargetAsync(ignoredGuids, cancellationToken);
            if (target is null)
                continue;

            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                PacketCodec.ClientEntitySelect(target.Guid), cancellationToken);
            if (!await FightUntilDeathAsync(target.Guid, cancellationToken, lootCorpse: false))
                ignoredGuids.Add(target.Guid);
        }

        if (holdoutResult != PlayerPathSoldierResult.Success)
            throw new BotFailureException($"holdout ended with {holdoutResult}");

        DateTimeOffset completionDeadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!IsPathMissionComplete(definition.Id))
        {
            TimeSpan remaining = completionDeadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero
                || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                break;
        }
    }

    private async Task<TrackedEntity?> FindNextHoldoutTargetAsync(
        IReadOnlySet<uint> ignoredGuids,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (true)
        {
            if (holdoutResult is not null)
                return null;
            TrackedEntity? target = entities.FindNearestVisibleNonPlayer(
                lastPosition,
                HoldoutTargetRadius,
                ignoredGuids,
                RequireGameData().IsHoldoutHostile);
            if (target is not null)
            {
                holdoutNoTargetSince = null;
                return target;
            }

            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero
                || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
            {
                LogHoldoutNoTarget(ignoredGuids);
                return null;
            }
        }
    }

    /// <summary>
    /// After 10 s without a holdout target, say what is around so a wave creature standing out of reach (stuck, far spawn,
    /// not hostile by our rule) can be told apart from a wave that is simply over.
    /// </summary>
    private void LogHoldoutNoTarget(IReadOnlySet<uint> ignoredGuids)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        holdoutNoTargetSince ??= now;
        if (now - holdoutNoTargetSince.Value < TimeSpan.FromSeconds(10)
            || now - holdoutLastNoTargetLog < TimeSpan.FromSeconds(10))
            return;

        holdoutLastNoTargetLog = now;
        string nearby = string.Join(", ", entities
            .GetNearbyCreatureIds(lastPosition, 150f, 10)
            .Select(entry => $"{entry.CreatureId}@{entry.Distance:0}m{(RequireGameData().IsHoldoutHostile(entry.CreatureId) ? "" : " (not hostile)")}"));
        log.Info($"holdout: no target within {HoldoutTargetRadius:0} m for {(now - holdoutNoTargetSince.Value).TotalSeconds:0} s "
            + $"(wave {FormatCurrentHoldoutWave()}, health {currentHealth?.ToString() ?? "?"}/{currentMaxHealth?.ToString() ?? "?"}, "
            + $"{ignoredGuids.Count} guid(s) ignored); creatures within 150 m: [{nearby}]");
    }

    private async Task<string> PrepareForHoldoutAsync(CancellationToken cancellationToken)
    {
        uint levelBefore = currentLevel;
        uint? maxHealthBefore = currentMaxHealth;
        bool raisedLevel = currentLevel < 15u;
        if (raisedLevel)
        {
            await SendCheatAsync("!character level 15", cancellationToken);
            currentLevel = 15u;
            await DrainWorldPacketsAsync(TimeSpan.FromSeconds(1), cancellationToken);
        }
        await EquipGearForLevelAsync(cancellationToken);

        Stopwatch healthWait = Stopwatch.StartNew();
        // out of combat the server heals 1 % of max health every 0.5 s (HealthStatUpdater); the gear of the level nearly triples max health
        // without refilling it, so a freshly dressed bot at 40 % needs about half a minute to fill up
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(150);
        while (currentHealth is not uint health
            || currentMaxHealth is not uint maxHealth
            || maxHealth == 0u
            || health < maxHealth
            || botDied
            || raisedLevel && currentMaxHealth == maxHealthBefore)
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero
                || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
            {
                throw new BotFailureException(
                    $"holdout preparation did not confirm alive at full health within 150 s (dead={botDied}; health={currentHealth?.ToString() ?? "unknown"}/{currentMaxHealth?.ToString() ?? "unknown"})");
            }
        }

        return $"level {levelBefore}->{currentLevel}; health {currentHealth}/{currentMaxHealth} full after {healthWait.Elapsed.TotalSeconds:0.0}s";
    }

    private string FormatCurrentHoldoutWave()
        => currentHoldoutWave?.ToString() ?? "unknown";

    private string FormatHoldoutMissionDetail()
    {
        string preparation = holdoutPreparationDetail is null
            ? "not-recorded"
            : holdoutPreparationDetail;
        string progress = holdoutProgressPackets.Count == 0
            ? "none"
            : string.Join(" | ", holdoutProgressPackets);
        return $"; preparation={preparation}; wave progress packets=[{progress}]";
    }

    private async Task<TrackedEntity> FindPathCreatureAsync(
        IReadOnlyList<uint> creatureIds,
        PathMissionDefinition definition,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Spawn> candidates = SpawnsFor(creatureIds, checked((ushort)definition.Episode.WorldId));
        if (candidates.Count == 0)
            throw new BotFailureException($"No world spawn was found for creature(s) {string.Join(',', creatureIds)}.");
        Spawn spawn = candidates
            .OrderBy(candidate => candidate.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
            .First();
        return await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken)
            ?? throw new BotFailureException($"creature(s) {string.Join(',', creatureIds)} were not visible; {FormatNearbyTrackedCreatures()}");
    }

    private async Task<ActivationOutcome> RunPathActivationFlowAsync(
        TrackedEntity entity,
        Func<bool> progressed,
        TimeSpan progressTimeout,
        CancellationToken cancellationToken,
        bool acceptConfirmedActivationWithoutProgress = false)
    {
        for (int retry = 0; retry <= 3; retry++)
        {
            uint clientUniqueId = spellCastId++;
            pendingActivationCastIds.Add(clientUniqueId);
            try
            {
                bool activationConfirmed = false;
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnitCast,
                    PacketCodec.ClientActivateUnitCast(clientUniqueId, entity.Guid), cancellationToken);
                ActivationCastResponse response = await TryWaitForActivationCastResponseAsync(
                    clientUniqueId, entity.Guid, TimeSpan.FromSeconds(5), cancellationToken);
                if (response.Result is SpellCastResult castResult)
                {
                    (bool shouldRetry, ActivationOutcome? outcome) = await HandleActivationCastResultAsync(
                        castResult, retry, cancellationToken);
                    if (shouldRetry)
                        continue;
                    return outcome!.Value;
                }
                if (response.Start is SpellCastStart spellStart)
                {
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientSpellClientSideInteraction,
                        PacketCodec.ClientSpellClientSideInteraction(spellStart.ServerUniqueId), cancellationToken);
                    respondedCsiSpellIds.Add(spellStart.ServerUniqueId);
                    activationConfirmed = await TryWaitForSpellFinishAsync(
                        spellStart.ServerUniqueId,
                        TimeSpan.FromSeconds(5),
                        cancellationToken);
                    if (activationCastResults.Remove(clientUniqueId, out SpellCastResult? finishResult)
                        && finishResult is not null)
                    {
                        (bool shouldRetry, ActivationOutcome? outcome) = await HandleActivationCastResultAsync(
                            finishResult, retry, cancellationToken);
                        if (shouldRetry)
                            continue;
                        return outcome!.Value;
                    }
                }
                else
                {
                    // the unit can be gone by now (a corpse looted away, a used node): the plain activation of a vanished guid was
                    // an invalid packet to the server and dropped the session (Explorer leg, 2026-11-02)
                    if (entities.Get(entity.Guid) is null)
                    {
                        log.Info($"activation target guid {entity.Guid} (creature {entity.CreatureId}) vanished before the plain activation; not usable");
                        return ActivationOutcome.TargetNotUsable;
                    }
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnit,
                        PacketCodec.ClientActivateUnit(entity.Guid), cancellationToken);
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
                        PacketCodec.ClientEntityInteract(entity.Guid, InteractionType.Interact), cancellationToken);
                    // a unit without an activation spell answers nothing a client can see; when the caller tracks the effect itself
                    // (an infrastructure crate's carried resource) the plain use is the activation
                    if (acceptConfirmedActivationWithoutProgress)
                    {
                        await DrainWorldPacketsAsync(TimeSpan.FromMilliseconds(300), cancellationToken);
                        return ActivationOutcome.Activated;
                    }
                }

                DateTimeOffset deadline = DateTimeOffset.UtcNow + progressTimeout;
                bool advanced = progressed();
                while (!advanced)
                {
                    TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
                    if (remaining <= TimeSpan.Zero
                        || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                        break;
                    advanced = progressed();
                }
                if (advanced)
                    return ActivationOutcome.Progressed;
                if (activationCastResults.Remove(clientUniqueId, out SpellCastResult? progressResult)
                    && progressResult is not null)
                {
                    (bool shouldRetry, ActivationOutcome? outcome) = await HandleActivationCastResultAsync(
                        progressResult, retry, cancellationToken);
                    if (shouldRetry)
                        continue;
                    return outcome!.Value;
                }
                if (activationConfirmed && acceptConfirmedActivationWithoutProgress)
                    return ActivationOutcome.Activated;
                return ActivationOutcome.NoProgress;
            }
            finally
            {
                pendingActivationCastIds.Remove(clientUniqueId);
                activationCastResults.Remove(clientUniqueId);
            }
        }
        return ActivationOutcome.NoProgress;
    }

    private PathMissionStatus? GetPathMissionStatus(ushort missionId)
        => pathMissionStates.GetValueOrDefault(missionId);

    private bool IsPathMissionComplete(ushort missionId)
        => GetPathMissionStatus(missionId)?.Completed == true;

    private async Task TryAbandonFailedQuestAsync(
        QuestDefinition definition,
        QuestRunReport questReport,
        CancellationToken cancellationToken)
    {
        if (questState is not (QuestState.Accepted or QuestState.Achieved))
            return;

        bool cannotAbandon = (definition.Entry.Flags & 0x4000u) != 0u
            || questState == QuestState.Achieved && (definition.Entry.Flags & 0x8000u) != 0u;
        if (cannotAbandon)
        {
            questReport.Steps.Add(new StepReport
            {
                Name = "abandon failed quest",
                Ok = true,
                ElapsedMs = 0,
                Detail = "quest flags prohibit abandonment; continuing with the quest still active"
            });
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestAbandon,
                PacketCodec.ClientQuestAbandon(definition.Id), cancellationToken);
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (questState is not (QuestState.Abandoned or QuestState.Mentioned))
            {
                TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                    throw new BotFailureException("server did not confirm abandonment within 5 seconds");
            }
            questReport.Steps.Add(new StepReport
            {
                Name = "abandon failed quest",
                Ok = true,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Detail = $"server reported state {questState}"
            });
        }
        catch (Exception exception)
        {
            questReport.Steps.Add(new StepReport
            {
                Name = "abandon failed quest",
                Ok = false,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Detail = exception.Message
            });
            log.Info($"quest {definition.Id} abandon cleanup failed: {exception.Message}");
        }
    }

    private async Task TryRecoverFromDeathAsync(QuestRunReport questReport, CancellationToken cancellationToken)
    {
        if (!botDied)
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();
        resurrectionAcceptSent = false;
        resurrectionPacketsAfterAccept.Clear();
        resurrectionPacketOverflow = 0;
        using var recoveryBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        recoveryBudget.CancelAfter(TimeSpan.FromSeconds(60));
        CancellationToken recoveryToken = recoveryBudget.Token;
        try
        {
            while (resurrectionShow is null)
            {
                if (await TryReadWorldPacketAsync(TimeSpan.FromSeconds(5), recoveryToken) is null)
                    continue;
            }

            ResurrectionShow offer = resurrectionShow;
            ResurrectionType type = (offer.Flags & ResurrectionType.Holocrypt) != 0
                ? ResurrectionType.Holocrypt
                : (offer.Flags & ResurrectionType.WakeHere) != 0
                    ? ResurrectionType.WakeHere
                    : (offer.Flags & ResurrectionType.ExitInstance) != 0
                        ? ResurrectionType.ExitInstance
                        : ResurrectionType.None;
            if (type == ResurrectionType.None)
                throw new BotFailureException("server offered no usable resurrection type");

            uint elapsedSinceOfferMs = (uint)Math.Min(uint.MaxValue,
                Math.Max(0d, (DateTimeOffset.UtcNow - resurrectionShowReceivedAt).TotalMilliseconds));
            uint waitUntilMs = type switch
            {
                // Holocrypt is immediately available when advertised by the server.
                ResurrectionType.Holocrypt => 0u,
                // WakeHere has its own cooldown and ResurrectSelf also observes the general rez timer.
                ResurrectionType.WakeHere => Math.Max(offer.TimeUntilWakeHereMs, offer.TimeUntilRezMs),
                _ => offer.TimeUntilRezMs
            };
            uint remainingWaitMs = waitUntilMs > elapsedSinceOfferMs ? waitUntilMs - elapsedSinceOfferMs : 0u;
            if (remainingWaitMs > 0u)
                await Task.Delay(TimeSpan.FromMilliseconds(remainingWaitMs + 200u), recoveryToken);

            resurrectionRecoveryActive = true;
            resurrectionConfirmation = null;
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientResurrectAccept,
                PacketCodec.ClientResurrectAccept(offer.GhostId, type), recoveryToken);
            resurrectionAcceptSent = true;
            while (botDied && resurrectionConfirmation is null)
            {
                if (await TryReadWorldPacketAsync(TimeSpan.FromSeconds(5), recoveryToken) is null)
                    continue;
            }

            await CompleteResurrectionRelocationAsync(type, recoveryToken);
            botDied = false;

            questReport.Steps.Add(new StepReport
            {
                Name = "recover from death",
                Ok = true,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Detail = $"resurrected with {type}; waited={remainingWaitMs + (remainingWaitMs > 0u ? 200u : 0u)}ms; confirmed by {resurrectionConfirmation ?? "DeathState false"}; {FormatResurrectionOffer(offer)}; {FormatResurrectionPacketTrail()}"
            });
        }
        catch (Exception exception)
        {
            string reason = exception is OperationCanceledException && recoveryBudget.IsCancellationRequested
                ? "death recovery exceeded 60 s"
                : exception.Message;
            questReport.Steps.Add(new StepReport
            {
                Name = "recover from death",
                Ok = false,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Detail = $"{reason}; {FormatResurrectionOffer(resurrectionShow)}; {FormatResurrectionPacketTrail()}"
            });
            log.Info($"death recovery failed: {reason}; {FormatResurrectionOffer(resurrectionShow)}; {FormatResurrectionPacketTrail()}");
        }
        finally
        {
            resurrectionRecoveryActive = false;
            resurrectionAcceptSent = false;
        }
    }

    private static string FormatResurrectionOffer(ResurrectionShow? offer)
        => offer is null
            ? "ShowRezFlags=<not received>; TimeUntilRezMs=<unknown>; TimeUntilWakeHereMs=<unknown>; TimeUntilForceRezMs=<unknown>"
            : $"ShowRezFlags={offer.Flags}(0x{(byte)offer.Flags:X2}); TimeUntilRezMs={offer.TimeUntilRezMs}; TimeUntilWakeHereMs={offer.TimeUntilWakeHereMs}; TimeUntilForceRezMs={offer.TimeUntilForceRezMs}; Dead={offer.Dead}; HasCasterRezRequest={offer.HasCasterRezRequest}; RezCost={offer.RezCost}";

    private void RecordResurrectionPacketAfterAccept(GameMessageOpcode opcode)
    {
        if (!resurrectionRecoveryActive || !resurrectionAcceptSent)
            return;
        if (resurrectionPacketsAfterAccept.Count < 64)
            resurrectionPacketsAfterAccept.Add(opcode.ToString());
        else
            resurrectionPacketOverflow++;
    }

    private string FormatResurrectionPacketTrail()
    {
        if (!resurrectionAcceptSent && resurrectionPacketsAfterAccept.Count == 0)
            return "packets after ClientResurrectAccept=<accept not sent>";
        string packets = resurrectionPacketsAfterAccept.Count == 0
            ? "none"
            : string.Join(" -> ", resurrectionPacketsAfterAccept);
        string overflow = resurrectionPacketOverflow == 0
            ? ""
            : $" -> ...(+{resurrectionPacketOverflow})";
        return $"packets after ClientResurrectAccept=[{packets}{overflow}]";
    }

    private async Task CompleteResurrectionRelocationAsync(
        ResurrectionType type,
        CancellationToken cancellationToken)
    {
        if (worldLoadPending)
        {
            await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerPlayerEnteredWorld, cancellationToken);
            return;
        }
        if (type != ResurrectionType.Holocrypt)
            return;

        // This fork's holocrypt resurrection uses TeleportToLocal in the same world: health/death
        // state clears first, followed by ServerLoadingScreen and a new movement-control position.
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (true)
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return;
            ProtocolPacket? packet = await TryReadWorldPacketAsync(remaining, cancellationToken);
            if (packet is null)
                return;
            if (worldLoadPending)
            {
                await ReadWorldUntilAsync(candidate => candidate.Opcode == GameMessageOpcode.ServerPlayerEnteredWorld, cancellationToken);
                return;
            }
            if (packet.Opcode == GameMessageOpcode.ServerLoadingScreen)
            {
                await ReadWorldUntilAsync(candidate => candidate.Opcode == GameMessageOpcode.ServerMovementControl, cancellationToken);
                resurrectionConfirmation += "+local holocrypt relocation";
                return;
            }
        }
    }

    private ushort? GetMissingPrerequisite(QuestDefinition definition)
    {
        ushort[] prerequisites = definition.Entry.PrerequisiteQuests
            .Where(value => value != 0u)
            .Select(value => checked((ushort)value))
            .ToArray();
        if (prerequisites.Length == 0)
            return RequireGameData().MissingQuestStatePrerequisite(definition.Entry, completedQuests.Contains);
        if ((definition.Entry.PrerequisiteFlags & 1u) != 0u)
            return prerequisites.Any(completedQuests.Contains)
                ? null
                : RequireGameData().GetPrerequisiteBranchChoice(definition.Id)?.BranchQuestId ?? prerequisites[0];
        return prerequisites.FirstOrDefault(id => !completedQuests.Contains(id)) is ushort missing && missing != 0
            ? missing
            : RequireGameData().MissingQuestStatePrerequisite(definition.Entry, completedQuests.Contains);
    }

    private void AddBlockedQuestReport(QuestDefinition definition, ushort prerequisiteId)
    {
        QuestRunReport questReport = CreateQuestReport(definition);
        // a prerequisite this character never receives (instance or other zone content, reported SKIPPED) blocks the chain
        // behind it the same way: Gallow's Strife waits for an instance quest in world 930
        QuestRunReport? skippedPrerequisite = report.Quests.FirstOrDefault(previous => previous.QuestId == prerequisiteId && previous.Result == "SKIPPED");
        string failure = skippedPrerequisite is null
            ? $"blocked by prerequisite {prerequisiteId}"
            : $"blocked by prerequisite {prerequisiteId}, content this character never receives ({skippedPrerequisite.Failure}); this quest never follows";
        questReport.Result = skippedPrerequisite is null ? "FAIL" : "SKIPPED";
        questReport.Skipped = skippedPrerequisite is not null;
        questReport.Failure = failure;
        questReport.EndTimestamp = DateTimeOffset.UtcNow;
        questReport.Steps.Add(new StepReport
        {
            Name = "accept quest",
            Ok = false,
            ElapsedMs = 0,
            Detail = failure
        });
        report.Quests.Add(questReport);
    }

    private void AddSkippedQuestReport(QuestDefinition definition)
    {
        if (!reportedSkippedQuests.Add(definition.Id))
            return;

        var questReport = CreateQuestReport(definition);
        questReport.Skipped = true;
        questReport.Result = "SKIPPED";
        questReport.EndTimestamp = DateTimeOffset.UtcNow;
        foreach (ObjectiveReport objective in questReport.Objectives)
        {
            objective.Progress = objective.Count;
            objective.Ok = true;
        }
        questReport.Steps.Add(new StepReport
        {
            Name = "skip completed quest",
            Ok = true,
            ElapsedMs = 0,
            Detail = "ServerQuestInit/ServerQuestStateChange reported state 3 (Completed)"
        });
        report.Quests.Add(questReport);
    }

    /// <summary>
    /// A quest whose area objective lies in another world (the class training instances, expeditions) is not zone content;
    /// the zone run reports it skipped instead of timing out at the instance portal.
    /// </summary>
    private string? InstanceSkipReason(QuestDefinition definition)
    {
        WorldLocation2Entry? receiver = definition.Entry.WorldLocation2IdReceiver == 0u
            ? null
            : RequireGameData().TryGetWorldLocation(definition.Entry.WorldLocation2IdReceiver);
        ushort? homeWorld = receiver is null ? null : checked((ushort)receiver.WorldId);
        foreach (QuestObjectiveDefinition objective in definition.Objectives)
        {
            // an EnterZone objective naming a zone outside the run zone's family is an instance or another zone (the class
            // training instances: Support Training enters zone 4819 of world 3406 through a portal in Algoroc)
            if (objective.Type == QuestObjectiveType.EnterZone && objective.Data != 0u && options.ZoneId is uint runZone
                && !RequireGameData().ZoneFamily(runZone).Contains(objective.Data) && !RequireGameData().ZoneFamily(objective.Data).Contains(runZone))
                return $"enters zone {objective.Data} outside zone {runZone} (objective {objective.Index}); instance or other zone content";
            if (objective.Type is not (QuestObjectiveType.EnterZone or QuestObjectiveType.EnterArea) || objective.WorldLocationId == 0u)
                continue;
            WorldLocation2Entry? location = RequireGameData().TryGetWorldLocation(objective.WorldLocationId);
            if (location is null)
                continue;
            ushort world = checked((ushort)location.WorldId);
            if (homeWorld is ushort home && world != home)
                return $"instance content (objective {objective.Index} enters world {world}, the quest lives in world {home})";
        }
        return null;
    }

    private void AddNotForCharacterQuestReport(QuestDefinition definition, string reason)
    {
        var questReport = CreateQuestReport(definition);
        questReport.Skipped = true;
        questReport.Result = "SKIPPED";
        questReport.Failure = $"SKIPPED: {reason}; this character never receives it";
        questReport.EndTimestamp = DateTimeOffset.UtcNow;
        questReport.Steps.Add(new StepReport
        {
            Name = "skip quest not for this character",
            Ok = true,
            ElapsedMs = 0,
            Detail = questReport.Failure
        });
        report.Quests.Add(questReport);
        log.Info($"quest {definition.Id} {questReport.Failure}");
    }

    private void AddItemStartedQuestReport(QuestDefinition definition, string reason)
    {
        var questReport = CreateQuestReport(definition);
        questReport.Skipped = true;
        questReport.Result = "SKIPPED";
        questReport.Failure = reason;
        questReport.EndTimestamp = DateTimeOffset.UtcNow;
        questReport.Steps.Add(new StepReport
        {
            Name = "skip item-started quest",
            Ok = true,
            ElapsedMs = 0,
            Detail = questReport.Failure
        });
        report.Quests.Add(questReport);
    }

    private async Task RunQuestAsync(QuestDefinition definition, CancellationToken cancellationToken)
    {
        currentQuestReport = CreateQuestReport(definition);
        report.Quests.Add(currentQuestReport);
        QuestRunReport questReport = currentQuestReport;
        try
        {
            // a death at the very end of the previous quest can leave the bot dead here (3487 Shellshock -> 3670 Calm Before
            // the Storm "bot remains dead"): give the resurrection one more 60 s budget before the quest is written off
            if (botDied)
                await TryRecoverFromDeathAsync(questReport, cancellationToken);
            ConfigureQuest(definition);
            await LevelForAcceptanceIfNeededAsync(definition, cancellationToken);
            if (definition.Entry.PrerequisiteItem != 0u)
                await LevelForQuestIfNeededAsync(definition, cancellationToken);
            TimeSpan acceptTimeout = StartItem(definition) == 0u ? DefaultStepTimeout : ItemDropAcceptTimeout;
            if (StartItem(definition) == 0u && questState != QuestState.Mentioned)
            {
                int giverSpawnCount = SpawnsFor(definition.GiverCreatureIds, questWorldId).Count;
                int receiverSpawnCount = RequireGameData().GetCommunicatorOffers(definition.Id).Count > 0
                    ? spawns.Count(spawn => definition.ReceiverCreatureIds.Contains(spawn.CreatureId))
                    : 0;
                acceptTimeout = AllowEveryQuestNpcSpawnAttempt(giverSpawnCount + receiverSpawnCount, acceptTimeout);
            }
            if (RequireGameData().RequiredSubZone(definition.Entry) is uint requiredZone)
            {
                // the server checks the InSubZone prerequisite at accept (the communicator offers these on zone entry)
                await RunQuestStepAsync("enter prerequisite zone", TimeSpan.FromSeconds(60), cancellationToken, async token =>
                {
                    Spawn? giverSpawn = SpawnsFor(definition.GiverCreatureIds, questWorldId).FirstOrDefault();
                    Vector3? near = giverSpawn is null ? null : new Vector3(giverSpawn.X, giverSpawn.Y, giverSpawn.Z);
                    WorldLocation2Entry location = RequireGameData().LocationInZone(requiredZone, questWorldId, near)
                        ?? throw new BotFailureException($"no WorldLocation2 lies in zone {requiredZone} of world {questWorldId}");
                    await TeleportToLocationAsync(location, 0f, token);
                    await DrainWorldPacketsAsync(TimeSpan.FromSeconds(1), token);
                    string where = $"zone {requiredZone} via WorldLocation2 {location.Id} ({location.Position0:0},{location.Position1:0},{location.Position2:0})";
                    // the quest's own prerequisite is the zone: accept here, before walking off to the giver (A Hidden Threat's
                    // communicator call fires in The Hidden Grotto and the accept from its giver outside was refused)
                    if (questState != QuestState.Mentioned)
                        await TryWaitForQuestStateAsync(QuestState.Mentioned, TimeSpan.FromSeconds(8), token);
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestAccept,
                        PacketCodec.ClientQuestAccept(definition.Id), token);
                    bool acceptedHere = await TryWaitForQuestStateAsync(QuestState.Accepted, TimeSpan.FromSeconds(5), token);
                    return where + (acceptedHere ? "; accepted in the zone" : $"; accept in the zone not confirmed (state {questState}), trying the giver");
                });
            }
            if (questState == QuestState.Accepted)
            {
                questReport.Steps.Add(new StepReport { Name = "accept quest", Ok = true, ElapsedMs = 0, Detail = "accepted in the prerequisite zone" });
            }
            else
                await RunQuestStepAsync("accept quest", acceptTimeout, cancellationToken, AcceptQuestAsync);
            await LevelForQuestIfNeededAsync(definition, cancellationToken);

            foreach (QuestObjectiveDefinition objective in definition.Objectives)
            {
                // optional objectives (flag 0x20: "Purchase ... from the In-Game Store" on Lots in Store!) are completed by the
                // server once the required ones are done; the bot does not attempt them
                if ((objective.Flags & 0x20u) != 0u && !IsObjectiveComplete(objective))
                {
                    log.Info($"quest {definition.Id} objective {objective.Index}: optional (flags 0x{objective.Flags:X}), skipped");
                    questReport.Steps.Add(new StepReport
                    {
                        Name = $"objective {objective.Index}",
                        Ok = true,
                        ElapsedMs = 0,
                        Detail = $"optional objective (flags 0x{objective.Flags:X}) left to the server"
                    });
                    continue;
                }
                TimeSpan timeout = ObjectiveTimeout(objective);
                await RunQuestStepAsync($"objective {objective.Index}", timeout, cancellationToken,
                    token => RunObjectiveWithRetriesAsync(objective, definition, token));
            }

            await RunQuestStepAsync("quest achieved", DefaultStepTimeout, cancellationToken, WaitForAchievedAsync);
            TimeSpan turnInTimeout = AllowEveryQuestNpcSpawnAttempt(
                spawns.Count(spawn => definition.ReceiverCreatureIds.Contains(spawn.CreatureId)),
                DefaultStepTimeout);
            await RunQuestStepAsync("turn in quest", turnInTimeout, cancellationToken, TurnInQuestAsync);
            questReport.Result = "PASS";
            questReport.Failure = null;
        }
        catch (Exception exception)
        {
            questReport.Result = "FAIL";
            questReport.Failure = exception.Message;
            if (!questReport.Steps.Any(step => !step.Ok))
            {
                questReport.Steps.Add(new StepReport
                {
                    Name = "prepare quest",
                    Ok = false,
                    ElapsedMs = 0,
                    Detail = exception.Message
                });
            }
            throw;
        }
        finally
        {
            questReport.EndTimestamp = DateTimeOffset.UtcNow;
            activeObjective = null;
        }
    }

    private TimeSpan AllowEveryObjectiveSpawnAttempt(
        QuestObjectiveDefinition objective,
        TimeSpan baseline,
        ushort? worldId = null)
    {
        int spawnCount = SpawnsFor(GetObjectiveCreatureIds(objective), worldId ?? questWorldId).Count;
        double everySpawnSeconds = spawnCount * EntityStreamTimeout.TotalSeconds * 2d + 15d;
        return TimeSpan.FromSeconds(Math.Max(baseline.TotalSeconds, everySpawnSeconds));
    }

    /// <summary>
    /// The budget of one objective step: by type and count, never under the time to try every spawn of its creatures.
    /// </summary>
    private TimeSpan ObjectiveTimeout(QuestObjectiveDefinition objective, ushort? worldId = null)
    {
        TimeSpan timeout = objective.Type is QuestObjectiveType.KillCreature
            or QuestObjectiveType.KillCreature2
            or QuestObjectiveType.KillTargetGroup
            or QuestObjectiveType.KillTargetGroups
            or QuestObjectiveType.GatheResource
            // seven Augmented Gronyx (Grafted Gronyx, difficulty 4) took about 40 s each with the teleport between them and
            // overran the flat 300 s on the seventh: 50 s per kill, never under the combat budget. The per-kill budget grows
            // with the toughest target's spawn health: the Augmented Slateform Slammer (3870) and Demolisher (4828) took
            // 45-58 s each at level 17, so seven of them overran 380 s on 2026-11-04 (six fails in seven legs)
            ? TimeSpan.FromSeconds(Math.Max(CombatTimeout.TotalSeconds, Math.Max(1u, objective.Count) * KillSeconds(objective, worldId) + 30d))
            // a virtual item held by hostile creatures (Rockridge Rascals: Arifo the Rascal, a named with 1.75x stats) is a fight
            : objective.Type == QuestObjectiveType.VirtualCollect && VirtualCollectIsFight(objective)
                ? TimeSpan.FromSeconds(Math.Max((uint)CombatTimeout.TotalSeconds, Math.Max(1u, objective.Count) * 55u))
            : objective.Type == QuestObjectiveType.VirtualCollect
                ? TimeSpan.FromSeconds(Math.Max(1u, objective.Count) * 55u)
            : objective.Type == QuestObjectiveType.SucceedCSI
                ? TimeSpan.FromSeconds(60)
            // a quest ability cast per target (Use Augmented Remains on eight constructs: a 3 s cast and a fight each)
            : objective.Type == QuestObjectiveType.SpellSuccess
                ? TimeSpan.FromSeconds(Math.Max(120u, Math.Max(1u, objective.Count) * 50u))
            // items collected by using units or looted from kills (By Leaps and Bounds: eight loftite fragments that drop
            // from 44 % of boulderback kills, so eighteen fights of about 25 s on average and the odd health recovery;
            // 390 s failed three legs of four): 75 s per item plus a respawn wait
            : objective.Type == QuestObjectiveType.CollectItem
                ? TimeSpan.FromSeconds(Math.Max(75u, Math.Max(1u, objective.Count) * 75u + 30u))
                : DefaultStepTimeout;
        if (objective.Type is QuestObjectiveType.ActivateEntity
            or QuestObjectiveType.ActivateTargetGroup
            or QuestObjectiveType.ActivateTargetGroupChecklist
            or QuestObjectiveType.Unknown10
            or QuestObjectiveType.Unknown28
            or QuestObjectiveType.VirtualCollect
            or QuestObjectiveType.GatheResource
            or QuestObjectiveType.SucceedCSI)
        {
            timeout = AllowEveryObjectiveSpawnAttempt(objective, timeout, worldId);
            // a used unit comes back on the respawn timer; the activation flow waits for it twice (35 s each)
            if (objective.Type == QuestObjectiveType.ActivateEntity && timeout < TimeSpan.FromSeconds(150))
                timeout = TimeSpan.FromSeconds(150);
        }
        if (IsSingleTargetKillRetryObjective(objective))
            timeout = SingleTargetKillRetryTimeout;
        return timeout;
    }

    /// <summary>
    /// Seconds budgeted per kill of an objective: 50 for an ordinary creature, more for a tough one (10 s plus the
    /// toughest spawn's health over 60, the bot's observed damage per second at level 17 with a three-slot action set:
    /// 3870 health = 75 s, 4828 = 90 s).
    /// </summary>
    private double KillSeconds(QuestObjectiveDefinition objective, ushort? worldId)
    {
        uint health = SpawnsFor(GetObjectiveCreatureIds(objective), worldId ?? questWorldId)
            .Select(spawn => spawn.Health)
            .DefaultIfEmpty(0u)
            .Max();
        return Math.Max(50d, 10d + health / 60d);
    }

    /// <summary>
    /// The per-quest budget is a safety net over the step timeouts: the accept wait plus every required objective's budget
    /// plus the achieved and turn-in steps, never under the flat 8 minutes. The flat budget alone cut Grafted Gronyx (seven
    /// kills of 50 s and then a second kill objective) and By Leaps and Bounds (eight 44 % drops at 75 s each) short.
    /// </summary>
    private TimeSpan QuestBudget(QuestDefinition definition)
    {
        ushort worldId = DetermineQuestWorld(definition);
        double seconds = (StartItem(definition) == 0u ? DefaultStepTimeout : ItemDropAcceptTimeout).TotalSeconds
            + definition.Objectives.Where(objective => (objective.Flags & 0x20u) == 0u)
                .Sum(objective => ObjectiveTimeout(objective, worldId).TotalSeconds)
            + DefaultStepTimeout.TotalSeconds * 2d + 60d;
        return TimeSpan.FromSeconds(Math.Max(PerQuestTimeout.TotalSeconds, seconds));
    }

    private static TimeSpan AllowEveryQuestNpcSpawnAttempt(int spawnCount, TimeSpan baseline)
    {
        double everySpawnSeconds = spawnCount
            * (EntityStreamTimeout.TotalSeconds * 2d + QuestNpcConfirmationTimeout.TotalSeconds)
            + 5d;
        return TimeSpan.FromSeconds(Math.Max(baseline.TotalSeconds, everySpawnSeconds));
    }

    private void ConfigureQuest(QuestDefinition definition)
    {
        quest = definition;
        questState = questStates.GetValueOrDefault(definition.Id, QuestState.Unknown);
        pendingActivationCastIds.Clear();
        activationCastResults.Clear();
        finishedSpellIds.Clear();
        objectiveCastResultDetails.Clear();
        objectiveProgress.Clear();
        foreach (QuestObjectiveDefinition objective in definition.Objectives)
        {
            if (allObjectiveProgress.TryGetValue((definition.Id, (uint)objective.Index), out uint progress))
            {
                objectiveProgress[(uint)objective.Index] = progress;
                UpdateObjectiveReport((uint)objective.Index, progress);
            }
        }
        if (botDied)
            throw new BotFailureException("bot remains dead after the previous quest");
        resurrectionShow = null;

        questWorldId = DetermineQuestWorld(definition);
        LogCombatSlotDiscovery();
    }

    private ushort DetermineQuestWorld(QuestDefinition definition)
    {
        if (StartItem(definition) is uint startItem && startItem != 0u)
        {
            ItemDropSources sources = itemDropSources.GetValueOrDefault(startItem)
                ?? throw new BotFailureException($"item {startItem} has no drop source in the world database");
            Spawn[] sourceSpawns = spawns.Where(spawn => sources.CreatureIds.Contains(spawn.CreatureId)).ToArray();
            if (sourceSpawns.Length == 0)
                throw new BotFailureException($"No world spawn was found for item {startItem} drop source(s) {string.Join(',', sources.CreatureIds)}.");
            if (sourceSpawns.Any(spawn => spawn.WorldId == currentWorldId))
                return currentWorldId;

            HashSet<ushort> receiverWorlds = spawns
                .Where(spawn => definition.ReceiverCreatureIds.Contains(spawn.CreatureId))
                .Select(spawn => spawn.WorldId)
                .ToHashSet();
            return sourceSpawns
                .OrderBy(spawn => receiverWorlds.Contains(spawn.WorldId) ? 0 : 1)
                .ThenBy(spawn => spawn.Id)
                .Select(spawn => spawn.WorldId)
                .First();
        }

        if (questState == QuestState.Mentioned)
            return DetermineObjectiveWorld(definition) ?? currentWorldId;

        Spawn[] giverSpawns = spawns.Where(spawn => definition.GiverCreatureIds.Contains(spawn.CreatureId)).ToArray();
        Spawn[] receiverSpawns = spawns.Where(spawn => definition.ReceiverCreatureIds.Contains(spawn.CreatureId)).ToArray();
        if (giverSpawns.Length == 0)
        {
            IReadOnlyList<CommunicatorOffer> offers = RequireGameData().GetCommunicatorOffers(definition.Id);
            if (offers.Count == 0)
                throw new BotFailureException("no giver spawn and no communicator offer");
            if (receiverSpawns.Length == 0)
                throw new BotFailureException($"Quest {definition.Id} has a communicator offer but no receiver spawn to use as a world anchor.");

            CommunicatorOffer? offer = offers.FirstOrDefault(candidate => candidate.WorldId == 0u
                || receiverSpawns.Any(spawn => spawn.WorldId == candidate.WorldId));
            if (offer is null)
                throw new BotFailureException($"Quest {definition.Id} communicator worlds do not contain a known receiver spawn.");
            return offer.WorldId == 0u
                ? receiverSpawns.Select(spawn => spawn.WorldId).First()
                : checked((ushort)offer.WorldId);
        }
        if (receiverSpawns.Length == 0)
            throw new BotFailureException($"No world spawn was found for a quest {definition.Id} receiver.");

        ushort[] sharedWorlds = giverSpawns.Select(spawn => spawn.WorldId)
            .Intersect(receiverSpawns.Select(spawn => spawn.WorldId))
            .Distinct()
            .ToArray();
        if (sharedWorlds.Length == 0)
        {
            // giver and receiver live in different worlds (Bon Voyage: Dorian Walker on the arkship, Scout Frostfield on Nexus):
            // run the quest in the giver's world, the turn in follows the receiver spawns into theirs
            ushort giverWorld = giverSpawns.Select(spawn => spawn.WorldId).Contains(currentWorldId)
                ? currentWorldId
                : giverSpawns.Select(spawn => spawn.WorldId).First();
            log.Info($"quest {definition.Id}: giver and receiver share no world; running in the giver's world {giverWorld}");
            return giverWorld;
        }

        if (sharedWorlds.Contains(currentWorldId))
            return currentWorldId;

        return sharedWorlds
            .OrderBy(worldId => MinimumGiverReceiverDistance(giverSpawns, receiverSpawns, worldId))
            .First();
    }

    private ushort? DetermineObjectiveWorld(QuestDefinition definition)
    {
        var objectiveWorlds = new List<ushort>();
        foreach (QuestObjectiveDefinition objective in definition.Objectives)
        {
            if (objective.WorldLocationId != 0u)
            {
                WorldLocation2Entry location = RequireGameData().GetWorldLocation(objective.WorldLocationId);
                objectiveWorlds.Add(checked((ushort)location.WorldId));
            }

            HashSet<uint> creatureIds = GetObjectiveCreatureIds(objective).ToHashSet();
            objectiveWorlds.AddRange(spawns
                .Where(spawn => creatureIds.Contains(spawn.CreatureId))
                .Select(spawn => spawn.WorldId));
        }

        if (objectiveWorlds.Contains(currentWorldId))
            return currentWorldId;

        return objectiveWorlds
            .GroupBy(worldId => worldId)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (ushort?)group.Key)
            .FirstOrDefault();
    }

    // power level of the gear the bot equipped per slot (starter gear counts as 0)
    private readonly Dictionary<EquippedItem, uint> equippedGearPowerLevel = [];
    private ulong? lastMovedItemGuid;
    // the item a ServerItemSwap sent back to the bag in exchange (null after a plain ServerItemMove)
    private ulong? lastSwappedOutItemGuid;

    /// <summary>
    /// The level cheat gives the character a level's health but not a level's gear: it fought at level 18 with the level 1
    /// starter sword (raw 160 per hit against 1,860 health creatures). A player of that level wears gear of about that power
    /// level, so after a level raise the bot takes a plain green weapon of its level (!item add, the item cheat it already
    /// relies on for levels) and equips it with the client's own item move.
    /// </summary>
    /// <summary>
    /// Dresses the bot like a player of its level: a plain green greatsword, heavy armor set and shield of about the level's
    /// power level (a naked level 18 bot had 4200 health and died to two level 12 creatures; the same gear a player gets from
    /// quests and vendors adds armor, health and a shield pool). Each piece replaced goes the way a player vendors it: deleted.
    /// </summary>
    private async Task EquipGearForLevelAsync(CancellationToken cancellationToken)
    {
        if (currentLevel < 2u)
            return;
        var equippedNow = new List<string>();
        foreach (GearSlot slot in BotGameData.WarriorGear)
        {
            equippedGearPowerLevel.TryGetValue(slot.Slot, out uint wornPowerLevel);
            if (wornPowerLevel + 2u >= currentLevel)
                continue;
            Item2Entry? piece = RequireGameData().ChooseGearForLevel(slot, currentLevel);
            if (piece is null || piece.PowerLevel <= wornPowerLevel)
                continue;
            if (await EquipGearPieceAsync(slot, piece, cancellationToken))
                equippedNow.Add($"{slot.Kind} {RequireGameData().ItemName(piece.Id)} ({piece.Id}, power level {piece.PowerLevel})");
            equippedGearPowerLevel[slot.Slot] = piece.PowerLevel;
        }
        if (equippedNow.Count > 0)
            log.Info($"gear for level {currentLevel}: {string.Join("; ", equippedNow)}");
    }

    private async Task<bool> EquipGearPieceAsync(GearSlot slot, Item2Entry piece, CancellationToken cancellationToken)
    {
        string name = RequireGameData().ItemName(piece.Id);
        await SendCheatAsync($"!item add {piece.Id}", cancellationToken);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        InventoryItemInfo? added = null;
        while (added is null && DateTimeOffset.UtcNow < deadline)
        {
            added = inventory.Values.FirstOrDefault(item => item.ItemId == piece.Id && item.Location == InventoryLocation.Inventory);
            if (added is null && await TryReadWorldPacketAsync(deadline - DateTimeOffset.UtcNow, cancellationToken) is null)
                break;
        }
        if (added is null)
        {
            log.Info($"{slot.Kind} for level {currentLevel}: {name} ({piece.Id}, power level {piece.PowerLevel}) did not arrive in the bags within 3 s");
            return false;
        }
        lastMovedItemGuid = null;
        lastSwappedOutItemGuid = null;
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientItemMove,
            PacketCodec.ClientItemMove(added.Location, added.BagIndex, InventoryLocation.Equipped, (uint)slot.Slot), cancellationToken);
        // the server answers with ServerItemSwap (the worn piece comes back to the bag slot the new one left) or ServerItemMove
        DateTimeOffset moveDeadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (lastMovedItemGuid != added.Guid && DateTimeOffset.UtcNow < moveDeadline
            && await TryReadWorldPacketAsync(moveDeadline - DateTimeOffset.UtcNow, cancellationToken) is not null)
        {
        }
        bool equipped = lastMovedItemGuid == added.Guid;
        if (!equipped)
        {
            log.Info($"{slot.Kind} for level {currentLevel}: {name} ({piece.Id}, power level {piece.PowerLevel}) added; equip not confirmed");
            return false;
        }
        inventory[added.Guid] = added with { Location = InventoryLocation.Equipped, BagIndex = (uint)slot.Slot };
        if (lastSwappedOutItemGuid is ulong oldGuid && inventory.TryGetValue(oldGuid, out InventoryItemInfo? old)
            && old.Location == InventoryLocation.Equipped && old.BagIndex == (uint)slot.Slot)
        {
            // the replaced piece is junk to a player (vendored): delete it from the bag slot it swapped into so bags stay free
            inventory[oldGuid] = old with { Location = InventoryLocation.Inventory, BagIndex = added.BagIndex };
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientItemDelete,
                PacketCodec.ClientItemDelete(InventoryLocation.Inventory, added.BagIndex), cancellationToken);
            await DrainWorldPacketsAsync(TimeSpan.FromMilliseconds(300), cancellationToken);
        }
        return true;
    }

    private async Task LevelForQuestIfNeededAsync(QuestDefinition definition, CancellationToken cancellationToken)
    {
        // camps with many kills (Shellshock: nine Dominion soldiers who aggro together) killed the bot at con level + 5,
        // 3335 damage taken over 31 enemy casts; a bigger margin for objectives asking five or more kills
        // a start item farmed from a drop source is a named creature fight (A Heart of Stone: Stonefist, a difficulty 5 named
        // with 1.75x stats, killed the bot at con level + 5 twice), so it gets the camp margin as well
        bool camp = StartItem(definition) != 0u || definition.Objectives.Any(objective => objective.Type is QuestObjectiveType.KillCreature
            or QuestObjectiveType.KillCreature2 or QuestObjectiveType.KillTargetGroup or QuestObjectiveType.KillTargetGroups
            && objective.Count >= 5u);
        // every quest of a zone run fights at con level + 5 (camps + 9): Algoroc's activation quests walk into Dominion camps
        // (Weapons of War's DREDplex) and a con-level bot died there before touching a grenade target
        // zone runs always take the camp margin: Weapons of War's grenade targets stand inside a Dominion camp and a con + 5
        // bot died there before using one (the zone cap below keeps it at 15 at most)
        uint targetLevel = definition.Entry.ConLevel + (options.ZoneId is not null ? 9u : RequiresLevelBoost(definition) ? (camp ? 9u : 5u) : 1u);
        if (options.ZoneId is not null)
            targetLevel = Math.Min(15u, targetLevel);

        if (targetLevel <= currentLevel)
        {
            await EquipGearForLevelAsync(cancellationToken);
            return;
        }

        await RunQuestStepAsync("set quest level", TimeSpan.FromSeconds(10), cancellationToken, async token =>
        {
            uint before = currentLevel;
            await SendCheatAsync($"!character level {targetLevel}", token);
            // The integer stat update is consumed when available; retaining the value sent is the
            // documented fallback because this GM command accepts increases only.
            currentLevel = targetLevel;
            await DrainWorldPacketsAsync(TimeSpan.FromSeconds(1), token);
            await EquipGearForLevelAsync(token);
            return $"level {before} -> {currentLevel} for quest con level {definition.Entry.ConLevel}";
        });
    }

    private async Task LevelForAcceptanceIfNeededAsync(QuestDefinition definition, CancellationToken cancellationToken)
    {
        uint targetLevel = Math.Max(1u, definition.Entry.PrerequisiteLevel);
        if (targetLevel <= currentLevel)
            return;

        await RunQuestStepAsync("set prerequisite level", TimeSpan.FromSeconds(10), cancellationToken, async token =>
        {
            uint before = currentLevel;
            await SendCheatAsync($"!character level {targetLevel}", token);
            currentLevel = targetLevel;
            await DrainWorldPacketsAsync(TimeSpan.FromSeconds(1), token);
            return $"level {before} -> {currentLevel} for prerequisite level {definition.Entry.PrerequisiteLevel}";
        });
    }

    private bool RequiresLevelBoost(QuestDefinition definition)
        => StartItem(definition) != 0u // the start item is farmed from a drop source (A Heart of Stone: a named gronyx)
            || definition.Objectives.Any(objective => objective.Type is QuestObjectiveType.KillCreature
            or QuestObjectiveType.KillCreature2
            or QuestObjectiveType.KillTargetGroup
            or QuestObjectiveType.KillTargetGroups
            or QuestObjectiveType.CollectItem
            or QuestObjectiveType.VirtualCollect);

    private static bool HasSingleTargetKillRetry(QuestDefinition definition)
        => definition.Objectives.Any(IsSingleTargetKillRetryObjective);

    private static bool IsSingleTargetKillRetryObjective(QuestObjectiveDefinition objective)
        => objective.Type == QuestObjectiveType.KillCreature && objective.Count == 1u;

    private async Task<string> RunObjectiveWithRetriesAsync(
        QuestObjectiveDefinition objective,
        QuestDefinition definition,
        CancellationToken cancellationToken)
    {
        if (!IsSingleTargetKillRetryObjective(objective))
            return await RunObjectiveWithDeathRetryAsync(objective, definition, cancellationToken);

        using (var firstAttempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            firstAttempt.CancelAfter(CombatTimeout);
            try
            {
                return await RunObjectiveWithDeathRetryAsync(objective, definition, firstAttempt.Token);
            }
            catch (Exception exception) when (firstAttempt.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested)
            {
                log.Info($"quest {definition.Id} objective {objective.Index}: first 300 s single-target KillCreature budget expired ({exception.Message}); preparing one retry");
            }
        }

        await RunQuestStepAsync(
            "raise level for single-target retry",
            TimeSpan.FromSeconds(10),
            cancellationToken,
            async token =>
            {
                uint before = currentLevel;
                if (currentLevel < 15u)
                {
                    await SendCheatAsync("!character level 15", token);
                    currentLevel = 15u;
                    await DrainWorldPacketsAsync(TimeSpan.FromSeconds(1), token);
                }
                return before < 15u
                    ? $"level {before} -> {currentLevel}; zone cap for the one retry"
                    : "already level 15; retaining zone cap for the one retry";
            });
        await RunQuestStepAsync(
            "heal before single-target retry",
            TimeSpan.FromSeconds(40),
            cancellationToken,
            async token =>
            {
                await RequireWorld().SendEncryptedAsync(
                    GameMessageOpcode.ClientEntitySelect,
                    PacketCodec.ClientEntitySelect(0),
                    token);
                await DrainWorldPacketsAsync(TimeSpan.FromSeconds(2), token);
                if (playerInCombat)
                {
                    Spawn? safeAnchor = SpawnsFor(definition.ReceiverCreatureIds, questWorldId)
                        .OrderBy(spawn => spawn.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
                        .ThenBy(spawn => spawn.Id)
                        .FirstOrDefault();
                    if (safeAnchor is not null)
                        await TeleportToSpawnLocationAsync(safeAnchor, token);
                }

                Stopwatch outOfCombatWait = Stopwatch.StartNew();
                DateTimeOffset outOfCombatDeadline = DateTimeOffset.UtcNow.AddSeconds(15);
                while (playerInCombat)
                {
                    TimeSpan remaining = outOfCombatDeadline - DateTimeOffset.UtcNow;
                    if (remaining <= TimeSpan.Zero
                        || await TryReadWorldPacketAsync(remaining, token) is null)
                        throw new BotFailureException("player remained in combat while preparing the single-target retry");
                }
                await DrainWorldPacketsAsync(TimeSpan.FromSeconds(15), token);
                if (botDied)
                {
                    QuestRunReport questReport = currentQuestReport
                        ?? throw new InvalidOperationException("Current quest report is unavailable during death recovery.");
                    await TryRecoverFromDeathAsync(questReport, token);
                    if (botDied)
                        throw new BotFailureException("bot died while healing before the single-target retry");
                }
                return $"confirmed out of combat after {outOfCombatWait.Elapsed.TotalSeconds:0.0}s, then waited 15 s to heal; health={currentHealth?.ToString() ?? "unknown"}/{currentMaxHealth?.ToString() ?? "unknown"}";
            });

        using var retryAttempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        retryAttempt.CancelAfter(CombatTimeout);
        try
        {
            string detail = await RunObjectiveWithDeathRetryAsync(objective, definition, retryAttempt.Token);
            return $"{detail}; succeeded on the one fresh 300 s retry";
        }
        catch (Exception) when (retryAttempt.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            throw new BotFailureException("single-target KillCreature timed out after the initial and retry 300 s budgets");
        }
    }

    private async Task<string> RunObjectiveWithDeathRetryAsync(
        QuestObjectiveDefinition objective,
        QuestDefinition definition,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RunObjectiveAsync(objective, cancellationToken);
        }
        catch (BotFailureException) when (botDied && !cancellationToken.IsCancellationRequested)
        {
            QuestRunReport questReport = currentQuestReport
                ?? throw new InvalidOperationException("Current quest report is unavailable during death recovery.");
            await TryRecoverFromDeathAsync(questReport, cancellationToken);
            if (botDied)
                throw;

            // a zone run already fights at the zone cap (15): the retry after a death comes back three levels stronger, as a
            // player who died to a named or a camp would (Algoroc's eleventh pass: 12 quests lost to a second death at 15)
            uint targetLevel = options.ZoneId is not null
                ? Math.Min(ZoneDeathRetryLevelCap, Math.Max(definition.Entry.ConLevel + 7u, currentLevel + 3u))
                : definition.Entry.ConLevel + 7u;
            if (targetLevel > currentLevel)
            {
                await RunQuestStepAsync("raise level after death", TimeSpan.FromSeconds(10), cancellationToken, async token =>
                {
                    uint before = currentLevel;
                    await SendCheatAsync($"!character level {targetLevel}", token);
                    currentLevel = targetLevel;
                    await DrainWorldPacketsAsync(TimeSpan.FromSeconds(1), token);
                    return $"level {before} -> {currentLevel} after death; retrying objective {objective.Index} once";
                });
            }
            return await RunObjectiveAsync(objective, cancellationToken);
        }
    }

    private QuestRunReport CreateQuestReport(QuestDefinition definition)
    {
        var questReport = new QuestRunReport
        {
            QuestId = definition.Id,
            Title = definition.Title,
            CharacterName = report.CharacterName
        };
        if (RequireGameData().GetPrerequisiteBranchChoice(definition.Id) is { } branch)
        {
            string availability = (branch.HasGiverSpawn, branch.HasCommunicatorOffer) switch
            {
                (true, true) => "giver spawn and communicator offer",
                (true, false) => "giver spawn",
                (false, true) => "communicator offer",
                _ => "no spawned giver or communicator offer"
            };
            questReport.Steps.Add(new StepReport
            {
                Name = "prerequisite branch",
                Ok = true,
                ElapsedMs = 0,
                Detail = $"selected root quest {branch.BranchQuestId} ({branch.BranchTitle}); {availability}; con level {RequireGameData().GetQuest(branch.BranchQuestId).Entry.ConLevel}"
            });
        }
        foreach (QuestObjectiveDefinition objective in definition.Objectives)
        {
            questReport.Objectives.Add(new ObjectiveReport
            {
                Index = objective.Index,
                Type = objective.Type.ToString(),
                Data = objective.Data,
                Count = objective.Count
            });
        }
        return questReport;
    }

    private async Task<string> RunQuestStepAsync(
        string name,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<string>> action)
    {
        int stepCount = report.Steps.Count;
        watchdogStep = name;
        bool questNpcStep = name is "accept quest" or "turn in quest";
        if (questNpcStep)
        {
            questNpcAttemptCreatureIds.Clear();
            questNpcAttemptDetails.Clear();
            itemHuntProgress = null;
        }
        try
        {
            string reportName = questPlan.Count > 1 ? $"quest {RequireQuest().Id}: {name}" : name;
            return await TimedStep.RunAsync(
                report,
                reportName,
                timeout,
                cancellationToken,
                action,
                questNpcStep ? FormatQuestNpcTimeoutDetail : null);
        }
        finally
        {
            if (currentQuestReport is not null && report.Steps.Count > stepCount)
            {
                StepReport topLevel = report.Steps[^1];
                if (questNpcStep
                    && !topLevel.Ok
                    && !topLevel.Detail.Contains("tracked creature ids within 60 m", StringComparison.Ordinal))
                {
                    topLevel = new StepReport
                    {
                        Name = topLevel.Name,
                        Ok = false,
                        ElapsedMs = topLevel.ElapsedMs,
                        Detail = $"{topLevel.Detail}; {FormatQuestNpcTimeoutDetail()}"
                    };
                    report.Steps[^1] = topLevel;
                }
                if (name.StartsWith("objective ", StringComparison.Ordinal)
                    && objectiveCastResultDetails.Count > 0
                    && !topLevel.Detail.Contains("; castResults:", StringComparison.Ordinal))
                {
                    topLevel = new StepReport
                    {
                        Name = topLevel.Name,
                        Ok = topLevel.Ok,
                        ElapsedMs = topLevel.ElapsedMs,
                        Detail = topLevel.Detail + FormatObjectiveCastResults()
                    };
                    report.Steps[^1] = topLevel;
                }
                currentQuestReport.Steps.Add(new StepReport
                {
                    Name = name,
                    Ok = topLevel.Ok,
                    ElapsedMs = topLevel.ElapsedMs,
                    Detail = topLevel.Detail
                });
            }
        }
    }

    private async Task<string> AcceptQuestAsync(CancellationToken cancellationToken)
    {
        QuestDefinition definition = RequireQuest();
        if (StartItem(definition) != 0u)
            return await AcquireItemAndAcceptQuestAsync(definition, cancellationToken);

        if (questState == QuestState.Mentioned)
        {
            await MoveToMentionedQuestWorldAsync(definition, cancellationToken);
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestAccept,
                PacketCodec.ClientQuestAccept(definition.Id), cancellationToken);
            await WaitForQuestStateAsync(QuestState.Accepted, cancellationToken);
            return "accepted (mentioned)";
        }

        QuestNpcUseResult? giver = await TryEveryQuestNpcAsync(
            "giver",
            definition.GiverCreatureIds,
            questWorldId,
            async (entity, token) =>
            {
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
                    PacketCodec.ClientEntityInteract(entity.Guid, InteractionType.TalkTo), token);
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestAccept,
                    PacketCodec.ClientQuestAccept(definition.Id), token);
                return await TryWaitForQuestStateAsync(
                    QuestState.Accepted,
                    QuestNpcConfirmationTimeout,
                    token);
            },
            cancellationToken);
        if (giver is null)
        {
            if (RequireGameData().GetCommunicatorOffers(definition.Id).Count > 0)
                return await AcceptCommunicatorQuestAsync(definition, cancellationToken);
            throw new BotFailureException(
                $"no giver accepted the quest and no communicator offer; {FormatQuestNpcAttempts()}; {FormatNearbyTrackedCreatures()}");
        }
        return $"accepted from creature {giver.Entity.CreatureId} guid {giver.Entity.Guid}; {giver.Attempts}";
    }

    /// <summary>
    /// The item that starts the quest: the one the quest asks for (PrerequisiteItem) or the one whose use activates it.
    /// </summary>
    private static bool HasReputationObjective(QuestDefinition definition)
        => definition.Objectives.Any(objective => objective.Type == QuestObjectiveType.ActivateEntity && objective.Data == 0u && objective.Count == 1000u);

    private uint StartItem(QuestDefinition definition)
        => definition.Entry.PrerequisiteItem != 0u
            ? definition.Entry.PrerequisiteItem
            : RequireGameData().GetQuestActivationItem(definition.Id);

    private async Task<string> AcquireItemAndAcceptQuestAsync(
        QuestDefinition definition,
        CancellationToken cancellationToken)
    {
        uint itemId = StartItem(definition);
        uint activationQuest = RequireGameData().GetItemActivationQuest(itemId);
        if (activationQuest != definition.Id)
            throw new BotFailureException($"item {itemId} activates quest {activationQuest}, not quest {definition.Id}");

        ItemDropSources sources = itemDropSources.GetValueOrDefault(itemId)
            ?? throw new BotFailureException($"item {itemId} has no drop source in the world database");
        if (sources.CreatureIds.Count == 0)
            throw new BotFailureException($"item {itemId} has no drop source in the world database");

        int kills = 0;
        itemHuntProgress = null;
        var killsByCreature = new SortedDictionary<uint, int>();
        int spawnHops = 0;
        InventoryItemInfo? item = FindInventoryItem(itemId);
        if (item is null)
        {
            IReadOnlyList<Spawn> candidates = SpawnsFor(sources.CreatureIds, questWorldId);
            if (candidates.Count == 0)
                throw new BotFailureException($"No spawn in world {questWorldId} for item {itemId} drop source(s) {string.Join(',', sources.CreatureIds)}.");

            IReadOnlySet<uint> creatureIds = sources.CreatureIds.ToHashSet();
            // the best sources first: Carcass Discovery's datapad drops from the Enraged Grimstone Mangler at 20 % and from the
            // cave's other Grimstone creatures at 1.5-7 %, and three runs of 21-33 mixed kills never saw it. The hunt visits the
            // spawns of creatures within half the best rate until each has been visited twice, then everything once, and so on
            double bestRate = creatureIds.Select(sources.RateOf).DefaultIfEmpty(0d).Max();
            IReadOnlySet<uint> preferredIds = bestRate > 0d
                ? creatureIds.Where(id => sources.RateOf(id) >= bestRate / 2d).ToHashSet()
                : creatureIds;
            IReadOnlyList<Spawn> preferredCandidates = SpawnsFor(preferredIds, questWorldId);
            bool preferredPhase = preferredCandidates.Count > 0 && preferredIds.Count < creatureIds.Count;
            if (preferredPhase)
                log.Info($"item {itemId} hunt: preferring creature(s) [{string.Join(',', preferredIds.OrderByDescending(sources.RateOf).Select(id => $"{id}@{sources.RateOf(id):0.#}%"))}] with {preferredCandidates.Count} spawn(s) over [{sources.DescribeCreatures()}]");
            var spawnVisits = new Dictionary<uint, int>();
            var finishedTargetGuids = new HashSet<uint>();
            while ((item = FindInventoryItem(itemId)) is null)
            {
                IReadOnlySet<uint> huntIds = preferredPhase ? preferredIds : creatureIds;
                IReadOnlyList<Spawn> huntCandidates = preferredPhase ? preferredCandidates : candidates;
                TrackedEntity? target = await TryFindVisibleCombatTargetAsync(
                    huntIds, finishedTargetGuids, CombatLocalTargetSearchTimeout, cancellationToken);
                if (target is null)
                {
                    Spawn? spawn = ChooseNextCombatSpawn(huntCandidates, spawnVisits);
                    if (spawn is null)
                    {
                        spawnVisits.Clear();
                        if (preferredCandidates.Count > 0 && preferredIds.Count < creatureIds.Count)
                        {
                            preferredPhase = !preferredPhase;
                            log.Info($"item {itemId} hunt: {(preferredPhase ? "back to the preferred" : "every")} source spawn(s) after {kills} kill(s)");
                        }
                        continue;
                    }
                    spawnVisits[spawn.Id] = spawnVisits.GetValueOrDefault(spawn.Id) + 1;
                    spawnHops++;
                    itemHuntProgress = FormatItemHunt(itemId, sources, kills, killsByCreature, spawnVisits.Count, huntCandidates.Count, spawnHops);
                    target = await TeleportAndTryFindCombatTargetAsync(
                        spawn, huntIds, finishedTargetGuids, CombatSpawnTargetSearchTimeout, cancellationToken);
                    if (target is null)
                        continue;
                }

                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                    PacketCodec.ClientEntitySelect(target.Guid), cancellationToken);
                bool killed = await FightUntilDeathAsync(target.Guid, cancellationToken);
                if (killed)
                {
                    kills++;
                    uint killedCreature = target.CreatureId ?? 0u;
                    killsByCreature[killedCreature] = killsByCreature.GetValueOrDefault(killedCreature) + 1;
                    finishedTargetGuids.Add(target.Guid);
                    itemHuntProgress = FormatItemHunt(itemId, sources, kills, killsByCreature, spawnVisits.Count, huntCandidates.Count, spawnHops);
                }
            }
        }
        itemHuntProgress = null;

        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestAccept,
            PacketCodec.ClientQuestAccept(definition.Id, item.Location, item.BagIndex), cancellationToken);
        await WaitForQuestStateAsync(QuestState.Accepted, cancellationToken);
        ushort acquisitionWorld = questWorldId;
        questWorldId = DetermineItemQuestProgressWorld(definition);
        return $"item {itemId} from creature(s) [{sources.DescribeCreatures()}] after {kills} kill(s); accepted from {item.Location} slot {item.BagIndex}; acquisition world {acquisitionWorld}, quest world {questWorldId}";
    }

    private static string FormatItemHunt(
        uint itemId,
        ItemDropSources sources,
        int kills,
        IReadOnlyDictionary<uint, int> killsByCreature,
        int spawnsVisited,
        int spawnCount,
        int spawnHops)
    {
        string perCreature = killsByCreature.Count == 0
            ? "none"
            : string.Join(',', killsByCreature.Select(pair => $"{pair.Key}x{pair.Value}"));
        return $"item {itemId} hunt: {kills} kill(s) [{perCreature}] of source creature(s) [{sources.DescribeCreatures()}], "
            + $"{spawnsVisited} of {spawnCount} spawn(s) visited in {spawnHops} teleport(s), no drop yet";
    }

    private ushort DetermineItemQuestProgressWorld(QuestDefinition definition)
    {
        var objectiveWorlds = new List<ushort>();
        foreach (QuestObjectiveDefinition objective in definition.Objectives)
        {
            if (objective.WorldLocationId != 0u)
                objectiveWorlds.Add(checked((ushort)RequireGameData().GetWorldLocation(objective.WorldLocationId).WorldId));

            HashSet<uint> creatureIds = GetObjectiveCreatureIds(objective).ToHashSet();
            objectiveWorlds.AddRange(spawns
                .Where(spawn => creatureIds.Contains(spawn.CreatureId))
                .Select(spawn => spawn.WorldId));
        }

        ushort[] receiverWorlds = spawns
            .Where(spawn => definition.ReceiverCreatureIds.Contains(spawn.CreatureId))
            .Select(spawn => spawn.WorldId)
            .Distinct()
            .ToArray();
        ushort[] sharedWorlds = objectiveWorlds.Intersect(receiverWorlds).Distinct().ToArray();
        if (sharedWorlds.Length > 0)
            return sharedWorlds.Contains(currentWorldId) ? currentWorldId : sharedWorlds[0];
        if (objectiveWorlds.Count > 0)
            return objectiveWorlds.Contains(currentWorldId)
                ? currentWorldId
                : objectiveWorlds.GroupBy(worldId => worldId).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).First().Key;
        if (receiverWorlds.Length > 0)
            return receiverWorlds.Contains(currentWorldId) ? currentWorldId : receiverWorlds[0];
        return currentWorldId;
    }

    private InventoryItemInfo? FindInventoryItem(uint itemId)
        => inventory.Values
            .Where(item => item.ItemId == itemId && item.Location == InventoryLocation.Inventory && item.StackCount > 0u)
            .OrderBy(item => item.BagIndex)
            .FirstOrDefault();

    private uint GetInventoryItemCount(uint itemId)
        => (uint)inventory.Values
            .Where(item => item.ItemId == itemId && item.Location == InventoryLocation.Inventory)
            .Aggregate(0ul, (total, item) => total + item.StackCount);

    private async Task MoveToMentionedQuestWorldAsync(
        QuestDefinition definition,
        CancellationToken cancellationToken)
    {
        if (currentWorldId == questWorldId)
            return;

        WorldLocation2Entry? location = definition.Objectives
            .Where(objective => objective.WorldLocationId != 0u)
            .Select(objective => RequireGameData().GetWorldLocation(objective.WorldLocationId))
            .FirstOrDefault(candidate => candidate.WorldId == questWorldId);
        if (location is not null)
        {
            await TeleportToLocationAsync(location, 0f, cancellationToken);
            return;
        }

        HashSet<uint> objectiveCreatureIds = definition.Objectives
            .SelectMany(GetObjectiveCreatureIds)
            .ToHashSet();
        Spawn anchor = spawns
            .Where(spawn => spawn.WorldId == questWorldId && objectiveCreatureIds.Contains(spawn.CreatureId))
            .OrderBy(spawn => spawn.Id)
            .FirstOrDefault()
            ?? throw new BotFailureException(
                $"Mentioned quest {definition.Id} has objectives in world {questWorldId}, but no objective location or spawn can anchor travel.");
        await TeleportToSpawnLocationAsync(anchor, cancellationToken);
    }

    private async Task<string> AcceptCommunicatorQuestAsync(
        QuestDefinition definition,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CommunicatorOffer> offers = RequireGameData().GetCommunicatorOffers(definition.Id);
        CommunicatorOffer? offer = offers.FirstOrDefault(candidate =>
                (candidate.WorldId == 0u || candidate.WorldId == questWorldId)
                && currentLevel >= candidate.MinLevel
                && (candidate.MaxLevel == 0u || currentLevel <= candidate.MaxLevel))
            ?? offers.FirstOrDefault(candidate => candidate.WorldId <= ushort.MaxValue
                && currentLevel >= candidate.MinLevel
                && (candidate.MaxLevel == 0u || currentLevel <= candidate.MaxLevel)
                && SpawnsFor(definition.ReceiverCreatureIds, (ushort)candidate.WorldId).Count > 0);
        if (offer is null)
        {
            if (offers.Count == 0)
                throw new BotFailureException("no giver spawn and no communicator offer");
            throw new BotFailureException(
                $"no communicator offer is usable at level {currentLevel}; offers: {string.Join(" | ", offers.Select(FormatCommunicatorOffer))}");
        }
        if (offer.WorldId != 0u)
            questWorldId = checked((ushort)offer.WorldId);

        if (currentLevel < offer.MinLevel || offer.MaxLevel != 0u && currentLevel > offer.MaxLevel)
            throw new BotFailureException(
                $"communicator {offer.Id} requires level [{offer.MinLevel},{offer.MaxLevel}] but bot is level {currentLevel}");

        string offerDetail = FormatCommunicatorOffer(offer);

        // a quest the server already mentioned (the communicator call fired) is accepted from anywhere; try that first, the
        // receiver anchor only matters when the call has not fired (He's the Brains: the server had mentioned it, the bot
        // never sent the accept because its receiver was out of view)
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestAccept,
            PacketCodec.ClientQuestAccept(definition.Id), cancellationToken);
        if (await TryWaitForQuestStateAsync(QuestState.Accepted, QuestNpcConfirmationTimeout, cancellationToken))
            return $"accepted from communicator (already mentioned, no anchor needed); {offerDetail}";

        // a zone entry call (A Hidden Threat: zone 1083, The Hidden Grotto) fires when the player walks into its zone. The server
        // counts a player as in a zone within 60 m of a WorldLocation2 row of the zone or its sub zones when the map file has no
        // area for it (the grotto's rows sit under its sub zones 2007-2010, 90 m from the quest's own marker, which lies in The
        // Crystal Shards): stand at the zone row nearest the first objective marker, else at the marker, wait for the call, accept
        if (offer.WorldZoneId != 0u && questState != QuestState.Mentioned)
        {
            WorldLocation2Entry? objectiveMarker = definition.Objectives
                .Where(candidate => candidate.WorldLocationId != 0u)
                .Select(candidate => RequireGameData().TryGetWorldLocation(candidate.WorldLocationId))
                .FirstOrDefault(candidate => candidate is not null && candidate.WorldId == questWorldId);
            IReadOnlyList<WorldLocation2Entry> zoneRows = RequireGameData().ZoneLocations(offer.WorldZoneId, questWorldId);
            WorldLocation2Entry? zoneHint = zoneRows.Count == 0
                ? objectiveMarker
                : objectiveMarker is null
                    ? zoneRows[0]
                    : zoneRows.MinBy(row => Vector3.DistanceSquared(
                        new Vector3(row.Position0, row.Position1, row.Position2),
                        new Vector3(objectiveMarker.Position0, objectiveMarker.Position1, objectiveMarker.Position2)));
            if (zoneHint is not null)
            {
                string where = zoneRows.Count == 0 ? "objective marker" : $"zone {zoneHint.WorldZoneId} location";
                log.Info($"communicator {offer.Id} waits for zone {offer.WorldZoneId}; standing at {where} {zoneHint.Id} ({zoneHint.Position0:0},{zoneHint.Position1:0},{zoneHint.Position2:0}) for the call");
                await TeleportToLocationAsync(zoneHint, 0f, cancellationToken);
                if (await TryWaitForQuestStateAsync(QuestState.Mentioned, TimeSpan.FromSeconds(8), cancellationToken))
                {
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestAccept,
                        PacketCodec.ClientQuestAccept(definition.Id), cancellationToken);
                    if (await TryWaitForQuestStateAsync(QuestState.Accepted, QuestNpcConfirmationTimeout, cancellationToken))
                        return $"accepted from communicator after entering its zone at objective marker {zoneHint.Id}; {offerDetail}";
                }
                else
                    log.Info($"communicator {offer.Id}: no call within 8 s at location {zoneHint.Id}; trying the receiver anchor");
            }
        }

        QuestNpcUseResult? anchor = await TryEveryQuestNpcAsync(
            "receiver anchor",
            definition.ReceiverCreatureIds,
            questWorldId,
            async (_, token) =>
            {
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestAccept,
                    PacketCodec.ClientQuestAccept(definition.Id), token);
                return await TryWaitForQuestStateAsync(
                    QuestState.Accepted,
                    QuestNpcConfirmationTimeout,
                    token);
            },
            cancellationToken);
        if (anchor is null)
        {
            throw new BotFailureException(
                $"communicator accept was not confirmed; {offerDetail}; {FormatQuestNpcAttempts()}; {FormatNearbyTrackedCreatures()}");
        }

        return $"accepted from communicator; {offerDetail}; anchor creature={anchor.Entity.CreatureId}; {anchor.Attempts}";
    }

    private string FormatCommunicatorOffer(CommunicatorOffer offer)
    {
        string questConditions = offer.QuestConditions.Count == 0
            ? "none"
            : string.Join(",", offer.QuestConditions.Select(item => $"{item.QuestId}:{item.State}"));
        return $"communicator={offer.Id}; entryWorld={offer.WorldId}; currentWorld={questWorldId}; levels=[{offer.MinLevel},{offer.MaxLevel}]; questStates=[{questConditions}]; prerequisite={offer.PrerequisiteId}";
    }

    private async Task<string> RunObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        activeObjective = objective;
        objectiveCastResultDetails.Clear();
        ObjectiveReport objectiveReport = RequireObjectiveReport(objective.Index);
        try
        {
            if (IsObjectiveComplete(objective))
            {
                objectiveReport.Ok = true;
                objectiveReport.Failure = null;
                return $"{objective.Type}; progress {objectiveReport.Progress}/{objectiveReport.Count}; already complete";
            }

            switch (objective.Type)
            {
                case QuestObjectiveType.TalkTo:
                    await RunInteractionObjectiveAsync(objective, InteractionType.TalkTo, cancellationToken);
                    break;
                case QuestObjectiveType.TalkToTargetGroup:
                    await RunTargetGroupInteractionObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.ActivateEntity when objective.Data == 0u && objective.Count == 1000u:
                    // "Raise your Reputation with the Algoroc Accord": the server fills it from the reward faction reputation
                    // (3000 = Liked); the plan runs these quests last so the zone's other quests have paid their reputation
                    DateTimeOffset reputationDeadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20);
                    while (!IsObjectiveComplete(objective) && DateTimeOffset.UtcNow < reputationDeadline)
                        if (await TryReadWorldPacketAsync(reputationDeadline - DateTimeOffset.UtcNow, cancellationToken) is null)
                            break;
                    if (!IsObjectiveComplete(objective))
                        throw new BotFailureException($"reputation objective at {GetRawProgress(objective.Index)}/1000: the zone's quests so far did not reach 3000 reputation with faction {RequireQuest().Entry.Faction2IdRewardReputation00}");
                    break;
                case QuestObjectiveType.ActivateEntity:
                    await RunActivateObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.ActivateTargetGroup:
                case QuestObjectiveType.ActivateTargetGroupChecklist:
                case QuestObjectiveType.Unknown10:
                case QuestObjectiveType.Unknown28:
                    await RunTargetGroupActivateObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.VirtualCollect:
                    await RunVirtualCollectObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.SpellSuccess:
                    await RunSpellSuccessObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.GatheResource:
                    await RunGatherResourceObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.SucceedCSI:
                    await RunSucceedCsiObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.CollectItem when !itemDropSources.TryGetValue(objective.Data, out ItemDropSources? collectSources) || collectSources.CreatureIds.Count == 0:
                    // no creature drops the item: it comes from using the objective's reward pane units (By Leaps and Bounds: loftite
                    // collected by jumping through the Loftite Crystals of target group 7534), so activate those until the count is met
                    await RunCollectByActivationObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.KillCreature:
                case QuestObjectiveType.KillCreature2:
                case QuestObjectiveType.KillTargetGroup:
                case QuestObjectiveType.KillTargetGroups:
                case QuestObjectiveType.CollectItem:
                    await RunKillObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.EnterArea when objective.WorldLocationId == 0u:
                    await RunEnterZoneAreaObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.EnterArea:
                case QuestObjectiveType.EnterZone:
                    await RunLocationObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.CompleteEvent:
                    await RunCompleteEventObjectiveAsync(objective, cancellationToken);
                    break;
                default:
                    throw new BotFailureException($"unsupported objective type {objective.Type}({(uint)objective.Type})");
            }

            objectiveReport.Ok = true;
            objectiveReport.Failure = null;
            string interactable = objectiveReport.Interactables.Count == 0
                ? ""
                : $"; interactable: {string.Join(" | ", objectiveReport.Interactables)}";
            string combat = objectiveReport.Kills.Count == 0
                ? ""
                : $"; foughtBack={objectiveReport.FoughtBack?.ToString().ToLowerInvariant()}, enemyCasts={objectiveReport.EnemyCasts}, damageTaken={objectiveReport.DamageTaken}, firstEnemyActionMs={objectiveReport.FirstEnemyActionMs?.ToString() ?? "null"}";
            string castResults = FormatObjectiveCastResults();
            return $"{objective.Type}; progress {objectiveReport.Progress}/{objectiveReport.Count}{interactable}{combat}{castResults}";
        }
        catch (Exception exception)
        {
            objectiveReport.Ok = false;
            string failure = botDied ? "bot died" : exception.Message;
            objectiveReport.Failure = failure + FormatObjectiveCastResults();
            if (objectiveCastResultDetails.Count > 0 && exception is not OperationCanceledException)
                throw new BotFailureException(objectiveReport.Failure);
            throw;
        }
        finally
        {
            activeObjective = null;
        }
    }

    private async Task RunInteractionObjectiveAsync(
        QuestObjectiveDefinition objective,
        InteractionType interactionType,
        CancellationToken cancellationToken)
    {
        var attemptedSpawns = new HashSet<uint>();
        IReadOnlyList<Spawn> candidates = SpawnsFor([objective.Data], questWorldId);
        while (!IsObjectiveComplete(objective))
        {
            if (objective.Data == 0u)
                throw new BotFailureException($"{objective.Type} objective has no creature id");
            uint before = GetRawProgress(objective.Index);
            Spawn spawn = ChooseNearestSpawn([objective.Data], questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity? entity = await TeleportAndTryFindAsync(spawn, TimeSpan.FromSeconds(5), cancellationToken);
            if (entity is null)
            {
                if (attemptedSpawns.Count >= candidates.Count)
                    throw new BotFailureException($"interaction target creature {objective.Data} was not visible; {FormatNearbyTrackedCreatures()}");
                continue;
            }
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
                PacketCodec.ClientEntityInteract(entity.Guid, interactionType), cancellationToken);
            if (!await TryWaitForObjectiveProgressAsync(objective.Index, before, TimeSpan.FromSeconds(8), cancellationToken)
                && attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException($"interaction with creature {objective.Data} produced no objective progress");
        }
    }

    private async Task RunTargetGroupInteractionObjectiveAsync(
        QuestObjectiveDefinition objective,
        CancellationToken cancellationToken)
    {
        uint[] creatureIds = GetObjectiveCreatureIds(objective).ToArray();
        if (creatureIds.Length == 0 && RequireGameData().IsAreaMarkerTargetGroup(objective.Data))
        {
            // TargetGroup type 9 names marker entities (Icemaw Cavern's Cave Entrances): the kills that count are the hostile
            // creatures within AreaMarkerRange of a marker (SEMANTICS: area marker target groups)
            creatureIds = AreaMarkerCreatureIds(objective).ToArray();
            log.Info($"objective {objective.Index}: area marker group {objective.Data} resolves to hostile creatures near its markers: [{string.Join(',', creatureIds)}]");
            if (creatureIds.Length == 0)
                throw new BotFailureException($"target group {objective.Data} is an area marker group (TargetGroup type 9) but no hostile spawn stands within {AreaMarkerRange:0} m of its markers in world {questWorldId}");
        }
        if (creatureIds.Length == 0)
            throw new BotFailureException($"target group {objective.Data} resolved to no creature ids");

        var attemptedSpawns = new HashSet<uint>();
        IReadOnlyList<Spawn> candidates = SpawnsFor(creatureIds, questWorldId);
        while (!IsObjectiveComplete(objective))
        {
            Spawn spawn = ChooseNearestSpawn(creatureIds, questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            uint before = GetRawProgress(objective.Index);
            TrackedEntity? entity = await TeleportAndTryFindAsync(spawn, TimeSpan.FromSeconds(5), cancellationToken);
            if (entity is null)
            {
                if (attemptedSpawns.Count >= candidates.Count)
                    throw new BotFailureException($"interaction target creature(s) {string.Join(',', creatureIds)} were not visible; {FormatNearbyTrackedCreatures()}");
                continue;
            }
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
                PacketCodec.ClientEntityInteract(entity.Guid, InteractionType.TalkTo), cancellationToken);
            if (!await TryWaitForObjectiveProgressAsync(objective.Index, before, TimeSpan.FromSeconds(8), cancellationToken)
                && attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException($"talking to target group {objective.Data} produced no objective progress");
        }
    }

    private async Task RunCollectByActivationObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        BotGameData data = RequireGameData();
        uint[] creatureIds = data.ResolveTargetGroup(objective.RewardTargetGroupId)
            .Concat(data.GetCreatureIdsFromText(objective.Text))
            .Where(data.IsCreature)
            .Distinct()
            .ToArray();
        if (creatureIds.Length == 0)
            throw new BotFailureException($"item {objective.Data} has no drop source in the world database and the objective names no unit to use");
        var attemptedSpawns = new HashSet<uint>();
        IReadOnlyList<Spawn> candidates = SpawnsFor(creatureIds, questWorldId);
        if (candidates.Count == 0)
            throw new BotFailureException($"no spawn in world {questWorldId} for the units {string.Join(',', creatureIds)} that give item {objective.Data}");
        while (!IsObjectiveComplete(objective))
        {
            uint before = GetRawProgress(objective.Index);
            if (attemptedSpawns.Count >= candidates.Count)
            {
                // every unit used once; they come back on the respawn timer
                log.Info($"objective {objective.Index}: all {candidates.Count} units used once at {before}/{objective.Count}; waiting for respawns");
                attemptedSpawns.Clear();
                await DrainWorldPacketsAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            Spawn spawn = ChooseNearestSpawn(creatureIds, questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity? entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
            if (entity is null)
                continue;
            ActivationOutcome outcome = await RunActivationFlowAsync(objective.Index, before, entity, TimeSpan.FromSeconds(8), cancellationToken);
            log.Info($"objective {objective.Index}: used unit {entity.CreatureId} guid {entity.Guid}: {outcome}; progress {GetRawProgress(objective.Index)}/{objective.Count}");
            if (outcome == ActivationOutcome.Stunned)
                await RetreatFromStunAsync(spawn, attemptedSpawns, cancellationToken);
        }
    }

    private readonly Dictionary<uint, int> stunRetreats = [];

    /// <summary>
    /// Stunned while using a unit (a boss telegraph such as the Boulderback's Loftite Shards covers it): step 30 m away for a few
    /// seconds, forget the attempt and come back, twice per spawn. Returns false once the retreats for this spawn are used up.
    /// </summary>
    private async Task<bool> RetreatFromStunAsync(Spawn spawn, HashSet<uint> attemptedSpawns, CancellationToken cancellationToken)
    {
        int retreats = stunRetreats.GetValueOrDefault(spawn.Id);
        if (retreats >= 2)
            return false;
        stunRetreats[spawn.Id] = retreats + 1;
        log.Info($"stunned at spawn {spawn.Id} (creature {spawn.CreatureId}); retreat {retreats + 1}/2: 30 m away for 8 s, then back");
        var away = new Vector3(spawn.X + 30f, spawn.Y, spawn.Z + 30f);
        await SendTeleportAsync(PacketCodec.TeleportCommand(away), false, cancellationToken);
        lastPosition = away;
        await DrainWorldPacketsAsync(TimeSpan.FromSeconds(8), cancellationToken);
        attemptedSpawns.Remove(spawn.Id);
        return true;
    }

    private async Task RunActivateObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        if (IsObjectiveComplete(objective))
            return;
        if (objective.Data == 0u && objective.RewardTargetGroupId == 0u && objective.EquipItemId == 0u)
        {
            await RunTargetlessActivateObjectiveAsync(objective, cancellationToken);
            return;
        }

        uint[] creatureIds = GetActivateCreatureIds(objective).ToArray();
        if (creatureIds.Length == 0)
            throw new BotFailureException("ActivateEntity objective has no creature, target-group, reward-pane, or text target");
        var attemptedSpawns = new HashSet<uint>();
        IReadOnlyList<Spawn> candidates = SpawnsFor(creatureIds, questWorldId);
        int respawnWaits = 0;
        while (!IsObjectiveComplete(objective))
        {
            uint before = GetRawProgress(objective.Index);
            Spawn spawn = ChooseNearestSpawn(creatureIds, questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity? entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
            if (entity is null)
            {
                if (attemptedSpawns.Count >= candidates.Count)
                {
                    // a unit used by an earlier objective comes back on the respawn timer (Breaching Security uses the Avatar Control
                    // Panel twice, 60 s apart); a player waits at the spot, so wait twice before giving up
                    if (respawnWaits++ >= 2)
                        throw new BotFailureException($"activation target creature(s) {string.Join(',', creatureIds)} were not visible after {respawnWaits - 1} respawn wait(s); {FormatNearbyTrackedCreatures()}");
                    log.Info($"objective {objective.Index}: none of the {candidates.Count} spawn(s) of creature(s) [{string.Join(',', creatureIds)}] is up; waiting 35 s for a respawn");
                    attemptedSpawns.Clear();
                    await DrainWorldPacketsAsync(TimeSpan.FromSeconds(35), cancellationToken);
                }
                continue;
            }
            ActivationOutcome outcome = await RunActivationFlowAsync(objective.Index, before, entity, TimeSpan.FromSeconds(8), cancellationToken);
            if (outcome == ActivationOutcome.Progressed && CanFight(entity.Type) && entity.CreatureId is uint usedCreatureId
                && RequireGameData().IsHoldoutHostile(usedCreatureId) && entities.Get(entity.Guid)?.Health is null or > 0u)
            {
                // a used hostile (Weapons of War: a loftite grenade on a Dominion Sentry) is awake now; finish it before the next
                // one, six sentries attacking at once killed the bot twice
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                    PacketCodec.ClientEntitySelect(entity.Guid), cancellationToken);
                _ = await FightUntilDeathAsync(entity.Guid, cancellationToken);
            }
            if (outcome == ActivationOutcome.TargetNotUsable)
                continue;
            if (outcome == ActivationOutcome.Stunned && await RetreatFromStunAsync(spawn, attemptedSpawns, cancellationToken))
                continue;
            if (outcome != ActivationOutcome.Progressed
                && attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException($"activation of creature(s) {string.Join(',', creatureIds)} produced no objective progress");
        }
    }

    private async Task RunTargetlessActivateObjectiveAsync(
        QuestObjectiveDefinition objective,
        CancellationToken cancellationToken)
    {
        uint before = GetRawProgress(objective.Index);
        if (objective.WorldLocationId == 0u)
        {
            if (!await TryWaitForObjectiveProgressAsync(objective.Index, before, TimeSpan.FromSeconds(5), cancellationToken))
                throw new BotFailureException("target-less ActivateEntity without an indicator did not receive its server completion within 5 s");
            return;
        }

        WorldLocation2Entry location = RequireGameData().GetWorldLocation(objective.WorldLocationId);
        await TeleportToLocationAsync(location, 0f, cancellationToken);
        if (await TryWaitForObjectiveProgressAsync(objective.Index, before, TimeSpan.FromSeconds(20), cancellationToken))
            return;

        float step = location.Radius > 0f ? Math.Min(1f, location.Radius * 0.5f) : 1f;
        await TeleportToLocationAsync(location, step, cancellationToken);
        if (!await TryWaitForObjectiveProgressAsync(objective.Index, before, TimeSpan.FromSeconds(10), cancellationToken))
            throw new BotFailureException($"target-less ActivateEntity produced no progress at indicator {location.Id} (radius {location.Radius:R}) after 20 s plus a {step:R} m step and 10 s");
    }

    private async Task RunTargetGroupActivateObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        uint[] creatureIds = GetObjectiveCreatureIds(objective).ToArray();
        if (creatureIds.Length == 0)
            throw new BotFailureException($"target group {objective.Data} resolved to no creature ids");

        bool checklist = IsChecklistObjective(objective);
        var attemptedSpawns = new HashSet<uint>();
        IReadOnlyList<Spawn> candidates = SpawnsFor(creatureIds, questWorldId);
        if (candidates.Count == 0)
            throw new BotFailureException($"No spawn in world {questWorldId} for target group {objective.Data}.");

        while (!IsObjectiveComplete(objective))
        {
            Spawn spawn = ChooseNearestSpawn(creatureIds, questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            // proximity-credited entities (the Unusual Rocks tick when the character comes within 4 m) progress during the
            // teleport itself, before any activation: count that as progress too
            uint beforeTeleport = GetRawProgress(objective.Index);
            TrackedEntity? entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
            if (IsObjectiveComplete(objective))
                break;
            if (GetRawProgress(objective.Index) != beforeTeleport)
                continue;
            if (entity is null)
            {
                if (attemptedSpawns.Count >= candidates.Count)
                    throw new BotFailureException($"checklist target creature(s) {string.Join(',', creatureIds)} were not visible; {FormatNearbyTrackedCreatures()}");
                continue;
            }
            uint before = GetRawProgress(objective.Index);
            ActivationOutcome outcome = await RunActivationFlowAsync(objective.Index, before, entity, TimeSpan.FromSeconds(8), cancellationToken);
            if (IsObjectiveComplete(objective))
                break;
            if (outcome == ActivationOutcome.TargetNotUsable)
                continue;
            if (outcome != ActivationOutcome.Progressed && attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException(checklist
                    ? $"checklist entities for target group {objective.Data} produced no further progress"
                    : $"activation targets for group {objective.Data} produced no further progress");
        }
    }

    private const uint ZoneDeathRetryLevelCap = 18u;

    private const int ScavengerClueSpawnAttempts = 6;

    private const int SpecimenSpawnAttempts = 3;

    private const int ActivateMissionSpawnRounds = 3;

    private const float QuestAbilityTargetRadius = 60f;

    /// <summary>
    /// A SpellSuccess objective: the quest tracker shows an ability (the objective's spell) the player casts on the objective's
    /// targets. Targets come from the reward pane group or the objective text; with neither, any hostile near the objective's
    /// map indicator (Use Augmented Remains on constructs near Annihilator Silo 14). The server allows the cast while the
    /// objective is open (SEMANTICS: quest abilities).
    /// </summary>
    private async Task RunSpellSuccessObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        BotGameData data = RequireGameData();
        uint[] creatureIds = data.ResolveTargetGroup(objective.RewardTargetGroupId).ToArray();
        if (creatureIds.Length == 0)
            creatureIds = data.GetCreatureIdsFromText(objective.Text).ToArray();
        bool hostile = creatureIds.Length == 0 || creatureIds.All(data.IsHoldoutHostile);
        WorldLocation2Entry? indicator = objective.WorldLocationId != 0u ? data.TryGetWorldLocation(objective.WorldLocationId) : null;
        if (creatureIds.Length == 0 && (indicator is null || indicator.WorldId != questWorldId))
            throw new BotFailureException($"quest ability {objective.Data}: no target creature (reward pane group {objective.RewardTargetGroupId}, text names none) and no map indicator in world {questWorldId}");
        log.Info($"objective {objective.Index}: quest ability {objective.Data} x{objective.Count} on creature(s) [{string.Join(',', creatureIds)}] hostile={hostile}"
            + (creatureIds.Length == 0 ? $"; any hostile within {QuestAbilityTargetRadius:0} m of indicator {indicator!.Id}" : ""));

        var attemptedSpawns = new HashSet<uint>();
        var usedGuids = new HashSet<uint>();
        bool atIndicator = false;
        while (!IsObjectiveComplete(objective))
        {
            TrackedEntity? entity;
            if (creatureIds.Length > 0)
            {
                IReadOnlySet<uint> wanted = creatureIds.ToHashSet();
                entity = entities.FindNearestVisibleNonPlayer(wanted, lastPosition, QuestAbilityTargetRadius, usedGuids)
                    ?? entities.FindNearestVisible(wanted, lastPosition, QuestAbilityTargetRadius, usedGuids);
                if (entity is null)
                {
                    Spawn spawn = ChooseCombatSpawn(creatureIds, questWorldId, attemptedSpawns);
                    attemptedSpawns.Add(spawn.Id);
                    entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken, usedGuids);
                }
            }
            else
            {
                if (!atIndicator)
                {
                    await TeleportToSpawnLocationAsync(new Spawn(0u, 0u, questWorldId, indicator!.Position0, indicator.Position1, indicator.Position2), cancellationToken);
                    atIndicator = true;
                }
                entity = entities.FindNearestVisibleNonPlayer(lastPosition, QuestAbilityTargetRadius, usedGuids, data.IsHoldoutHostile);
                if (entity is null)
                {
                    // respawns stream in; keep looking until the objective budget ends
                    await DrainWorldPacketsAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    continue;
                }
            }
            if (entity is null)
                continue;

            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                PacketCodec.ClientEntitySelect(entity.Guid), cancellationToken);
            uint before = GetRawProgress(objective.Index);
            bool progressed = await CastQuestAbilityAsync(objective, entity, cancellationToken);
            usedGuids.Add(entity.Guid);
            if (IsObjectiveComplete(objective))
                break;
            // a usable object (the retrofitted cannon of Small But Effective, faction 219 with an activation spell) is never fought:
            // the bot stood hitting it for the whole budget after the server had already credited the launch
            if (hostile && data.IsAttackableCreature(entity.CreatureId ?? 0u) && !combatTargetDestroyed && (entities.Get(entity.Guid)?.Health is null or > 0u))
            {
                // the target is awake now: finish it so the next cast is not made under attack (and a sibling kill objective counts)
                _ = await FightUntilDeathAsync(entity.Guid, cancellationToken);
            }
            if (!progressed)
                log.Info($"objective {objective.Index}: quest ability {objective.Data} on guid {entity.Guid} (creature {entity.CreatureId}) gave no progress; trying another target");
        }
    }

    private async Task<bool> CastQuestAbilityAsync(QuestObjectiveDefinition objective, TrackedEntity entity, CancellationToken cancellationToken, int cooldownRetries = 0)
    {
        await FaceCombatTargetAsync(entity.Guid, true, cancellationToken);
        uint before = GetRawProgress(objective.Index);
        uint clientUniqueId = spellCastId++;
        pendingActivationCastIds.Add(clientUniqueId);
        try
        {
            Vector3 position = entities.Get(entity.Guid)?.Position ?? entity.Position ?? lastPosition;
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientCastObjectiveAbility,
                PacketCodec.ClientCastObjectiveAbility(clientUniqueId, objective.Data, entity.Guid, position), cancellationToken);
            ActivationCastResponse response = await TryWaitForActivationCastResponseAsync(
                clientUniqueId, entity.Guid, TimeSpan.FromSeconds(5), cancellationToken);
            if (response.Result is SpellCastResult result && result.Result != CastResult.Ok)
            {
                log.Info($"objective {objective.Index}: quest ability {objective.Data} refused: {result.Result}(0x{(uint)result.Result:X4})");
                // the cast right after a fight lands in the global cooldown of the last combat ability (Unconventional
                // Technology: every second target was skipped that way), and the ability has its own cooldown (10 s on
                // Augmented Remains): wait it out and cast at the same target again
                if (result.Result is CastResult.SpellGlobalCooldown or CastResult.SpellCooldown && cooldownRetries < 3)
                {
                    pendingActivationCastIds.Remove(clientUniqueId);
                    activationCastResults.Remove(clientUniqueId);
                    await DrainWorldPacketsAsync(result.Result == CastResult.SpellCooldown ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(1.5), cancellationToken);
                    return await CastQuestAbilityAsync(objective, entity, cancellationToken, cooldownRetries + 1);
                }
                return false;
            }
            // the cast runs its cast time (2-3 s) before the server credits the objective
            return await TryWaitForObjectiveProgressAsync(objective.Index, before, TimeSpan.FromSeconds(12), cancellationToken);
        }
        finally
        {
            pendingActivationCastIds.Remove(clientUniqueId);
            activationCastResults.Remove(clientUniqueId);
        }
    }

    /// <summary>
    /// A virtual item whose reward pane creatures are hostile is taken from their corpses (the server credits the kill).
    /// </summary>
    private bool VirtualCollectIsFight(QuestObjectiveDefinition objective)
    {
        uint[] creatureIds = GetObjectiveCreatureIds(objective).ToArray();
        // a usable object (a Protostar Battery Pack on the sea floor, faction 219 with an activation spell) is used, not fought
        return creatureIds.Length > 0 && creatureIds.All(creatureId => RequireGameData().IsAttackableCreature(creatureId));
    }

    // a new character has 16 inventory slots (InventoryLocation.Inventory capacity); loot delivered into full bags is lost
    // on the server side (the loot item is marked delivered even when ItemCreate refused it), so junk goes before the loot
    private const int LootBagSlotsToKeepFree = 12;
    // the server refused an item for lack of bag space since junk was last cleared
    private bool bagsReportedFull;

    private async Task RunVirtualCollectObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        uint[] creatureIds = GetObjectiveCreatureIds(objective).ToArray();
        if (creatureIds.Length == 0)
            throw new BotFailureException($"target group {objective.Data} resolved to no creature ids");
        if (VirtualCollectIsFight(objective))
        {
            // Rockridge Rascals: "retrieve the loot from Arifo the Rascal" is a kill, the 20 s use attempt only aggroed him
            log.Info($"objective {objective.Index}: virtual item {objective.Data} is held by hostile creature(s) [{string.Join(',', creatureIds)}]; fighting for it");
            await RunKillObjectiveAsync(objective, cancellationToken);
            return;
        }

        var attemptedSpawns = new HashSet<uint>();
        while (!IsObjectiveComplete(objective))
        {
            Spawn spawn = ChooseCombatSpawn(creatureIds, questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity? entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
            if (entity is null)
                continue;
            uint before = GetRawProgress(objective.Index);
            ActivationOutcome outcome = await RunActivationFlowAsync(
                objective.Index, before, entity, TimeSpan.FromSeconds(20), cancellationToken);
            if (outcome is ActivationOutcome.Progressed or ActivationOutcome.TargetNotUsable)
                continue;

            if (!CanFight(entity.Type))
                continue;
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                PacketCodec.ClientEntitySelect(entity.Guid), cancellationToken);
            _ = await FightUntilProgressAsync(objective.Index, before, entity.Guid, cancellationToken);
        }
    }

    private async Task RunGatherResourceObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        if (objective.Data == 0u)
            throw new BotFailureException("GatheResource objective has no creature id");
        var attemptedSpawns = new HashSet<uint>();
        while (!IsObjectiveComplete(objective))
        {
            Spawn spawn = ChooseCombatSpawn([objective.Data], questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity? entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
            if (entity is null)
                continue;
            uint before = GetRawProgress(objective.Index);
            ActivationOutcome outcome = await RunActivationFlowAsync(objective.Index, before, entity, TimeSpan.FromSeconds(8), cancellationToken);
            if (outcome == ActivationOutcome.Progressed)
                continue;
            if (outcome == ActivationOutcome.TargetNotUsable || !CanFight(entity.Type))
                continue;
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                PacketCodec.ClientEntitySelect(entity.Guid), cancellationToken);
            _ = await FightUntilProgressAsync(objective.Index, before, entity.Guid, cancellationToken);
        }
    }

    private static bool CanFight(EntityType type)
        => type is EntityType.NonPlayer or EntityType.Player or EntityType.Vehicle or EntityType.Mount;

    private async Task<ActivationOutcome> RunActivationFlowAsync(
        int objectiveIndex,
        uint before,
        TrackedEntity entity,
        TimeSpan progressTimeout,
        CancellationToken cancellationToken)
    {
        RecordInteractability(objectiveIndex, entity);
        for (int retry = 0; retry <= 3; retry++)
        {
            uint clientUniqueId = spellCastId++;
            pendingActivationCastIds.Add(clientUniqueId);
            try
            {
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnitCast,
                    PacketCodec.ClientActivateUnitCast(clientUniqueId, entity.Guid), cancellationToken);
                ActivationCastResponse response = await TryWaitForActivationCastResponseAsync(
                    clientUniqueId, entity.Guid, TimeSpan.FromSeconds(5), cancellationToken);
                if (response.Result is SpellCastResult castResult)
                {
                    (bool shouldRetry, ActivationOutcome? outcome) = await HandleActivationCastResultAsync(
                        castResult, retry, cancellationToken);
                    if (shouldRetry)
                        continue;
                    return outcome!.Value;
                }

                if (response.Start is SpellCastStart spellStart)
                {
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientSpellClientSideInteraction,
                        PacketCodec.ClientSpellClientSideInteraction(spellStart.ServerUniqueId), cancellationToken);
                    respondedCsiSpellIds.Add(spellStart.ServerUniqueId);
                    await TryWaitForSpellFinishAsync(spellStart.ServerUniqueId, TimeSpan.FromSeconds(5), cancellationToken);
                    if (activationCastResults.Remove(clientUniqueId, out SpellCastResult? finishResult)
                        && finishResult is not null)
                    {
                        (bool shouldRetry, ActivationOutcome? outcome) = await HandleActivationCastResultAsync(
                            finishResult, retry, cancellationToken);
                        if (shouldRetry)
                            continue;
                        return outcome!.Value;
                    }
                }
                else
                {
                    // the unit can be gone by now (a corpse looted away, a used node): the plain activation of a vanished guid was
                    // an invalid packet to the server and dropped the session (Explorer leg, 2026-11-02)
                    if (entities.Get(entity.Guid) is null)
                    {
                        log.Info($"activation target guid {entity.Guid} (creature {entity.CreatureId}) vanished before the plain activation; not usable");
                        return ActivationOutcome.TargetNotUsable;
                    }
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnit,
                        PacketCodec.ClientActivateUnit(entity.Guid), cancellationToken);
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
                        PacketCodec.ClientEntityInteract(entity.Guid, InteractionType.Interact), cancellationToken);
                }

                bool advanced = GetRawProgress(objectiveIndex) > before
                    || await TryWaitForObjectiveProgressAsync(objectiveIndex, before, progressTimeout, cancellationToken);
                if (advanced)
                    return ActivationOutcome.Progressed;
                if (activationCastResults.Remove(clientUniqueId, out SpellCastResult? progressResult)
                    && progressResult is not null)
                {
                    (bool shouldRetry, ActivationOutcome? outcome) = await HandleActivationCastResultAsync(
                        progressResult, retry, cancellationToken);
                    if (shouldRetry)
                        continue;
                    return outcome!.Value;
                }
                return ActivationOutcome.NoProgress;
            }
            finally
            {
                pendingActivationCastIds.Remove(clientUniqueId);
                activationCastResults.Remove(clientUniqueId);
            }
        }
        return ActivationOutcome.NoProgress;
    }

    private static async Task<(bool Retry, ActivationOutcome? Outcome)> HandleActivationCastResultAsync(
        SpellCastResult castResult,
        int retry,
        CancellationToken cancellationToken)
    {
        if (castResult.Result == CastResult.TargetIsNotUseable)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            return (false, ActivationOutcome.TargetNotUsable);
        }
        if (castResult.Result == CastResult.CCStun)
            return (false, ActivationOutcome.Stunned); // the caller steps away from whatever stuns here and comes back
        if (retry < 3)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            return (true, null);
        }
        return (false, ActivationOutcome.NoProgress);
    }

    private async Task RunSucceedCsiObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        var attemptedSpawns = new HashSet<uint>();
        IReadOnlyList<Spawn> candidates = SpawnsFor([objective.Data], questWorldId);
        if (candidates.Count == 0)
            throw new BotFailureException($"No spawn in world {questWorldId} for CSI creature {objective.Data}.");

        while (!IsObjectiveComplete(objective))
        {
            Spawn spawn = ChooseNearestSpawn([objective.Data], questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity? entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
            if (entity is null)
            {
                if (attemptedSpawns.Count >= candidates.Count)
                    throw new BotFailureException($"CSI target creature {objective.Data} was not visible; {FormatNearbyTrackedCreatures()}");
                continue;
            }
            uint before = GetRawProgress(objective.Index);
            ActivationOutcome outcome = await RunActivationFlowAsync(
                objective.Index, before, entity, TimeSpan.FromSeconds(8), cancellationToken);
            if (outcome == ActivationOutcome.TargetNotUsable)
                continue;
            if (outcome == ActivationOutcome.Stunned && await RetreatFromStunAsync(spawn, attemptedSpawns, cancellationToken))
                continue;
            if (outcome != ActivationOutcome.Progressed && attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException($"CSI creature {objective.Data} did not provide enough activatable spawns");
        }
    }

    private void RecordInteractability(int objectiveIndex, TrackedEntity entity)
    {
        if (entity.CreatureId is not uint creatureId)
            return;
        CreatureActivationData activation = RequireGameData().GetCreatureActivationData(creatureId);
        string evidence = $"guid={entity.Guid}, creature={creatureId}, createFlags={entity.CreateFlags}(0x{(byte)entity.CreateFlags:X2}), questChecklistIdx={entity.QuestChecklistIdx?.ToString() ?? "none"}, activationSpells=[{string.Join(',', activation.ActivationSpellIds)}], unitVisualType={activation.UnitVisualTypeId}, activationFlags=0x{activation.ActivationFlags:X}, activateSpellTextId={activation.ActivateSpellTextId}, activateSpellText=\"{activation.ActivateSpellText.Replace("\"", "'")}\"";
        ObjectiveReport objective = RequireObjectiveReport(objectiveIndex);
        if (!objective.Interactables.Contains(evidence, StringComparer.Ordinal))
            objective.Interactables.Add(evidence);
    }

    private async Task<ActivationCastResponse> TryWaitForActivationCastResponseAsync(
        uint clientUniqueId,
        uint targetGuid,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return new ActivationCastResponse(null, null);
            if (activationCastResults.Remove(clientUniqueId, out SpellCastResult? existingResult))
                return new ActivationCastResponse(null, existingResult);
            ProtocolPacket? packet = await TryReadWorldPacketAsync(remaining, cancellationToken);
            if (packet is null)
                return new ActivationCastResponse(null, null);

            if (activationCastResults.Remove(clientUniqueId, out SpellCastResult? result))
                return new ActivationCastResponse(null, result);

            if (packet.Opcode == GameMessageOpcode.ServerSpellStart)
            {
                SpellCastStart start = PacketCodec.ParseSpellStart(packet.Body);
                if (start.CasterId == playerGuid && start.PrimaryTargetId == targetGuid
                    && !respondedCsiSpellIds.Contains(start.ServerUniqueId))
                    return new ActivationCastResponse(start, null);
            }
            else if (packet.Opcode == GameMessageOpcode.ServerSpellConfirmCast)
            {
                SpellCastStart start = PacketCodec.ParseSpellConfirmCast(packet.Body);
                if (start.ClientUniqueId == clientUniqueId && !respondedCsiSpellIds.Contains(start.ServerUniqueId))
                    return new ActivationCastResponse(start, null);
            }
        }
    }

    private async Task<bool> TryWaitForSpellFinishAsync(
        uint serverUniqueId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (!finishedSpellIds.Remove(serverUniqueId))
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return false;
        }
        return true;
    }

    private string FormatObjectiveCastResults()
        => objectiveCastResultDetails.Count == 0
            ? ""
            : $"; castResults: {string.Join(" | ", objectiveCastResultDetails)}";

    private async Task<QuestNpcUseResult?> TryEveryQuestNpcAsync(
        string role,
        IReadOnlyList<uint> creatureIds,
        ushort worldId,
        Func<TrackedEntity, CancellationToken, Task<bool>> tryUse,
        CancellationToken cancellationToken)
    {
        foreach (uint creatureId in creatureIds.Distinct())
        {
            if (!questNpcAttemptCreatureIds.Contains(creatureId))
                questNpcAttemptCreatureIds.Add(creatureId);

            Spawn[] creatureSpawns = SpawnsFor([creatureId], worldId)
                .OrderBy(spawn => spawn.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
                .ThenBy(spawn => spawn.Id)
                .ToArray();
            if (creatureSpawns.Length == 0)
            {
                // no database spawn (script-spawned or escort NPC such as 16622 for More Important Than Revenge): use it if it is
                // already streamed in next to the bot
                TrackedEntity? visible = entities.FindCreature(creatureId);
                if (visible is not null)
                {
                    int visibleIndex = questNpcAttemptDetails.Count;
                    questNpcAttemptDetails.Add($"{role} creature {creatureId}: no spawns in world {worldId}, using visible guid {visible.Guid}");
                    bool visibleConfirmed = await tryUse(visible, cancellationToken);
                    questNpcAttemptDetails[visibleIndex] += visibleConfirmed ? ", confirmed" : ", no quest-state confirmation";
                    if (visibleConfirmed)
                        return new QuestNpcUseResult(visible, FormatQuestNpcAttempts());
                    continue;
                }
                questNpcAttemptDetails.Add($"{role} creature {creatureId}: no spawns in world {worldId}");
                continue;
            }

            foreach (Spawn spawn in creatureSpawns)
            {
                int detailIndex = questNpcAttemptDetails.Count;
                questNpcAttemptDetails.Add(
                    $"{role} creature {creatureId} spawn {spawn.Id}: waiting for visibility");
                TrackedEntity? entity = await TeleportAndTryFindWithRetryAsync(spawn, cancellationToken);
                if (entity is null)
                {
                    questNpcAttemptDetails[detailIndex] =
                        $"{role} creature {creatureId} spawn {spawn.Id}: not visible after 15 s + re-teleport 15 s ({FormatNearbyTrackedCreatures()})";
                    continue;
                }

                questNpcAttemptDetails[detailIndex] =
                    $"{role} creature {creatureId} spawn {spawn.Id}: visible guid {entity.Guid}, awaiting quest-state confirmation";
                bool confirmed = await tryUse(entity, cancellationToken);
                questNpcAttemptDetails[detailIndex] = confirmed
                    ? $"{role} creature {creatureId} spawn {spawn.Id}: visible guid {entity.Guid}, confirmed"
                    : $"{role} creature {creatureId} spawn {spawn.Id}: visible guid {entity.Guid}, no quest-state confirmation within {QuestNpcConfirmationTimeout.TotalSeconds:0} s";
                if (confirmed)
                    return new QuestNpcUseResult(entity, FormatQuestNpcAttempts());
            }
        }

        return null;
    }

    private string FormatQuestNpcAttempts()
    {
        string ids = questNpcAttemptCreatureIds.Count == 0
            ? "none"
            : string.Join(',', questNpcAttemptCreatureIds);
        string details = questNpcAttemptDetails.Count == 0
            ? "none"
            : string.Join(" | ", questNpcAttemptDetails);
        return $"attempted creature ids=[{ids}]; visibility=[{details}]";
    }

    private string FormatQuestNpcTimeoutDetail()
        => itemHuntProgress is { Length: > 0 } hunt
            ? $"{hunt}; {FormatNearbyTrackedCreatures()}"
            : $"{FormatQuestNpcAttempts()}; {FormatNearbyTrackedCreatures()}";

    private string FormatNearbyTrackedCreatures()
    {
        IReadOnlyList<(uint CreatureId, float Distance)> nearby = entities.GetNearbyCreatureIds(lastPosition, 60f, 15);
        return nearby.Count == 0
            ? "tracked creature ids within 60 m: none"
            : $"tracked creature ids within 60 m: {string.Join(", ", nearby.Select(item => FormattableString.Invariant($"{item.CreatureId}@{item.Distance:0.0}m")))}";
    }

    private async Task RunKillObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        uint[] creatureIds = GetObjectiveCreatureIds(objective).ToArray();
        (Vector3 Center, float Range)[]? areaAnchors = null;
        if (creatureIds.Length == 0 && RequireGameData().IsAreaMarkerTargetGroup(objective.Data))
        {
            // a kill group of type 9 names marker entities (Yeti Extermination: the Cave Entrances of Icemaw Cavern); the server
            // counts any hostile killed within AreaMarkerRange of a marker, so fight the hostile spawns standing there
            creatureIds = AreaMarkerCreatureIds(objective).ToArray();
            log.Info($"objective {objective.Index}: area marker kill group {objective.Data} resolves to hostile creatures near its markers: [{string.Join(',', creatureIds)}]");
            if (creatureIds.Length == 0)
                throw new BotFailureException($"target group {objective.Data} is an area marker group (TargetGroup type 9) but no hostile spawn stands within {AreaMarkerRange:0} m of its markers in world {questWorldId}");
            (Spawn[] markers, float markerRange) = AreaMarkerAnchors(objective);
            areaAnchors = markers.Select(marker => (new Vector3(marker.X, marker.Y, marker.Z), markerRange)).ToArray();
        }
        if (creatureIds.Length == 0)
            throw new BotFailureException($"target group {objective.Data} resolved to no creature ids");
        IReadOnlyList<Spawn> candidates = SpawnsFor(creatureIds, questWorldId);
        if (areaAnchors is not null)
        {
            // only a kill inside the counted radius progresses the objective: with every snoglug of Algoroc a candidate the
            // bot drifted 270 m from Icemaw Cavern's indicator and none of its fourteen kills counted (Yeti Extermination)
            int all = candidates.Count;
            candidates = candidates.Where(spawn => InArea(areaAnchors, new Vector3(spawn.X, spawn.Y, spawn.Z))).ToList();
            log.Info($"objective {objective.Index}: {candidates.Count} of {all} spawn(s) of the area marker creatures stand inside the counted radius; visible targets outside it are ignored");
        }
        if (candidates.Count == 0)
            // script-spawned enemies (the arkship Virtual Legionnaires drop in when a Sim-Turret dies) have no database spawn:
            // keep watching for them until the objective budget runs out instead of giving up at once
            log.Info($"no spawn in world {questWorldId} for combat target(s) {string.Join(',', creatureIds)}; waiting for script-spawned entities");

        IReadOnlySet<uint> objectiveCreatureIds = creatureIds.ToHashSet();
        var spawnVisits = new Dictionary<uint, int>();
        var finishedTargetGuids = new HashSet<uint>();
        combatAreaAnchors = areaAnchors;
        try
        {
            while (!IsObjectiveComplete(objective))
            {
                TrackedEntity? entity = await AcquireCombatTargetAsync(
                    objective, candidates, objectiveCreatureIds, finishedTargetGuids, spawnVisits, cancellationToken);
                if (entity is null || IsObjectiveComplete(objective))
                    break;
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                    PacketCodec.ClientEntitySelect(entity.Guid), cancellationToken);

                uint before = GetRawProgress(objective.Index);
                bool targetFinished = await FightUntilProgressAsync(
                    objective.Index, before, entity.Guid, cancellationToken);
                if (targetFinished)
                    finishedTargetGuids.Add(entity.Guid);
            }
        }
        finally
        {
            combatAreaAnchors = null;
        }
    }

    private const float AreaMarkerRange = 60f;

    // the area a kill objective counts in (area marker groups): the visible-target search and the spawn candidates stay inside it
    private IReadOnlyList<(Vector3 Center, float Range)>? combatAreaAnchors;

    private bool InCombatArea(Vector3 position)
        => combatAreaAnchors is null || InArea(combatAreaAnchors, position);

    private static bool InArea(IReadOnlyList<(Vector3 Center, float Range)> anchors, Vector3 position)
        => anchors.Any(anchor => Vector3.DistanceSquared(anchor.Center, position) <= anchor.Range * anchor.Range);
    private const float AreaMarkerIndicatorRange = 100f; // UnitEntity.AreaMarkerIndicatorRange
    private const int StunEscapeThreshold = 8;
    private int consecutiveStunResults;

    /// <summary>
    /// Creature ids of the hostile spawns within <see cref="AreaMarkerRange"/> of the marker entities a type 9 target group names.
    /// </summary>
    private (Spawn[] Markers, float Range) AreaMarkerAnchors(QuestObjectiveDefinition objective)
    {
        uint targetGroupId = objective.Data;
        IReadOnlyList<uint> markerIds = RequireGameData().AreaMarkerCreatureIds(targetGroupId);
        Spawn[] markers = SpawnsFor(markerIds, questWorldId).ToArray();
        if (markers.Length == 0 && objective.WorldLocationId != 0u
            && RequireGameData().TryGetWorldLocation(objective.WorldLocationId) is { } indicator && indicator.WorldId == questWorldId)
        {
            // no marker of the group is spawned (Yeti Extermination's Cave Entrances): the server counts kills within
            // AreaMarkerIndicatorRange of the objective's map indicator instead (SEMANTICS: area marker target groups)
            log.Info($"objective {objective.Index}: no marker of group {targetGroupId} is spawned; using its map indicator {indicator.Id} with a {AreaMarkerIndicatorRange:0} m range");
            return ([new Spawn(0u, 0u, questWorldId, indicator.Position0, indicator.Position1, indicator.Position2)], AreaMarkerIndicatorRange);
        }
        return (markers, AreaMarkerRange);
    }

    private IEnumerable<uint> AreaMarkerCreatureIds(QuestObjectiveDefinition objective)
    {
        IReadOnlyList<uint> markerIds = RequireGameData().AreaMarkerCreatureIds(objective.Data);
        (Spawn[] markers, float markerRange) = AreaMarkerAnchors(objective);
        double range = markerRange * (double)markerRange;
        return spawns
            .Where(spawn => spawn.WorldId == questWorldId && !markerIds.Contains(spawn.CreatureId)
                && RequireGameData().IsHoldoutHostile(spawn.CreatureId)
                && markers.Any(marker => spawn.DistanceSquared(marker.X, marker.Y, marker.Z) <= range))
            .Select(spawn => spawn.CreatureId)
            .Distinct();
    }

    private async Task<TrackedEntity?> AcquireCombatTargetAsync(
        QuestObjectiveDefinition objective,
        IReadOnlyList<Spawn> candidates,
        IReadOnlySet<uint> creatureIds,
        IReadOnlySet<uint> excludedGuids,
        Dictionary<uint, int> spawnVisits,
        CancellationToken cancellationToken)
    {
        if (IsObjectiveComplete(objective))
            return null;

        TrackedEntity? nearby = await TryFindVisibleCombatTargetAsync(
            creatureIds, excludedGuids, CombatLocalTargetSearchTimeout, cancellationToken);
        if (IsObjectiveComplete(objective))
            return null;
        if (nearby is not null)
            return nearby;
        TrackedEntity? approached = await TryApproachVisibleCombatTargetAsync(creatureIds, excludedGuids, cancellationToken);
        if (IsObjectiveComplete(objective))
            return null;
        if (approached is not null)
            return approached;

        while (ChooseNextCombatSpawn(candidates, spawnVisits) is Spawn spawn)
        {
            spawnVisits[spawn.Id] = spawnVisits.GetValueOrDefault(spawn.Id) + 1;
            TrackedEntity? teleportedTarget = await TeleportAndTryFindCombatTargetAsync(
                spawn, creatureIds, excludedGuids, CombatSpawnTargetSearchTimeout, cancellationToken);
            if (IsObjectiveComplete(objective))
                return null;
            if (teleportedTarget is not null)
                return teleportedTarget;
        }

        // Every spawn has received both permitted teleports. Keep watching the final area for a
        // streamed-in entity or a respawn until the enclosing 300 s budget expires.
        while (true)
        {
            nearby = await TryFindVisibleCombatTargetAsync(
                creatureIds, excludedGuids, CombatLocalTargetSearchTimeout, cancellationToken);
            if (IsObjectiveComplete(objective))
                return null;
            if (nearby is not null)
                return nearby;
            approached = await TryApproachVisibleCombatTargetAsync(creatureIds, excludedGuids, cancellationToken);
            if (IsObjectiveComplete(objective))
                return null;
            if (approached is not null)
                return approached;
        }
    }

    /// <summary>
    /// A wanted creature that is streamed in but outside the 40 m fight radius (the Dominion Ultrabot stood 55 m from its
    /// database spawn point): teleport next to its live position instead of waiting at the spawn.
    /// </summary>
    private async Task<TrackedEntity?> TryApproachVisibleCombatTargetAsync(
        IReadOnlySet<uint> creatureIds,
        IReadOnlySet<uint> excludedGuids,
        CancellationToken cancellationToken)
    {
        if (currentWorldId != questWorldId || worldLoadPending)
            return null;
        TrackedEntity? far = entities.FindNearestVisibleNonPlayer(creatureIds, lastPosition, 400f, excludedGuids, InCombatArea);
        if (far?.Position is not Vector3 position)
            return null;
        float distance = Vector3.Distance(lastPosition, position);
        log.Info($"combat target guid={far.Guid} creature {far.CreatureId} is visible {distance:0.0} m away; teleporting to it");
        await SendTeleportAsync(PacketCodec.TeleportCommand(position), false, cancellationToken);
        lastPosition = new Vector3(position.X + 2f, position.Y, position.Z + 2f);
        return FindVisibleCombatTarget(questWorldId, creatureIds, excludedGuids) ?? entities.Get(far.Guid) ?? far;
    }

    /// <summary>
    /// Delete looted items the current quest does not need so a turn in with item rewards has bag room.
    /// </summary>
    private async Task<int> FreeBagSpaceAsync(QuestDefinition definition, CancellationToken cancellationToken)
    {
        var keep = new HashSet<uint>(definition.Objectives.Select(objective => objective.Data));
        keep.UnionWith(definition.Entry.PushedItemIds.Where(id => id != 0u));
        if (definition.Entry.PrerequisiteItem != 0u)
            keep.Add(definition.Entry.PrerequisiteItem);
        return await FreeLootedJunkAsync(keep, $"quest {definition.Id}", cancellationToken);
    }

    /// <summary>
    /// Deletes looted items that are not in <paramref name="keep"/>, as a player clears quest loot from full bags.
    /// </summary>
    private async Task<int> FreeLootedJunkAsync(IReadOnlySet<uint> keep, string why, CancellationToken cancellationToken)
    {
        InventoryItemInfo[] junk = inventory.Values
            .Where(item => item.Location == InventoryLocation.Inventory && lootedItemIds.Contains(item.ItemId) && !keep.Contains(item.ItemId))
            .ToArray();
        foreach (InventoryItemInfo item in junk)
        {
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientItemDelete,
                PacketCodec.ClientItemDelete(item.Location, item.BagIndex), cancellationToken);
        }
        log.Info($"bag space: requested deletion of {junk.Length} looted item(s) not needed by {why}");
        DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (junk.Any(item => inventory.ContainsKey(item.Guid)) && DateTimeOffset.UtcNow < deadline)
        {
            if (await TryReadWorldPacketAsync(deadline - DateTimeOffset.UtcNow, cancellationToken) is null)
                break;
        }
        return junk.Count(item => !inventory.ContainsKey(item.Guid));
    }

    private Spawn? ChooseNextCombatSpawn(
        IReadOnlyList<Spawn> candidates,
        IReadOnlyDictionary<uint, int> spawnVisits)
    {
        int? cycle = candidates
            .Select(spawn => spawnVisits.GetValueOrDefault(spawn.Id))
            .Where(visits => visits < MaximumCombatSpawnVisits)
            .Cast<int?>()
            .Min();
        if (cycle is null)
            return null;

        return candidates
            .Where(spawn => spawnVisits.GetValueOrDefault(spawn.Id) == cycle.Value)
            .OrderBy(spawn => spawn.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
            .ThenBy(spawn => spawn.Id)
            .First();
    }

    private Task<bool> FightUntilProgressAsync(int objectiveIndex, uint before, uint targetGuid, CancellationToken cancellationToken)
        => FightUntilCombatGoalAsync(
            objectiveIndex,
            before,
            targetGuid,
            lootCorpse: true,
            cancellationToken: cancellationToken);

    private Task<bool> FightUntilDeathAsync(
        uint targetGuid,
        CancellationToken cancellationToken,
        bool lootCorpse = true)
        => FightUntilCombatGoalAsync(null, 0u, targetGuid, lootCorpse, cancellationToken);

    // a chain of kills with no pause (By Leaps and Bounds: three loftite girroks at 1,100 damage each) killed the bot on the
    // third; out of combat a player regenerates quickly, so wait for the health to come back before the next fight
    private const double FightHealthFloor = 0.6d;
    private const double FightHealthTarget = 0.9d;

    private async Task RecoverHealthBeforeFightAsync(CancellationToken cancellationToken)
    {
        if (activeHoldoutEventId is not null || playerInCombat || botDied)
            return;
        if (currentHealth is not uint health || currentMaxHealth is not uint max || max == 0u || health >= max * FightHealthFloor)
            return;
        log.Info($"health {health}/{max} before the next fight; waiting out of combat for it to recover");
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(25);
        while (currentHealth is uint now && now < max * FightHealthTarget && !playerInCombat && !botDied)
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                break;
        }
        log.Info($"health {currentHealth}/{max} after the recovery wait");
    }

    private async Task<bool> FightUntilCombatGoalAsync(
        int? objectiveIndex,
        uint before,
        uint targetGuid,
        bool lootCorpse,
        CancellationToken cancellationToken)
    {
        await RecoverHealthBeforeFightAsync(cancellationToken);
        BeginCombatEvidence(targetGuid);
        bool targetFinished = false;
        try
        {
            ushort[] slots = GetCombatSlots();
            int slotCursor = 0;
            CombatFacing facing = await FaceCombatTargetAsync(targetGuid, true, cancellationToken);

            while (true)
            {
                if (activeHoldoutEventId is not null && holdoutResult is not null)
                    return false;
                if (CombatGoalProgressed(objectiveIndex, before))
                {
                    targetFinished = true;
                    return true;
                }
                if (combatTargetDestroyed)
                {
                    targetFinished = combatTargetConfirmedDead;
                    return true;
                }

                TimeSpan globalWait = nextCombatCastAllowedAt - DateTimeOffset.UtcNow;
                if (globalWait > TimeSpan.Zero)
                {
                    facing = await DrainCombatPacketsAsync(globalWait, targetGuid, facing, cancellationToken);
                    continue;
                }

                if (ShouldRefreshCombatFacing(targetGuid, facing))
                    facing = await FaceCombatTargetAsync(targetGuid, false, cancellationToken);

                ushort? availableSlot = null;
                DateTimeOffset now = DateTimeOffset.UtcNow;
                for (int i = 0; i < slots.Length; i++)
                {
                    ushort candidate = slots[slotCursor];
                    slotCursor = (slotCursor + 1) % slots.Length;
                    if (!combatSlotBusyUntil.TryGetValue(candidate, out DateTimeOffset until) || until <= now)
                    {
                        availableSlot = candidate;
                        combatSlotBusyUntil.Remove(candidate);
                        break;
                    }
                }

                if (availableSlot is null)
                {
                    log.Info("combat: every discovered slot is busy; waiting 0.5 s");
                    facing = await DrainCombatPacketsAsync(AllSlotsBusyWait, targetGuid, facing, cancellationToken);
                    continue;
                }

                ushort bagIndex = availableSlot.Value;
                uint clientUniqueId = spellCastId++;
                pendingCombatClientId = clientUniqueId;
                pendingCombatCastResult = null;
                pendingCombatServerId = null;
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientCastSpell,
                    PacketCodec.ClientCastSpell(clientUniqueId, bagIndex, targetGuid), cancellationToken);
                nextCombatCastAllowedAt = DateTimeOffset.UtcNow + MinimumCombatCastInterval;

                CombatCastTransition transition;
                SpellCastResult? castResult;
                try
                {
                    (transition, facing) = await WaitForCombatCastTransitionAsync(
                        objectiveIndex, before, targetGuid, facing, cancellationToken);
                    castResult = pendingCombatCastResult;
                }
                finally
                {
                    pendingCombatClientId = null;
                    pendingCombatCastResult = null;
                    pendingCombatServerId = null;
                }

                if (transition == CombatCastTransition.Progressed)
                {
                    targetFinished = true;
                    return true;
                }
                if (transition == CombatCastTransition.TargetDestroyed)
                {
                    targetFinished = combatTargetConfirmedDead;
                    return true;
                }
                if (transition == CombatCastTransition.Started)
                {
                    // the cast went through: the next one is allowed when the global cooldown ends (a second cast of the same
                    // slot inside it only returns SpellGlobalCooldown and used to cost the bot another second)
                    DateTimeOffset afterGlobalCooldown = DateTimeOffset.UtcNow + GlobalCooldownWait;
                    if (afterGlobalCooldown > nextCombatCastAllowedAt)
                        nextCombatCastAllowedAt = afterGlobalCooldown;
                    log.Info($"combat slot {bagIndex}: own spell started; next cast after the global cooldown");
                    continue;
                }
                if (transition == CombatCastTransition.NoResponse || castResult is null)
                {
                    log.Info($"combat slot {bagIndex}: no cast result or own spell start within 1.2 s; trying next slot");
                    continue;
                }

                if (castResult.Result == CastResult.CCStun)
                    consecutiveStunResults++;
                else
                    consecutiveStunResults = 0;
                if (consecutiveStunResults >= StunEscapeThreshold)
                {
                    // chain-stunned (standing in a boss telegraph: the Boulderback's Loftite Shards stun every 2.5 s): a player
                    // walks out; the bot drops this target and the caller moves to another spawn (Irradiated Creatures, 582 CCStun)
                    consecutiveStunResults = 0;
                    throw new CombatTargetUnavailableException($"stunned for {StunEscapeThreshold} casts in a row; leaving the spot");
                }
                switch (castResult.Result)
                {
                    case CastResult.SpellCooldown:
                    {
                        // the ability's own cooldown from Spell4 (58591 is 20 s; retrying it every 4 s wasted casts), at least 4 s
                        uint cooldownMs = RequireGameData().GetSpellCooldownMs(castResult.Spell4Id);
                        TimeSpan busy = cooldownMs > SlotCooldownWait.TotalMilliseconds ? TimeSpan.FromMilliseconds(cooldownMs) : SlotCooldownWait;
                        combatSlotBusyUntil[bagIndex] = DateTimeOffset.UtcNow + busy;
                        log.Info($"combat slot {bagIndex}: SpellCooldown; busy for {busy.TotalSeconds:0} s, trying next slot");
                        break;
                    }
                    case CastResult.SpellGlobalCooldown:
                        DateTimeOffset globalCooldownUntil = DateTimeOffset.UtcNow + GlobalCooldownWait;
                        if (globalCooldownUntil > nextCombatCastAllowedAt)
                            nextCombatCastAllowedAt = globalCooldownUntil;
                        log.Info($"combat slot {bagIndex}: SpellGlobalCooldown; waiting {GlobalCooldownWait.TotalSeconds:0.0} s before any cast");
                        break;
                    case CastResult.PrereqCasterCast:
                        combatSlotBusyUntil[bagIndex] = DateTimeOffset.UtcNow.AddSeconds(15);
                        log.Info($"combat slot {bagIndex}: PrereqCasterCast; unusable for 15 s");
                        break;
                    default:
                        if (IsInvalidCombatTargetResult(castResult.Result))
                        {
                            log.Info($"combat slot {bagIndex}: {castResult.Result}; reselecting target {targetGuid}");
                            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                                PacketCodec.ClientEntitySelect(targetGuid), cancellationToken);
                            facing = await FaceCombatTargetAsync(targetGuid, false, cancellationToken);
                        }
                        else
                        {
                            log.Info($"combat slot {bagIndex}: {castResult.Result}; trying next slot immediately");
                        }
                        break;
                }
            }
        }
        catch (CombatTargetUnavailableException exception)
        {
            log.Info($"combat target {targetGuid} vanished; acquiring another target ({exception.Message})");
            return false;
        }
        finally
        {
            if (targetFinished && lootCorpse)
                combatLoot.AddRange(await LootCorpseAsync(targetGuid, cancellationToken));
            FinishCombatEvidence(objectiveIndex, targetGuid, targetFinished);
        }
    }

    private bool ShouldRefreshCombatFacing(uint targetGuid, CombatFacing facing)
    {
        if (DateTimeOffset.UtcNow - facing.SentAt >= CombatFacingRefreshInterval)
            return true;

        return entities.Get(targetGuid)?.Position is Vector3 targetPosition
            && Vector3.DistanceSquared(targetPosition, facing.TargetPosition)
                > CombatTargetMovementThreshold * CombatTargetMovementThreshold;
    }

    private async Task<CombatFacing> DrainCombatPacketsAsync(
        TimeSpan duration,
        uint targetGuid,
        CombatFacing facing,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + duration;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (combatTargetDestroyed)
                break;
            if (ShouldRefreshCombatFacing(targetGuid, facing))
                facing = await FaceCombatTargetAsync(targetGuid, false, cancellationToken);

            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;
            TimeSpan untilFacingRefresh = facing.SentAt + CombatFacingRefreshInterval - DateTimeOffset.UtcNow;
            TimeSpan readTimeout = untilFacingRefresh > TimeSpan.Zero && untilFacingRefresh < remaining
                ? untilFacingRefresh
                : remaining;
            _ = await TryReadWorldPacketAsync(readTimeout, cancellationToken);
        }

        return facing;
    }

    private async Task<CombatFacing> FaceCombatTargetAsync(
        uint targetGuid,
        bool logFacing,
        CancellationToken cancellationToken)
    {
        if (worldLoadPending)
            throw new CombatTargetUnavailableException($"world load pending while facing target {targetGuid}");

        TrackedEntity target = entities.Get(targetGuid)
            ?? throw new CombatTargetUnavailableException($"target {targetGuid} is no longer tracked");
        Vector3 targetPosition = target.Position
            ?? throw new CombatTargetUnavailableException($"target {targetGuid} has no tracked position");

        var awayFromTarget = new Vector2(lastPosition.X - targetPosition.X, lastPosition.Z - targetPosition.Z);
        if (awayFromTarget.LengthSquared() < 0.0001f)
            awayFromTarget = Vector2.UnitX;
        else
            awayFromTarget = Vector2.Normalize(awayFromTarget);

        var playerPosition = new Vector3(
            targetPosition.X + awayFromTarget.X * CombatStandOffDistance,
            targetPosition.Y,
            targetPosition.Z + awayFromTarget.Y * CombatStandOffDistance);
        Vector3 direction = Vector3.Normalize(targetPosition - playerPosition);
        float pitch = MathF.Asin(-direction.Y);
        float yaw = MathF.Atan2(-direction.X, -direction.Z);
        var rotation = new Vector3(yaw, pitch, 0f);

        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityCommand,
            PacketCodec.ClientEntityCommand(playerPosition, rotation), cancellationToken);
        lastPosition = playerPosition;

        if (logFacing)
        {
            float distance = Vector3.Distance(playerPosition, targetPosition);
            log.Info(FormattableString.Invariant(
                $"facing yaw={yaw:R} target guid={targetGuid} at {distance:0.0} m"));
        }

        return new CombatFacing(targetPosition, DateTimeOffset.UtcNow);
    }

    private ushort[] GetCombatSlots()
    {
        if (!activeActionSetKnown || !actionSetCombatSlots.TryGetValue(activeActionSet, out ushort[]? slots))
        {
            if (quest is not null && combatSlotDiscoveryLoggedQuests.Add(quest.Id))
                log.Info($"quest {quest.Id} combat slot discovery unavailable; falling back to slots [0,1,2,3,4,5,6,7]");
            return Enumerable.Range(0, 8).Select(index => (ushort)index).ToArray();
        }
        if (slots.Length == 0)
            throw new BotFailureException($"active action set {activeActionSet} contains no spell shortcuts");
        return slots;
    }

    private async Task<(CombatCastTransition Transition, CombatFacing Facing)> WaitForCombatCastTransitionAsync(
        int? objectiveIndex,
        uint before,
        uint targetGuid,
        CombatFacing facing,
        CancellationToken cancellationToken)
    {
        DateTimeOffset responseDeadline = DateTimeOffset.UtcNow.AddSeconds(1.2);
        uint? startedServerId = null;
        DateTimeOffset? finishDeadline = null;
        while (true)
        {
            if (CombatGoalProgressed(objectiveIndex, before))
                return (CombatCastTransition.Progressed, facing);
            if (combatTargetDestroyed)
                return (CombatCastTransition.TargetDestroyed, facing);
            if (pendingCombatCastResult is { Result: not CastResult.Ok })
                return (CombatCastTransition.CastResult, facing);

            if (ShouldRefreshCombatFacing(targetGuid, facing))
                facing = await FaceCombatTargetAsync(targetGuid, false, cancellationToken);

            if (pendingCombatServerId is uint serverId && startedServerId != serverId)
            {
                startedServerId = serverId;
                finishDeadline = DateTimeOffset.UtcNow + CombatCastFinishWait;
            }
            if (startedServerId is uint started && finishedCombatSpellIds.Remove(started))
                return (CombatCastTransition.Started, facing);

            DateTimeOffset deadline = finishDeadline ?? responseDeadline;
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return (startedServerId is null ? CombatCastTransition.NoResponse : CombatCastTransition.Started, facing);

            TimeSpan untilFacingRefresh = facing.SentAt + CombatFacingRefreshInterval - DateTimeOffset.UtcNow;
            TimeSpan readTimeout = untilFacingRefresh > TimeSpan.Zero && untilFacingRefresh < remaining
                ? untilFacingRefresh
                : remaining;
            if (await TryReadWorldPacketAsync(readTimeout, cancellationToken) is null
                && !ShouldRefreshCombatFacing(targetGuid, facing))
            {
                return (startedServerId is null ? CombatCastTransition.NoResponse : CombatCastTransition.Started, facing);
            }
        }
    }

    private static bool IsInvalidCombatTargetResult(CastResult result)
        => result is CastResult.CasterMustHaveTarget
            or CastResult.TargetGroup
            or CastResult.TargetUnknown
            or CastResult.NoTarget
            or CastResult.TargetIsNotUseable
            or CastResult.TargetRangeMin
            or CastResult.TargetRangeMax
            or CastResult.TargetRangeVertical
            or CastResult.CasterNeedsLoS
            or CastResult.InvalidPosition;

    private void BeginCombatEvidence(uint targetGuid)
    {
        combatTargetGuid = targetGuid;
        combatStartedAt = DateTimeOffset.UtcNow;
        combatEnemyCasts = 0u;
        combatDamageTaken = 0u;
        combatFirstEnemyActionMs = null;
        combatTargetDestroyed = false;
        combatTargetConfirmedDead = false;
        combatLoot.Clear();
    }

    private bool CombatGoalProgressed(int? objectiveIndex, uint before)
        => objectiveIndex is int index && GetRawProgress(index) > before;

    private async Task<IReadOnlyList<LootEvidence>> LootCorpseAsync(
        uint corpseGuid,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + CorpseLootTimeout;
        LootNotification? notification;
        while (!lootNotifications.TryGetValue(corpseGuid, out notification))
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return [];
        }

        LootItemInfo[] requested = notification.Items
            .Where(item => item.CanLoot && !item.Delivered)
            .ToArray();
        if (requested.Length == 0)
        {
            lootNotifications.Remove(corpseGuid);
            return [];
        }

        Dictionary<uint, uint> countsBefore = requested
            .Where(item => item.Type == LootItemType.StaticItem)
            .Select(item => item.StaticId)
            .Distinct()
            .ToDictionary(itemId => itemId, GetInventoryItemCount);

        // full bags lose the loot (ServerItemError ItemInventoryFull): the start item of Carcass Discovery never arrived from
        // nine Grimstone Mangler corpses; a player would clear junk first
        if ((bagsReportedFull || inventory.Values.Count(item => item.Location == InventoryLocation.Inventory) >= LootBagSlotsToKeepFree)
            && currentQuestReport is not null && questDefinitions.TryGetValue(currentQuestReport.QuestId, out QuestDefinition? lootQuest))
        {
            bagsReportedFull = false;
            int freed = await FreeBagSpaceAsync(lootQuest, cancellationToken);
            if (freed > 0)
                log.Info($"loot corpse {corpseGuid}: freed {freed} bag slot(s) before looting");
        }

        foreach (LootItemInfo lootItem in requested)
        {
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientLootItem,
                PacketCodec.ClientLootItem(corpseGuid, lootItem.LootUnitId), cancellationToken);
        }
        log.Info($"loot corpse {corpseGuid}: requested {requested.Length} item(s) with ClientLootItem");

        while (true)
        {
            Dictionary<uint, LootItemInfo>? grants = lootGrants.GetValueOrDefault(corpseGuid);
            bool everyGrantReceived = requested.All(item => grants?.ContainsKey(item.LootUnitId) == true);
            LootItemInfo[] grantedItems = requested
                .Where(item => grants?.ContainsKey(item.LootUnitId) == true && item.Type == LootItemType.StaticItem)
                .ToArray();
            bool inventoryReflected = grantedItems
                .GroupBy(item => item.StaticId)
                .All(group => GetInventoryItemCount(group.Key) >= countsBefore.GetValueOrDefault(group.Key)
                    + (uint)group.Aggregate(0ul, (total, item) => total + item.Amount));
            if (everyGrantReceived && inventoryReflected)
                break;

            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                break;
        }

        Dictionary<uint, LootItemInfo>? receivedGrants = lootGrants.GetValueOrDefault(corpseGuid);
        var evidence = new List<LootEvidence>();
        foreach (IGrouping<uint, LootItemInfo> group in requested
            .Where(item => item.Type == LootItemType.StaticItem
                && receivedGrants?.ContainsKey(item.LootUnitId) == true)
            .GroupBy(item => item.StaticId))
        {
            uint after = GetInventoryItemCount(group.Key);
            uint before = countsBefore.GetValueOrDefault(group.Key);
            uint inventoryDelta = after > before ? after - before : 0u;
            uint granted = (uint)group.Aggregate(0ul, (total, item) => total + item.Amount);
            uint received = Math.Min(inventoryDelta, granted);
            if (received > 0u)
                evidence.Add(new LootEvidence { ItemId = group.Key, Count = received });
        }

        lootNotifications.Remove(corpseGuid);
        lootGrants.Remove(corpseGuid);
        return evidence;
    }

    private void FinishCombatEvidence(int? objectiveIndex, uint targetGuid, bool targetFinished)
    {
        KillEvidence? evidence = null;
        if (targetFinished)
        {
            evidence = new KillEvidence
            {
                TargetGuid = targetGuid,
                FoughtBack = combatEnemyCasts > 0u || combatDamageTaken > 0u,
                EnemyCasts = combatEnemyCasts,
                DamageTaken = combatDamageTaken,
                FirstEnemyActionMs = combatFirstEnemyActionMs
            };
            evidence.Loot.AddRange(combatLoot);
        }
        if (targetFinished && objectiveIndex is int index)
        {
            ObjectiveReport objective = RequireObjectiveReport(index);
            objective.Kills.Add(evidence!);
            objective.FoughtBack = objective.Kills.All(kill => kill.FoughtBack);
            objective.EnemyCasts = objective.Kills.Aggregate(0u, (total, kill) => total + kill.EnemyCasts);
            objective.DamageTaken = objective.Kills.Aggregate(0u, (total, kill) => total + kill.DamageTaken);
            objective.FirstEnemyActionMs = objective.Kills.Where(kill => kill.FirstEnemyActionMs.HasValue)
                .Select(kill => kill.FirstEnemyActionMs)
                .Min();
        }
        if (evidence is not null && currentMissionReport is not null)
        {
            currentMissionReport.Kills.Add(evidence);
            currentMissionReport.FoughtBack = currentMissionReport.Kills.All(kill => kill.FoughtBack);
            currentMissionReport.EnemyCasts = currentMissionReport.Kills.Aggregate(0u, (total, kill) => total + kill.EnemyCasts);
            currentMissionReport.DamageTaken = currentMissionReport.Kills.Aggregate(0u, (total, kill) => total + kill.DamageTaken);
            currentMissionReport.FirstEnemyActionMs = currentMissionReport.Kills
                .Where(kill => kill.FirstEnemyActionMs.HasValue)
                .Select(kill => kill.FirstEnemyActionMs)
                .Min();
        }
        combatTargetGuid = null;
        combatTargetDestroyed = false;
        combatTargetConfirmedDead = false;
        pendingCombatClientId = null;
        pendingCombatCastResult = null;
        pendingCombatServerId = null;
    }

    /// <summary>
    /// An EnterArea objective without a map indicator was a map trigger volume the server does not have. The server completes
    /// it near a creature the text names ("Search for Botsmith Redfield inside Halsen's Claim"), at a location the text links,
    /// or in the world zone its Data names (Corrupted Reconnaissance: zone 4350); the bot goes to the first of those it can
    /// resolve in the quest's world. The Data is not always a zone of this world (Beyond the Realm of Medicine: 2026 lies in
    /// another world), which is why the text comes first.
    /// </summary>
    private async Task RunEnterZoneAreaObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        uint before = GetRawProgress(objective.Index);
        BotGameData data = RequireGameData();
        string reached;
        IReadOnlyList<uint> textCreatureIds = data.GetCreatureIdsFromText(objective.Text);
        Spawn? textSpawn = null;
        if (textCreatureIds.Count > 0)
        {
            IReadOnlyList<Spawn> textSpawns = await BotDatabase.LoadSpawnsAsync(options.Database, textCreatureIds, cancellationToken);
            textSpawn = textSpawns
                .Where(spawn => spawn.WorldId == questWorldId)
                .OrderBy(spawn => spawn.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
                .FirstOrDefault();
        }
        if (textSpawn is not null)
        {
            reached = $"creature {textSpawn.CreatureId} named in the text (spawn {textSpawn.Id} at {textSpawn.X:0},{textSpawn.Y:0},{textSpawn.Z:0})";
            log.Info($"objective {objective.Index}: EnterArea {objective.Data} via {reached}");
            await TeleportToSpawnLocationAsync(textSpawn, cancellationToken);
        }
        else
        {
            WorldLocation2Entry? location = data.GetLocationIdsFromText(objective.Text)
                .Select(id => data.GetWorldLocation(id))
                .FirstOrDefault(candidate => candidate.WorldId == questWorldId);
            reached = location is null ? "" : $"text location WorldLocation2 {location.Id}";
            if (location is null)
            {
                location = data.LocationInZone(objective.Data, questWorldId, lastPosition)
                    ?? throw new BotFailureException($"EnterArea objective has no indicator, its text names no creature or location of world {questWorldId}, and no WorldLocation2 lies in zone {objective.Data} of that world");
                reached = $"zone {objective.Data} WorldLocation2 {location.Id}";
            }
            log.Info($"objective {objective.Index}: EnterArea {objective.Data} via {reached} ({location.Position0:0},{location.Position1:0},{location.Position2:0})");
            await TeleportToLocationAsync(location, 0f, cancellationToken);
        }
        if (!await TryWaitForObjectiveProgressAsync(objective.Index, before, TimeSpan.FromSeconds(15), cancellationToken))
            throw new BotFailureException($"EnterArea {objective.Data} produced no progress 15 s after reaching {reached}");
    }

    /// <summary>
    /// CompleteEvent: the server holds the objective's place for its EventFallbackTime (20 s) when the public event cannot run
    /// here, so stand at the objective's indicator; a running event would need the event itself to be won.
    /// </summary>
    private async Task RunCompleteEventObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        uint before = GetRawProgress(objective.Index);
        if (objective.WorldLocationId == 0u)
            throw new BotFailureException($"CompleteEvent objective for public event {objective.Data} has no map indicator to stand at");
        WorldLocation2Entry location = RequireGameData().GetWorldLocation(objective.WorldLocationId);
        await TeleportToLocationAsync(location, 0f, cancellationToken);
        log.Info($"objective {objective.Index}: CompleteEvent {objective.Data}: standing at indicator {location.Id} for the server's event fallback");
        if (!await TryWaitForObjectiveProgressAsync(objective.Index, before, TimeSpan.FromSeconds(45), cancellationToken))
            throw new BotFailureException($"CompleteEvent {objective.Data} produced no progress after 45 s at indicator {location.Id}; the event may be running (unsupported) or the fallback did not fire");
    }

    private async Task RunLocationObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        WorldLocation2Entry location = RequireGameData().GetWorldLocation(objective.WorldLocationId);
        uint before = GetRawProgress(objective.Index);
        await TeleportToLocationAsync(location, 0f, cancellationToken);
        // "Right-click the $(creature=73741) ... to continue to the Protostar Model Skyplot" (10525, EnterZone 4965): the
        // zone is entered through a portal object, so activate the creature the text names once it is streamed in
        // only zone transitions go through a portal object; an EnterArea text may name a creature for other reasons
        // ("Jump up to the $p(creature=73499) platform" names the boss standing on it)
        uint portalCreatureId = objective.Type == QuestObjectiveType.EnterZone ? BotGameData.CreatureFromText(objective.Text) : 0u;
        if (portalCreatureId != 0u && !IsObjectiveComplete(objective))
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + EntityStreamTimeout;
            TrackedEntity? portal = entities.FindCreature(portalCreatureId);
            while (portal is null && DateTimeOffset.UtcNow < deadline)
            {
                if (await TryReadWorldPacketAsync(deadline - DateTimeOffset.UtcNow, cancellationToken) is null)
                    break;
                portal = entities.FindCreature(portalCreatureId);
            }
            if (portal is not null)
            {
                log.Info($"location objective {objective.Index}: activating portal creature {portalCreatureId} guid {portal.Guid}");
                // portals have no activation spell: the plain ClientActivateUnit reaches the server's OnActivate directly
                // one plain activation only: the server teleports the player (a map transfer, the portal is gone from view),
                // and a follow-up ActivateUnitCast for the vanished unit made the server drop the session (13:41 run)
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnit,
                    PacketCodec.ClientActivateUnit(portal.Guid), cancellationToken);
            }
            else
                log.Info($"location objective {objective.Index}: portal creature {portalCreatureId} not visible within {EntityStreamTimeout.TotalSeconds:0} s");
        }
        await WaitForObjectiveProgressAsync(objective.Index, before, cancellationToken);
    }

    private async Task TeleportToLocationAsync(
        WorldLocation2Entry location,
        float offsetX,
        CancellationToken cancellationToken)
    {
        bool includeWorld = currentWorldId != location.WorldId;
        if (includeWorld)
            entities.Clear();
        await SendTeleportAsync(PacketCodec.TeleportCommand(location, includeWorld, offsetX), includeWorld, cancellationToken);
        if (includeWorld)
            await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerPlayerEnteredWorld, cancellationToken);
        lastPosition = new Vector3(location.Position0 + offsetX, location.Position1, location.Position2);
    }

    private async Task TeleportToSpawnLocationAsync(Spawn spawn, CancellationToken cancellationToken)
    {
        bool includeWorld = currentWorldId != spawn.WorldId;
        if (includeWorld)
            entities.Clear();
        await SendTeleportAsync(PacketCodec.TeleportCommand(spawn, includeWorld), includeWorld, cancellationToken);
        if (includeWorld)
            await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerPlayerEnteredWorld, cancellationToken);
        lastPosition = new Vector3(spawn.X + 2f, spawn.Y, spawn.Z + 2f);
    }

    private async Task<string> WaitForAchievedAsync(CancellationToken cancellationToken)
    {
        await WaitForQuestStateAsync(QuestState.Achieved, cancellationToken);
        return "server reported state 2 (Achieved)";
    }

    private async Task<string> TurnInQuestAsync(CancellationToken cancellationToken)
    {
        QuestDefinition definition = RequireQuest();
        // the receiver may live in another world (Bon Voyage: accepted on the arkship, handed to Scout Frostfield in Northern
        // Wilds): follow the receiver spawns, whatever world they are in
        if (definition.ReceiverCreatureIds.Count > 0
            && !spawns.Any(spawn => spawn.WorldId == questWorldId && definition.ReceiverCreatureIds.Contains(spawn.CreatureId)))
        {
            Spawn? receiverWorldAnchor = spawns
                .Where(spawn => definition.ReceiverCreatureIds.Contains(spawn.CreatureId))
                .OrderBy(spawn => spawn.WorldId == currentWorldId ? 0 : 1)
                .ThenBy(spawn => spawn.Id)
                .FirstOrDefault();
            if (receiverWorldAnchor is not null)
            {
                log.Info($"quest {definition.Id}: receivers spawn in world {receiverWorldAnchor.WorldId}, not {questWorldId}; following them");
                questWorldId = receiverWorldAnchor.WorldId;
            }
        }
        if (definition.ReceiverCreatureIds.Count == 0)
        {
            // no receiver creature (The Face of the Enemy 10518, flags 0x804000): the client turns these in over the
            // communicator, ClientQuestComplete with IsCommunicatorMsg set
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestComplete,
                PacketCodec.ClientQuestComplete(definition.Id, communicator: true), cancellationToken);
            if (await TryWaitForQuestStateAsync(QuestState.Completed, QuestNpcConfirmationTimeout, cancellationToken))
                return "completed over the communicator (no receiver creature); state 3";
            throw new BotFailureException(
                $"communicator turn-in (no receiver creature) produced no Completed state within {QuestNpcConfirmationTimeout.TotalSeconds:0} s; quest flags 0x{definition.Entry.Flags:X}; last generic error={lastGenericError?.ToString() ?? "none"}");
        }
        if (definition.Entry.PrerequisiteItem != 0u
            && !spawns.Any(spawn => spawn.WorldId == questWorldId && definition.ReceiverCreatureIds.Contains(spawn.CreatureId)))
        {
            Spawn? receiverAnchor = spawns
                .Where(spawn => definition.ReceiverCreatureIds.Contains(spawn.CreatureId))
                .OrderBy(spawn => spawn.WorldId == currentWorldId ? 0 : 1)
                .ThenBy(spawn => spawn.Id)
                .FirstOrDefault();
            if (receiverAnchor is not null)
                questWorldId = receiverAnchor.WorldId;
        }
        QuestNpcUseResult? receiver = await TryEveryQuestNpcAsync(
            "receiver",
            definition.ReceiverCreatureIds,
            questWorldId,
            async (entity, token) =>
            {
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
                    PacketCodec.ClientEntityInteract(entity.Guid, InteractionType.TalkTo), token);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    lastGenericError = null;
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestComplete,
                        PacketCodec.ClientQuestComplete(definition.Id), token);
                    if (await TryWaitForQuestStateAsync(QuestState.Completed, QuestNpcConfirmationTimeout, token))
                        return true;
                    // QuestComplete returns without a packet when the reward items have no bag room: drop looted junk and retry
                    if (lastGenericError != GenericError.ItemInventoryFull || attempt > 0)
                        return false;
                    int freed = await FreeBagSpaceAsync(definition, token);
                    questNpcAttemptDetails.Add($"turn in refused with ItemInventoryFull; deleted {freed} looted item(s) and retried");
                    if (freed == 0)
                        return false;
                }
                return false;
            },
            cancellationToken);
        if (receiver is null)
        {
            throw new BotFailureException(
                $"no receiver completed the quest; {FormatQuestNpcAttempts()}; {FormatNearbyTrackedCreatures()}");
        }
        return $"completed with receiver creature {receiver.Entity.CreatureId} guid {receiver.Entity.Guid}; state 3; {receiver.Attempts}";
    }

    private async Task<TrackedEntity?> TeleportAndTryFindAsync(
        Spawn spawn,
        TimeSpan entityTimeout,
        CancellationToken cancellationToken,
        IReadOnlySet<uint>? excludedGuids = null)
    {
        bool includeWorld = currentWorldId != spawn.WorldId;
        if (includeWorld)
            entities.Clear();
        Vector3 destination = new(spawn.X + 2f, spawn.Y, spawn.Z + 2f);
        await SendTeleportAsync(PacketCodec.TeleportCommand(spawn, includeWorld), includeWorld, cancellationToken);
        lastPosition = destination;

        DateTimeOffset deadline = DateTimeOffset.UtcNow + entityTimeout;
        while (true)
        {
            TrackedEntity? existing = currentWorldId == spawn.WorldId && !worldLoadPending
                ? entities.FindCreature(spawn.CreatureId, spawn, excludedGuids)
                : null;
            if (existing is not null)
                return existing;

            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return null;
        }
    }

    private async Task<TrackedEntity?> TeleportAndTryFindWithRetryAsync(
        Spawn spawn,
        CancellationToken cancellationToken,
        IReadOnlySet<uint>? excludedGuids = null)
    {
        TrackedEntity? entity = await TeleportAndTryFindAsync(spawn, EntityStreamTimeout, cancellationToken, excludedGuids);
        if (entity is not null)
            return entity;

        // The spawn stream after a teleport can lag behind PlayerEnteredWorld. Re-send the same
        // command (which intentionally lands 2 m off the spawn) once and allow a second full window.
        return await TeleportAndTryFindAsync(spawn, EntityStreamTimeout, cancellationToken, excludedGuids);
    }

    private async Task<TrackedEntity?> TeleportAndTryFindCombatTargetAsync(
        Spawn spawn,
        IReadOnlySet<uint> creatureIds,
        IReadOnlySet<uint> excludedGuids,
        TimeSpan entityTimeout,
        CancellationToken cancellationToken)
    {
        bool includeWorld = currentWorldId != spawn.WorldId;
        if (includeWorld)
            entities.Clear();
        Vector3 destination = new(spawn.X + 2f, spawn.Y, spawn.Z + 2f);
        await SendTeleportAsync(PacketCodec.TeleportCommand(spawn, includeWorld), includeWorld, cancellationToken);
        lastPosition = destination;

        DateTimeOffset deadline = DateTimeOffset.UtcNow + entityTimeout;
        while (true)
        {
            TrackedEntity? target = FindVisibleCombatTarget(spawn.WorldId, creatureIds, excludedGuids);
            if (target is not null)
                return target;

            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return null;
        }
    }

    private async Task<TrackedEntity?> TryFindVisibleCombatTargetAsync(
        IReadOnlySet<uint> creatureIds,
        IReadOnlySet<uint> excludedGuids,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            TrackedEntity? target = FindVisibleCombatTarget(questWorldId, creatureIds, excludedGuids);
            if (target is not null)
                return target;

            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return null;
        }
    }

    private TrackedEntity? FindVisibleCombatTarget(
        ushort worldId,
        IReadOnlySet<uint> creatureIds,
        IReadOnlySet<uint> excludedGuids)
        => currentWorldId == worldId && !worldLoadPending
            ? entities.FindNearestVisibleNonPlayer(creatureIds, lastPosition, 40f, excludedGuids, InCombatArea)
            : null;

    private async Task WaitForLocalTeleportAsync(CancellationToken cancellationToken)
    {
        await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerLoadingScreen || teleportRefused, cancellationToken);
        if (teleportRefused)
            return;
        await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerMovementControl, cancellationToken);
    }

    /// <summary>
    /// Sends a teleport command and waits for the local teleport. A server that still has a pending teleport refuses the
    /// command in chat; the bot retries a few times instead of waiting for a loading screen that never comes.
    /// </summary>
    private async Task SendTeleportAsync(string command, bool includeWorld, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            teleportRefused = false;
            await SendCheatAsync(command, cancellationToken);
            if (includeWorld)
                return;
            await WaitForLocalTeleportAsync(cancellationToken);
            if (!teleportRefused)
                return;
            if (attempt >= TeleportRefusedRetries)
                throw new BotFailureException($"server refused the teleport {attempt + 1} times with a pending teleport (the previous teleport never completed on the server)");
            log.Info($"teleport refused (pending teleport); retry {attempt + 1}/{TeleportRefusedRetries} in 2 s");
            await DrainWorldPacketsAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    private async Task SendCheatAsync(string command, CancellationToken cancellationToken)
    {
        if (command.StartsWith("!quest", StringComparison.OrdinalIgnoreCase)
            || command.StartsWith("!path", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("quest and path commands are forbidden");
        report.Cheats.Add(command);
        currentQuestReport?.Cheats.Add(command);
        currentMissionReport?.Cheats.Add(command);
        // GM commands act on the CURRENT TARGET (GetTargetOrInvoker): with a creature still selected after a kill,
        // "!tele" and "!character level" fail with "Invalid target for this command!" — clear the target first.
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect, PacketCodec.ClientEntitySelect(0), cancellationToken);
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientChat,
            PacketCodec.ClientChat(command, chatMessageId++), cancellationToken);
    }

    private async Task WaitForQuestStateAsync(QuestState expected, CancellationToken cancellationToken)
    {
        while (questState != expected)
        {
            if (questState is QuestState.Botched or QuestState.Abandoned or QuestState.Ignored)
                throw new BotFailureException($"quest entered terminal state {questState} while waiting for {expected}");
            if (expected == QuestState.Accepted && questState is QuestState.Achieved or QuestState.Completed)
                return;
            if (expected == QuestState.Achieved && questState == QuestState.Completed)
                return;
            await ReadAndHandleWorldPacketAsync(cancellationToken);
        }
    }

    private async Task<bool> TryWaitForQuestStateAsync(
        QuestState expected,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            if (questState == expected
                || expected == QuestState.Accepted && questState is QuestState.Achieved or QuestState.Completed
                || expected == QuestState.Achieved && questState == QuestState.Completed)
                return true;
            if (questState is QuestState.Botched or QuestState.Abandoned or QuestState.Ignored)
                throw new BotFailureException($"quest entered terminal state {questState} while waiting for {expected}");

            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero
                || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return false;
        }
    }

    private async Task WaitForObjectiveProgressAsync(int index, uint before, CancellationToken cancellationToken)
    {
        while (GetRawProgress(index) <= before)
            await ReadAndHandleWorldPacketAsync(cancellationToken);
    }

    private async Task<bool> TryWaitForObjectiveProgressAsync(
        int index,
        uint before,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (GetRawProgress(index) <= before)
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return false;
            if (await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return false;
        }
        return true;
    }

    private async Task DrainWorldPacketsAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + duration;
        while (true)
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return;
            if (await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return;
        }
    }

    private async Task<ProtocolPacket> ReadWorldUntilAsync(
        Func<ProtocolPacket, bool> predicate,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            ProtocolPacket packet = await ReadAndHandleWorldPacketAsync(cancellationToken);
            if (predicate(packet))
                return packet;
        }
    }

    private async Task<ProtocolPacket> ReadAndHandleWorldPacketAsync(CancellationToken cancellationToken)
    {
        ProtocolPacket packet = await RequireWorld().ReadAsync(cancellationToken);
        await HandleWorldPacketAsync(packet, cancellationToken);
        return packet;
    }

    private async Task<ProtocolPacket?> TryReadWorldPacketAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            return await ReadAndHandleWorldPacketAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task HandleWorldPacketAsync(ProtocolPacket packet, CancellationToken cancellationToken)
    {
        try
        {
            RecordResurrectionPacketAfterAccept(packet.Opcode);
            switch (packet.Opcode)
            {
                case GameMessageOpcode.ServerChangeWorld:
                {
                    (currentWorldId, lastPosition) = PacketCodec.ParseChangeWorld(packet.Body);
                    worldLoadPending = true;
                    enteredWorldSent = false;
                    entities.Clear();
                    if (resurrectionRecoveryActive)
                    {
                        botDied = false;
                        resurrectionConfirmation = "ServerChangeWorld";
                    }
                    break;
                }
                case GameMessageOpcode.ServerMovementControl:
                {
                    controlGuid = PacketCodec.ParseMovementControl(packet.Body);
                    if (playerGuid == 0u)
                        playerGuid = controlGuid;
                    if (resurrectionRecoveryActive && controlGuid == playerGuid)
                    {
                        botDied = false;
                        resurrectionConfirmation ??= "ServerMovementControl returned to player guid";
                    }
                    break;
                }
                case GameMessageOpcode.ServerPlayerChanged:
                    playerGuid = PacketCodec.ParsePlayerChanged(packet.Body);
                    controlGuid = playerGuid;
                    // ServerPlayerCreate only comes on the FIRST map add (Player.SendPacketsAfterAddToMap: PreviousMap == null);
                    // after a teleport the server ends the add with ServerPlayerChanged, so that is the "world loaded" signal too.
                    if (worldLoadPending && !enteredWorldSent)
                    {
                        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEnteredWorld,
                            PacketCodec.ClientEnteredWorld(), cancellationToken);
                        enteredWorldSent = true;
                    }
                    break;
                case GameMessageOpcode.ServerPlayerCreate:
                    try
                    {
                        PlayerCreateInfo playerCreate = PacketCodec.ParsePlayerCreate(packet.Body);
                        activeActionSet = playerCreate.ActiveSpec;
                        activeActionSetKnown = true;
                        inventory.Clear();
                        foreach (InventoryItemInfo item in playerCreate.Inventory)
                            inventory[item.Guid] = item;
                        LogCombatSlotDiscovery();
                    }
                    catch (Exception exception)
                    {
                        log.Info($"combat slot discovery could not read ServerPlayerCreate active spec: {exception.Message}");
                    }
                    if (worldLoadPending && !enteredWorldSent)
                    {
                        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEnteredWorld,
                            PacketCodec.ClientEnteredWorld(), cancellationToken);
                        enteredWorldSent = true;
                    }
                    break;
                case GameMessageOpcode.ServerActionSet:
                {
                    ActionSetInfo actionSet = PacketCodec.ParseActionSet(packet.Body);
                    actionSetCombatSlots[actionSet.SpecIndex] = actionSet.Slots
                        .Where(slot => slot.BagIndex < 8
                            && slot.ShortcutType == ShortcutType.SpellbookItem
                            && slot.ObjectId != 0u)
                        .Select(slot => slot.BagIndex)
                        .Distinct()
                        .Order()
                        .ToArray();
                    LogCombatSlotDiscovery();
                    break;
                }
                case GameMessageOpcode.ServerPlayerEnteredWorld:
                    worldLoadPending = false;
                    break;
                case GameMessageOpcode.ServerEntityCreate:
                {
                    TrackedEntity entity = entities.ParseCreate(packet.Body);
                    string position = entity.Position is Vector3 value
                        ? FormattableString.Invariant($"({value.X:R},{value.Y:R},{value.Z:R})")
                        : "none";
                    log.Debug($"ENT guid={entity.Guid} EntityType={entity.Type} creatureId={entity.CreatureId?.ToString() ?? "none"} position={position}");
                    if (entity.Guid == playerGuid && entity.Type == EntityType.Player)
                    {
                        if (entity.Level is uint level)
                            currentLevel = level;
                        if (entity.Health is uint health)
                            currentHealth = health;
                        if (entity.MaxHealth is uint maxHealth)
                            currentMaxHealth = maxHealth;
                        if (resurrectionRecoveryActive)
                        {
                            botDied = false;
                            resurrectionConfirmation = "ServerEntityCreate for player guid";
                        }
                    }
                    break;
                }
                case GameMessageOpcode.ServerEntityCommand:
                {
                    (uint guid, Vector3? position) = entities.ParseCommands(packet.Body);
                    if (position is Vector3 value && (guid == playerGuid || guid == controlGuid))
                        lastPosition = value;
                    break;
                }
                case GameMessageOpcode.ServerEntityDestroy:
                {
                    uint destroyedGuid = PacketCodec.ParseEntityDestroy(packet.Body);
                    log.Debug($"ENT gone {destroyedGuid}");
                    if (combatTargetGuid == destroyedGuid)
                        combatTargetDestroyed = true;
                    entities.Remove(destroyedGuid);
                    if (!worldLoadPending && destroyedGuid == playerGuid)
                    {
                        botDied = true;
                        if (activeObjective is not null || combatTargetGuid is not null)
                            throw new BotFailureException("bot died");
                    }
                    break;
                }
                case GameMessageOpcode.ServerEntityDeathState:
                {
                    (uint unitId, bool dead) = PacketCodec.ParseEntityDeathState(packet.Body);
                    if (dead)
                    {
                        entities.MarkDead(unitId);
                        if (combatTargetGuid == unitId)
                        {
                            combatTargetDestroyed = true;
                            combatTargetConfirmedDead = true;
                        }
                    }
                    if (unitId == playerGuid && dead)
                    {
                        botDied = true;
                        // the offer for THIS death comes a moment later; accepting with the offer of an earlier death
                        // (same quest, second death) was ignored by the server and the recovery waited 60 s for nothing
                        resurrectionShow = null;
                        if (activeObjective is not null || combatTargetGuid is not null)
                            throw new BotFailureException("bot died");
                    }
                    if (unitId == playerGuid && !dead)
                    {
                        botDied = false;
                        if (resurrectionRecoveryActive)
                            resurrectionConfirmation = "ServerEntityDeathState false";
                    }
                    break;
                }
                case GameMessageOpcode.ServerResurrectionShow:
                    resurrectionShow = PacketCodec.ParseResurrectionShow(packet.Body);
                    resurrectionShowReceivedAt = DateTimeOffset.UtcNow;
                    break;
                case GameMessageOpcode.ServerSpellStart:
                {
                    SpellCastStart start = PacketCodec.ParseSpellStart(packet.Body);
                    if (combatTargetGuid == start.CasterId)
                    {
                        combatEnemyCasts++;
                        RecordEnemyAction();
                    }
                    if (combatTargetGuid is not null
                        && start.CasterId == playerGuid
                        && pendingCombatClientId is not null)
                    {
                        pendingCombatServerId = start.ServerUniqueId;
                    }
                    break;
                }
                case GameMessageOpcode.ServerSpellCastResult:
                {
                    SpellCastResult result = PacketCodec.ParseSpellCastResult(packet.Body);
                    uint? correlatedClientId = pendingActivationCastIds.Contains(result.ClientUniqueId)
                        ? result.ClientUniqueId
                        : result.ClientUniqueId == 0u && pendingActivationCastIds.Count == 1
                            ? pendingActivationCastIds.Single()
                            : null;
                    string correlation = correlatedClientId is uint correlated && correlated != result.ClientUniqueId
                        ? $", correlatedActivationClient={correlated}"
                        : "";
                    string detail = $"client={result.ClientUniqueId}{correlation}, spell={result.Spell4Id}, result={result.Result}(0x{(uint)result.Result:X4})";
                    log.Info($"spell cast result: {detail}");
                    if (correlatedClientId is uint activationClientId)
                    {
                        activationCastResults[activationClientId] = result;
                        objectiveCastResultDetails.Add(detail);
                    }
                    if (pendingCombatClientId is uint combatClientId
                        && (result.ClientUniqueId == combatClientId || result.ClientUniqueId == 0u))
                    {
                        pendingCombatCastResult = result;
                    }
                    break;
                }
                case GameMessageOpcode.ServerSpellFinish:
                {
                    uint serverUniqueId = PacketCodec.ParseSpellFinish(packet.Body);
                    if (respondedCsiSpellIds.Contains(serverUniqueId))
                        finishedSpellIds.Add(serverUniqueId);
                    if (pendingCombatServerId == serverUniqueId)
                        finishedCombatSpellIds.Add(serverUniqueId);
                    break;
                }
                case GameMessageOpcode.ServerEntityHealthUpdate:
                case GameMessageOpcode.ServerPlayerHealthUpdate:
                {
                    (uint unitId, uint health) = PacketCodec.ParseEntityHealthUpdate(packet.Body);
                    entities.UpdateHealth(unitId, health);
                    if (unitId == playerGuid)
                        RecordPlayerHealth(health);
                    break;
                }
                case GameMessageOpcode.ServerUnitEnteredCombat:
                {
                    UnitCombatState state = PacketCodec.ParseUnitCombatState(packet.Body);
                    if (state.UnitId == playerGuid)
                        playerInCombat = state.InCombat;
                    break;
                }
                case GameMessageOpcode.ServerEntityStatUpdateInteger:
                {
                    EntityStatInteger update = PacketCodec.ParseEntityStatInteger(packet.Body);
                    if (update.UnitId == playerGuid)
                    {
                        if (update.Stat == Stat.Level)
                            currentLevel = update.Value;
                        else if (update.Stat == Stat.Health)
                            RecordPlayerHealth(update.Value);
                    }
                    break;
                }
                case GameMessageOpcode.ServerEntityPropertiesUpdate:
                {
                    EntityPropertiesUpdate update = entities.ParsePropertiesUpdate(packet.Body);
                    if (update.UnitId == playerGuid && update.MaxHealth is uint maxHealth)
                        currentMaxHealth = maxHealth;
                    break;
                }
                case GameMessageOpcode.ServerLootNotify:
                {
                    LootNotification notification = PacketCodec.ParseLootNotify(packet.Body);
                    lootNotifications[notification.OwnerUnitId] = notification;
                    log.Info($"loot notify owner {notification.OwnerUnitId}: {notification.Items.Count} item(s)");
                    break;
                }
                case GameMessageOpcode.ServerLootGrant:
                {
                    LootGrant grant = PacketCodec.ParseLootGrant(packet.Body);
                    if (grant.LooterUnitId == playerGuid)
                    {
                        if (!lootGrants.TryGetValue(grant.OwnerUnitId, out Dictionary<uint, LootItemInfo>? grants))
                        {
                            grants = [];
                            lootGrants[grant.OwnerUnitId] = grants;
                        }
                        grants[grant.Item.LootUnitId] = grant.Item;
                        if (grant.Item.Type == LootItemType.StaticItem)
                            lootedItemIds.Add(grant.Item.StaticId);
                    }
                    break;
                }
                case GameMessageOpcode.ServerLootRemove:
                {
                    uint ownerUnitId = PacketCodec.ParseLootRemove(packet.Body);
                    lootNotifications.Remove(ownerUnitId);
                    log.Debug($"loot removed owner {ownerUnitId}");
                    break;
                }
                case GameMessageOpcode.ServerItemAdd:
                {
                    InventoryItemInfo item = PacketCodec.ParseItemAdd(packet.Body);
                    inventory[item.Guid] = item;
                    break;
                }
                case GameMessageOpcode.ServerItemStackCountUpdate:
                {
                    (ulong guid, uint stackCount) = PacketCodec.ParseItemStackCountUpdate(packet.Body);
                    if (inventory.TryGetValue(guid, out InventoryItemInfo? item))
                        inventory[guid] = item with { StackCount = stackCount };
                    break;
                }
                case GameMessageOpcode.ServerItemDelete:
                    inventory.Remove(PacketCodec.ParseItemDelete(packet.Body));
                    break;
                case GameMessageOpcode.ServerItemMove:
                    lastMovedItemGuid = PacketCodec.ParseItemMovedGuid(packet.Body);
                    lastSwappedOutItemGuid = null;
                    break;
                case GameMessageOpcode.ServerItemSwap:
                    (lastMovedItemGuid, lastSwappedOutItemGuid) = PacketCodec.ParseItemSwapGuids(packet.Body);
                    break;
                case GameMessageOpcode.ServerPathInitialise:
                {
                    PathInitialiseInfo initialise = PacketCodec.ParsePathInitialise(packet.Body);
                    activePath = initialise.ActivePath;
                    pathInitialiseReceived = true;
                    log.Info($"path initialise: active={activePath}, unlocked={initialise.UnlockedMask}");
                    break;
                }
                case GameMessageOpcode.ServerPathSetCurrentEpisode:
                    currentPathEpisodeId = PacketCodec.ParsePathCurrentEpisode(packet.Body);
                    log.Info($"path current episode: {currentPathEpisodeId}");
                    break;
                case GameMessageOpcode.ServerPathEpisodeProgress:
                {
                    PathEpisodeProgress progress = PacketCodec.ParsePathEpisodeProgress(packet.Body);
                    currentPathEpisodeId = progress.EpisodeId;
                    foreach (PathMissionStatus mission in progress.Missions)
                        TrackPathMission(mission);
                    log.Info($"path episode {progress.EpisodeId}: {progress.Missions.Count} mission state(s)");
                    break;
                }
                case GameMessageOpcode.ServerPathMissionActivate:
                    foreach (PathMissionStatus mission in PacketCodec.ParsePathMissionActivate(packet.Body))
                        TrackPathMission(mission);
                    break;
                case GameMessageOpcode.ServerPathMissionUpdate:
                    TrackPathMission(PacketCodec.ParsePathMissionUpdate(packet.Body));
                    break;
                case GameMessageOpcode.ServerPathMissionAdvanced:
                {
                    ushort missionId = PacketCodec.ParsePathMissionAdvanced(packet.Body);
                    PathMissionStatus previous = pathMissionStates.GetValueOrDefault(missionId)
                        ?? new PathMissionStatus(missionId, false, 0u, 0u, null, 0u);
                    TrackPathMission(previous with { Completed = true });
                    break;
                }
                case GameMessageOpcode.ServerPathScientistExperimentationInvoke:
                {
                    latestExperimentationInvoke = PacketCodec.ParsePathScientistExperimentationInvoke(packet.Body);
                    log.Info($"scientist experimentation invoke: unit={latestExperimentationInvoke.UnitId}, code=[{string.Join(',', latestExperimentationInvoke.CurrentPattern)}]");
                    break;
                }
                case GameMessageOpcode.ServerPathSoldierHoldOutNextWave:
                {
                    PathSoldierHoldoutWave wave = PacketCodec.ParsePathSoldierHoldoutNextWave(packet.Body);
                    if (activeHoldoutEventId is null || activeHoldoutEventId == wave.EventId)
                    {
                        currentHoldoutWave = checked((int)wave.WaveIndex + 1);
                        holdoutProgressPackets.Add(
                            $"next-wave(event={wave.EventId},rawIndex={wave.WaveIndex},wave={currentHoldoutWave},boss={wave.IsBoss.ToString().ToLowerInvariant()})");
                    }
                    log.Info($"holdout event {wave.EventId}: next wave rawIndex={wave.WaveIndex}, boss={wave.IsBoss}");
                    break;
                }
                case GameMessageOpcode.ServerPathSoldierHoldoutStatus:
                {
                    PathSoldierHoldoutStatus status = PacketCodec.ParsePathSoldierHoldoutStatus(packet.Body);
                    if (activeHoldoutEventId is null || activeHoldoutEventId == status.EventId)
                    {
                        if (status.Mode == PlayerPathSoldierEventMode.Active)
                            currentHoldoutWave = Math.Max(currentHoldoutWave ?? 0, status.WaveIndex + 1);
                        holdoutProgressPackets.Add(
                            $"status(event={status.EventId},mode={status.Mode},rawIndex={status.WaveIndex},wave={FormatCurrentHoldoutWave()},boss={status.IsBoss.ToString().ToLowerInvariant()},delayMs={status.DelayTime},startOffsetMs={status.StartTimeOffset},units={status.UnitCount})");
                    }
                    log.Info($"holdout event {status.EventId}: mode={status.Mode}, rawIndex={status.WaveIndex}, boss={status.IsBoss}, delay={status.DelayTime} ms");
                    break;
                }
                case GameMessageOpcode.ServerPathSoldierHoldoutDeath:
                {
                    ushort eventId = PacketCodec.ParsePathSoldierHoldoutDeath(packet.Body);
                    if (activeHoldoutEventId is null || activeHoldoutEventId == eventId)
                        holdoutProgressPackets.Add($"death(event={eventId},wave={FormatCurrentHoldoutWave()})");
                    log.Info($"holdout event {eventId}: death in wave {FormatCurrentHoldoutWave()}");
                    break;
                }
                case GameMessageOpcode.ServerPathSoldierHoldoutEnd:
                {
                    (ushort eventId, PlayerPathSoldierResult result) = PacketCodec.ParsePathSoldierHoldoutEnd(packet.Body);
                    if (activeHoldoutEventId is null || activeHoldoutEventId == eventId)
                    {
                        holdoutResult = result;
                        holdoutProgressPackets.Add($"end(event={eventId},result={result},wave={FormatCurrentHoldoutWave()})");
                    }
                    else
                    {
                        log.Info($"holdout event {eventId} ended with {result}; ignored while active event is {activeHoldoutEventId}");
                        break;
                    }
                    log.Info($"holdout event {eventId} ended: {result}");
                    break;
                }
                case GameMessageOpcode.ServerQuestInit:
                {
                    QuestInitInfo init = PacketCodec.ParseQuestInit(packet.Body);
                    completedQuests.UnionWith(init.CompletedQuestIds);
                    foreach ((ushort questId, QuestState state) in init.InactiveQuestStates)
                        questStates[questId] = state;
                    break;
                }
                case GameMessageOpcode.ServerQuestStateChange:
                {
                    QuestStateUpdate state = PacketCodec.ParseQuestState(packet.Body);
                    questStates[state.QuestId] = state.State;
                    if (state.State == QuestState.Completed)
                        completedQuests.Add(state.QuestId);
                    if (quest is not null && state.QuestId == quest.Id)
                        questState = state.State;
                    break;
                }
                case GameMessageOpcode.ServerQuestObjectiveUpdate:
                {
                    QuestObjectiveUpdate update = PacketCodec.ParseQuestObjective(packet.Body);
                    allObjectiveProgress[(update.QuestId, update.Index)] = update.Completed;
                    if (quest is not null && update.QuestId == quest.Id)
                    {
                        objectiveProgress[update.Index] = update.Completed;
                        UpdateObjectiveReport(update.Index, update.Completed);
                    }
                    break;
                }
                case GameMessageOpcode.ServerGenericError:
                {
                    GenericError error = PacketCodec.ParseGenericError(packet.Body);
                    lastGenericError = error;
                    log.Info($"server generic error: {error}");
                    currentQuestReport?.ServerMessages.Add($"generic error: {error}");
                    break;
                }
                case GameMessageOpcode.ServerItemError:
                {
                    GenericError error = PacketCodec.ParseItemError(packet.Body);
                    lastItemError = error;
                    if (error == GenericError.ItemInventoryFull)
                        bagsReportedFull = true;
                    log.Info($"server item error: {error}");
                    currentQuestReport?.ServerMessages.Add($"item error: {error}");
                    break;
                }
                case GameMessageOpcode.ServerChat:
                {
                    string text = PacketCodec.ParseServerChatText(packet.Body);
                    log.Info($"server chat: {text}");
                    if (text.Contains("pending teleport", StringComparison.OrdinalIgnoreCase))
                        teleportRefused = true;
                    currentQuestReport?.ServerMessages.Add(text);
                    break;
                }
                case GameMessageOpcode.ServerQueueStatus:
                {
                    var queue = PacketCodec.ParseQueueStatus(packet.Body);
                    log.Info($"realm queue position {queue.Position}; estimated wait {queue.WaitSeconds} s");
                    break;
                }
                case GameMessageOpcode.ServerQueueFinish:
                    log.Info("realm queue finished");
                    break;
            }
        }
        catch (Exception exception) when (packet.Opcode is GameMessageOpcode.ServerEntityCreate
            or GameMessageOpcode.ServerChat or GameMessageOpcode.ServerActionSet)
        {
            log.Info($"skipped malformed {packet.Opcode} body: {exception.Message}");
        }
    }

    private void TrackPathMission(PathMissionStatus status)
    {
        if (pathMissionStates.TryGetValue(status.MissionId, out PathMissionStatus? previous))
        {
            status = status with
            {
                Completed = status.Completed || previous.Completed,
                State = status.State ?? previous.State,
                GiverUnitId = status.GiverUnitId != 0u ? status.GiverUnitId : previous.GiverUnitId
            };
        }
        pathMissionStates[status.MissionId] = status;
        log.Info($"path mission {status.MissionId}: complete={status.Completed}, progress={status.Progress}, state={status.State?.ToString() ?? "not-sent"}");
    }

    private void RecordPlayerHealth(uint health)
    {
        if (combatTargetGuid is not null && currentHealth is uint previousHealth && health < previousHealth)
        {
            combatDamageTaken += previousHealth - health;
            RecordEnemyAction();
        }
        if (activeHoldoutEventId is not null && currentHealth != health)
            log.Info($"holdout health {health}/{currentMaxHealth?.ToString() ?? "?"} (wave {FormatCurrentHoldoutWave()})");
        currentHealth = health;
        if (resurrectionRecoveryActive && health > 0u)
        {
            botDied = false;
            resurrectionConfirmation ??= "positive player health update";
        }
    }

    private void RecordEnemyAction()
    {
        if (combatTargetGuid is null || combatFirstEnemyActionMs.HasValue)
            return;
        combatFirstEnemyActionMs = Math.Max(0L, (long)(DateTimeOffset.UtcNow - combatStartedAt).TotalMilliseconds);
    }

    private void LogCombatSlotDiscovery()
    {
        if (quest is null
            || !activeActionSetKnown
            || !actionSetCombatSlots.TryGetValue(activeActionSet, out ushort[]? slots)
            || !combatSlotDiscoveryLoggedQuests.Add(quest.Id))
            return;
        log.Info(slots.Length == 0
            ? $"quest {quest.Id} combat slot discovery: active action set {activeActionSet} has no spell shortcuts"
            : $"quest {quest.Id} combat slot discovery: active action set {activeActionSet} spell slots [{string.Join(',', slots)}]");
    }

    private void UpdateObjectiveReport(uint index, uint rawProgress)
    {
        ObjectiveReport? item = currentQuestReport?.Objectives.SingleOrDefault(objective => objective.Index == index);
        QuestObjectiveDefinition? definition = quest?.Objectives.SingleOrDefault(objective => objective.Index == index);
        if (item is null || definition is null)
            return;

        item.Progress = IsChecklistObjective(definition)
            ? Math.Min(definition.Count, (uint)BitOperations.PopCount(rawProgress))
            : IsDynamicKillObjective(definition)
            && definition.Count > 1
            ? Math.Min(definition.Count, (rawProgress * definition.Count + 999u) / 1000u)
            : Math.Min(definition.Count, rawProgress);
    }

    private bool IsObjectiveComplete(QuestObjectiveDefinition objective)
    {
        uint raw = GetRawProgress(objective.Index);
        if (IsChecklistObjective(objective))
            return (uint)BitOperations.PopCount(raw) >= objective.Count;
        if (IsDynamicKillObjective(objective)
            && objective.Count > 1)
            return raw >= 1000u;
        return raw >= Math.Max(1u, objective.Count);
    }

    private uint GetRawProgress(int index) => objectiveProgress.GetValueOrDefault((uint)index);

    // mirrors QuestObjective.IsDynamic on the server: kill objectives with Count > 1 count in thousandths unless flag 0x0200
    // is set, then plain kills (The Face of the Enemy 10518: 5 Battle Beasts credited 1..5, the bot waited for 1000)
    private static bool IsDynamicKillObjective(QuestObjectiveDefinition objective)
        => objective.Type is QuestObjectiveType.KillCreature
            or QuestObjectiveType.KillCreature2
            or QuestObjectiveType.KillTargetGroup
            or QuestObjectiveType.KillTargetGroups
            && objective.Count > 1u
            && (objective.Flags & 0x0200u) == 0u;

    private static bool IsChecklistObjective(QuestObjectiveDefinition objective)
        => objective.Type is QuestObjectiveType.ActivateTargetGroupChecklist or QuestObjectiveType.Unknown10;

    private ObjectiveReport RequireObjectiveReport(int index)
        => currentQuestReport?.Objectives.SingleOrDefault(item => item.Index == index)
            ?? throw new InvalidOperationException($"Objective report {index} is unavailable.");

    private Spawn ChooseNearestSpawn(IEnumerable<uint> creatureIds, ushort worldId, ISet<uint>? excluded = null)
    {
        IReadOnlyList<Spawn> allCandidates = SpawnsFor(creatureIds, worldId)
            .OrderBy(spawn => spawn.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
            .ToArray();
        IReadOnlyList<Spawn> candidates = allCandidates
            .Where(spawn => excluded is null || !excluded.Contains(spawn.Id))
            .ToArray();
        if (candidates.Count == 0 && excluded is not null && allCandidates.Count > 0)
        {
            // Start a new pass without immediately selecting the entity nearest the last attempted one.
            excluded.Clear();
            if (allCandidates.Count > 1)
            {
                excluded.Add(allCandidates[0].Id);
                return allCandidates[1];
            }
            return allCandidates[0];
        }
        return candidates.FirstOrDefault()
            ?? throw new BotFailureException($"No spawn in world {worldId} for creature(s) {string.Join(',', creatureIds)}.");
    }

    private Spawn ChooseCombatSpawn(IEnumerable<uint> creatureIds, ushort worldId, ISet<uint>? excluded = null)
    {
        HashSet<uint> objectiveCreatureIds = creatureIds.ToHashSet();
        IReadOnlyList<Spawn> allCandidates = SpawnsFor(objectiveCreatureIds, worldId);
        Spawn[] candidates = allCandidates
            .Where(spawn => excluded is null || !excluded.Contains(spawn.Id))
            .ToArray();
        if (candidates.Length == 0 && excluded is not null && allCandidates.Count > 0)
        {
            excluded.Clear();
            candidates = allCandidates.ToArray();
        }
        if (candidates.Length == 0)
            throw new BotFailureException($"No spawn in world {worldId} for creature(s) {string.Join(',', creatureIds)}.");

        return candidates
            .Select(spawn => new
            {
                Spawn = spawn,
                // Every candidate is a creature required by this combat objective, so this gives
                // every spawn a pack-density score even when it is outside the current entity stream.
                ObjectiveNeighbors = allCandidates.Count(other => other.Id != spawn.Id
                    && other.DistanceSquared(spawn.X, spawn.Y, spawn.Z) <= 15d * 15d),
                // Entity create packets do not expose disposition in the portion the tracker needs,
                // so treat every other live NonPlayer as a conservative hostile-pack candidate.
                TrackedNonPlayerNeighbors = entities.CountOtherNonPlayers(spawn, 15f) ?? 0
            })
            .OrderBy(candidate => candidate.Spawn.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
            .ThenBy(candidate => candidate.ObjectiveNeighbors + candidate.TrackedNonPlayerNeighbors)
            .ThenBy(candidate => candidate.ObjectiveNeighbors)
            .ThenBy(candidate => candidate.Spawn.Id)
            .First().Spawn;
    }

    private IReadOnlyList<Spawn> SpawnsFor(IEnumerable<uint> creatureIds, ushort worldId)
    {
        HashSet<uint> ids = creatureIds.ToHashSet();
        return spawns.Where(spawn => spawn.WorldId == worldId && ids.Contains(spawn.CreatureId)).ToArray();
    }

    private static double MinimumGiverReceiverDistance(IEnumerable<Spawn> givers, IEnumerable<Spawn> receivers, ushort worldId)
    {
        Spawn[] worldGivers = givers.Where(spawn => spawn.WorldId == worldId).ToArray();
        Spawn[] worldReceivers = receivers.Where(spawn => spawn.WorldId == worldId).ToArray();
        return worldGivers.SelectMany(giver => worldReceivers.Select(receiver =>
            receiver.DistanceSquared(giver.X, giver.Y, giver.Z))).DefaultIfEmpty(double.MaxValue).Min();
    }

    private void StartKeepAlive(CancellationToken cancellationToken)
    {
        keepAliveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        keepAliveTask = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
                while (await timer.WaitForNextTickAsync(keepAliveCancellation.Token))
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientPregameKeepAlive, [], keepAliveCancellation.Token);
            }
            catch (OperationCanceledException) when (keepAliveCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                log.Info($"keep-alive stopped: {exception.Message}");
            }
        });
    }

    private async Task StopKeepAliveAsync()
    {
        if (keepAliveCancellation is null)
            return;
        keepAliveCancellation.Cancel();
        if (keepAliveTask is not null)
            await keepAliveTask;
        keepAliveCancellation.Dispose();
    }

    private async Task TryCleanupAsync(CancellationToken runCancellation)
    {
        if (characterId == 0 || world is null || !world.Connected)
            return;
        if (options.KeepCharacter is not null)
        {
            log.Info($"keeping character {characterId} \"{report.CharacterName}\" on the account (--keep-character)");
            return;
        }

        using var cleanupGrace = runCancellation.IsCancellationRequested
            ? new CancellationTokenSource(TimeSpan.FromSeconds(5))
            : null;
        CancellationToken cleanupCancellation = cleanupGrace?.Token ?? runCancellation;

        try
        {
            await TimedStep.RunAsync(report, "delete character", DefaultStepTimeout, cleanupCancellation, async cancellationToken =>
            {
                await world.SendEncryptedAsync(GameMessageOpcode.ClientCharacterDelete,
                    PacketCodec.ClientCharacterDelete(characterId), cancellationToken);
                ProtocolPacket packet = await ReadWorldUntilAsync(
                    candidate => candidate.Opcode == GameMessageOpcode.ServerCharacterDeleteResult, cancellationToken);
                byte result = PacketCodec.ParseCharacterDeleteResult(packet.Body);
                if (result != 0)
                    throw new BotFailureException($"character deletion failed with result {result}");
                return $"character {characterId} deleted";
            });
        }
        catch (Exception exception)
        {
            log.Info($"cleanup failed: {exception.Message}");
        }
    }

    private static async Task<ProtocolPacket> ReadUntilAsync(
        ProtocolClient client,
        Func<ProtocolPacket, bool> predicate,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            ProtocolPacket packet = await client.ReadAsync(cancellationToken);
            if (predicate(packet))
                return packet;
        }
    }

    private BotGameData RequireGameData() => gameData ?? throw new InvalidOperationException("Game data is unavailable.");
    private QuestDefinition RequireQuest() => quest ?? throw new InvalidOperationException("Quest data is unavailable.");
    private ProtocolClient RequireWorld() => world ?? throw new InvalidOperationException("World connection is unavailable.");
}
