using System.Numerics;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable.Model;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.Bot;

internal sealed class BotRunner
{
    private static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CombatTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan WholeRunTimeout = TimeSpan.FromMinutes(10);

    private readonly RunOptions options;
    private readonly RunReport report;
    private readonly BotLog log;
    private readonly EntityTracker entities = new();
    private readonly Dictionary<uint, uint> objectiveProgress = [];

    private BotGameData? gameData;
    private QuestDefinition? quest;
    private IReadOnlyList<Spawn> spawns = [];
    private ProtocolClient? world;
    private RealmInfo? realm;
    private ulong characterId;
    private uint playerGuid;
    private ushort currentWorldId;
    private Vector3 lastPosition;
    private ushort questWorldId;
    private bool worldLoadPending;
    private bool enteredWorldSent;
    private bool questStarted;
    private bool levelCheatUsed;
    private QuestState questState;
    private ushort chatMessageId;
    private uint spellCastId = 1;
    private CancellationTokenSource? keepAliveCancellation;
    private Task? keepAliveTask;

    public BotRunner(RunOptions options, RunReport report, BotLog log)
    {
        this.options = options;
        this.report = report;
        this.log = log;
    }

    public async Task<int> RunAsync()
    {
        using var wholeRun = new CancellationTokenSource(WholeRunTimeout);
        int exitCode = 2;

        try
        {
            await TimedStep.RunAsync(report, "load quest data", DefaultStepTimeout, wholeRun.Token, LoadQuestDataAsync);
            await TimedStep.RunAsync(report, "authenticate", DefaultStepTimeout, wholeRun.Token, AuthenticateAsync);
            await TimedStep.RunAsync(report, "connect world", DefaultStepTimeout, wholeRun.Token, ConnectWorldAsync);
            await TimedStep.RunAsync(report, "create character", DefaultStepTimeout, wholeRun.Token, CreateCharacterAsync);
            await TimedStep.RunAsync(report, "enter world", DefaultStepTimeout, wholeRun.Token, EnterWorldAsync);

            StartKeepAlive(wholeRun.Token);

            await TimedStep.RunAsync(report, "accept quest", DefaultStepTimeout, wholeRun.Token, AcceptQuestAsync);
            questStarted = true;

            foreach (QuestObjectiveDefinition objective in RequireQuest().Objectives)
            {
                TimeSpan timeout = objective.Type is QuestObjectiveType.KillCreature or QuestObjectiveType.KillCreature2
                    ? CombatTimeout
                    : DefaultStepTimeout;
                await TimedStep.RunAsync(report, $"objective {objective.Index}", timeout, wholeRun.Token,
                    cancellationToken => RunObjectiveAsync(objective, cancellationToken));
            }

            await TimedStep.RunAsync(report, "quest achieved", DefaultStepTimeout, wholeRun.Token, WaitForAchievedAsync);
            await TimedStep.RunAsync(report, "turn in quest", DefaultStepTimeout, wholeRun.Token, TurnInQuestAsync);

            report.Result = "PASS";
            report.Failure = null;
            exitCode = 0;
        }
        catch (Exception exception)
        {
            string failure = exception is OperationCanceledException && wholeRun.IsCancellationRequested
                ? "whole run timeout after 10 minutes"
                : exception.Message;
            report.Result = "FAIL";
            report.Failure = failure;
            log.Info($"FAIL: {failure}");
            exitCode = questStarted ? 1 : 2;
        }
        finally
        {
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
        quest = gameData.GetQuest(options.QuestId);
        report.Title = quest.Title;
        foreach (QuestObjectiveDefinition objective in quest.Objectives)
        {
            report.Objectives.Add(new ObjectiveReport
            {
                Index = objective.Index,
                Type = objective.Type.ToString(),
                Data = objective.Data,
                Count = objective.Count
            });
        }

        IEnumerable<uint> objectiveCreatures = quest.Objectives
            .Where(objective => objective.Type is QuestObjectiveType.TalkTo
                or QuestObjectiveType.ActivateEntity
                or QuestObjectiveType.KillCreature
                or QuestObjectiveType.KillCreature2)
            .Select(objective => objective.Data);
        uint[] creatureIds = quest.GiverCreatureIds
            .Concat(quest.ReceiverCreatureIds)
            .Concat(objectiveCreatures)
            .Distinct()
            .ToArray();
        spawns = await BotDatabase.LoadSpawnsAsync(options.Database, creatureIds, cancellationToken);

        Spawn[] giverSpawns = spawns.Where(spawn => quest.GiverCreatureIds.Contains(spawn.CreatureId)).ToArray();
        Spawn[] receiverSpawns = spawns.Where(spawn => quest.ReceiverCreatureIds.Contains(spawn.CreatureId)).ToArray();
        if (giverSpawns.Length == 0)
            throw new BotFailureException($"No world spawn was found for a quest {quest.Id} giver.");
        if (receiverSpawns.Length == 0)
            throw new BotFailureException($"No world spawn was found for a quest {quest.Id} receiver.");

        ushort[] sharedWorlds = giverSpawns.Select(spawn => spawn.WorldId)
            .Intersect(receiverSpawns.Select(spawn => spawn.WorldId))
            .Distinct()
            .ToArray();
        if (sharedWorlds.Length == 0)
            throw new BotFailureException("Quest giver and receiver have no spawns in a common world.");

        questWorldId = sharedWorlds
            .OrderBy(worldId => MinimumGiverReceiverDistance(giverSpawns, receiverSpawns, worldId))
            .First();

        string selfCheck = "";
        if (quest.Id == 3479)
        {
            bool receiverIdMatches = quest.ReceiverCreatureIds.Contains(12959u);
            bool receiverSpawnMatches = receiverSpawns.Any(spawn => spawn.CreatureId == 12959u
                && spawn.WorldId == 426
                && spawn.DistanceSquared(4486.46f, -724.84f, -5391.26f) <= 100d);
            if (!receiverIdMatches || !receiverSpawnMatches)
                throw new BotFailureException("quest 3479 receiver self-check failed for creature 12959 near world 426 (4486.46, -724.84, -5391.26)");
            selfCheck = "; receiver self-check OK";
        }

        return $"{quest.Title}; {quest.Objectives.Count} objectives; quest world {questWorldId}; {spawns.Count} relevant spawns{selfCheck}";
    }

    private async Task<string> AuthenticateAsync(CancellationToken cancellationToken)
    {
        await using var auth = new ProtocolClient(log);
        await auth.ConnectAsync(options.Auth, 23115, cancellationToken);
        await ReadUntilAsync(auth, packet => packet.Opcode == GameMessageOpcode.ServerHello, cancellationToken);
        await auth.SendEncryptedAsync(GameMessageOpcode.ClientHelloAuth,
            PacketCodec.ClientHelloAuth(options.Email, options.Token), cancellationToken);

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
        CharacterCreationEntry creation = RequireGameData().GetBotCharacterCreation();
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            report.CharacterName = $"Bot {Random.Shared.Next(0, 10000):0000}";
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientCharacterCreate,
                PacketCodec.ClientCharacterCreate(creation, report.CharacterName), cancellationToken);

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
            return $"{report.CharacterName}; character {characterId}; creation {creation.Id} ({creation.CharacterCreationStartEnum}); world {result.WorldId}";
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
        return $"world {currentWorldId}; player guid {playerGuid}; player entity ready";
    }

    private async Task<string> AcceptQuestAsync(CancellationToken cancellationToken)
    {
        QuestDefinition definition = RequireQuest();
        bool currentWorldHasGiver = SpawnsFor(definition.GiverCreatureIds, currentWorldId).Count > 0;
        bool currentWorldHasReceiver = SpawnsFor(definition.ReceiverCreatureIds, currentWorldId).Count > 0;
        if (currentWorldHasGiver && currentWorldHasReceiver)
            questWorldId = currentWorldId;
        Spawn giver = ChooseNearestSpawn(definition.GiverCreatureIds, questWorldId);
        TrackedEntity entity = await TeleportAndFindAsync(giver, cancellationToken);
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
            PacketCodec.ClientEntityInteract(entity.Guid, InteractionType.TalkTo), cancellationToken);
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestAccept,
            PacketCodec.ClientQuestAccept(definition.Id), cancellationToken);
        await WaitForQuestStateAsync(QuestState.Accepted, cancellationToken);
        return $"accepted from creature {entity.CreatureId} guid {entity.Guid}";
    }

    private async Task<string> RunObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        switch (objective.Type)
        {
            case QuestObjectiveType.TalkTo:
                await RunInteractionObjectiveAsync(objective, InteractionType.TalkTo, cancellationToken);
                break;
            case QuestObjectiveType.ActivateEntity:
                await RunActivateObjectiveAsync(objective, cancellationToken);
                break;
            case QuestObjectiveType.KillCreature:
            case QuestObjectiveType.KillCreature2:
                await RunKillObjectiveAsync(objective, cancellationToken);
                break;
            case QuestObjectiveType.EnterArea:
            case QuestObjectiveType.EnterZone:
                await RunLocationObjectiveAsync(objective, cancellationToken);
                break;
            default:
                throw new BotFailureException($"unsupported objective type {(uint)objective.Type}");
        }

        ObjectiveReport objectiveReport = report.Objectives.Single(item => item.Index == objective.Index);
        objectiveReport.Ok = true;
        return $"{objective.Type}; progress {objectiveReport.Progress}/{objectiveReport.Count}";
    }

    private async Task RunInteractionObjectiveAsync(
        QuestObjectiveDefinition objective,
        InteractionType interactionType,
        CancellationToken cancellationToken)
    {
        while (!IsObjectiveComplete(objective))
        {
            uint before = GetRawProgress(objective.Index);
            Spawn spawn = ChooseNearestSpawn([objective.Data], questWorldId);
            TrackedEntity entity = await TeleportAndFindAsync(spawn, cancellationToken);
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
                PacketCodec.ClientEntityInteract(entity.Guid, interactionType), cancellationToken);
            await WaitForObjectiveProgressAsync(objective.Index, before, cancellationToken);
        }
    }

    private async Task RunActivateObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        while (!IsObjectiveComplete(objective))
        {
            uint before = GetRawProgress(objective.Index);
            Spawn spawn = ChooseNearestSpawn([objective.Data], questWorldId);
            TrackedEntity entity = await TeleportAndFindAsync(spawn, cancellationToken);
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnit,
                PacketCodec.ClientActivateUnit(entity.Guid), cancellationToken);

            bool advanced = await TryWaitForObjectiveProgressAsync(objective.Index, before, TimeSpan.FromSeconds(5), cancellationToken);
            if (!advanced)
            {
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
                    PacketCodec.ClientEntityInteract(entity.Guid, InteractionType.Interact), cancellationToken);
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnitCast,
                    PacketCodec.ClientActivateUnitCast(spellCastId++, entity.Guid), cancellationToken);
                await WaitForObjectiveProgressAsync(objective.Index, before, cancellationToken);
            }
        }
    }

    private async Task RunKillObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        if (!levelCheatUsed)
        {
            await SendCheatAsync("!character level 10", cancellationToken);
            levelCheatUsed = true;
        }

        var attemptedSpawns = new HashSet<uint>();
        while (!IsObjectiveComplete(objective))
        {
            Spawn spawn = ChooseNearestSpawn([objective.Data], questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity entity = await TeleportAndFindAsync(spawn, cancellationToken);
            uint before = GetRawProgress(objective.Index);
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                PacketCodec.ClientEntitySelect(entity.Guid), cancellationToken);

            bool confirmed = await FightUntilProgressAsync(objective.Index, before, entity.Guid, cancellationToken);
            if (!confirmed && attemptedSpawns.Count >= SpawnsFor([objective.Data], questWorldId).Count)
                throw new BotFailureException($"target creature {objective.Data} died without a quest objective update");
        }
    }

    private async Task<bool> FightUntilProgressAsync(int objectiveIndex, uint before, uint targetGuid, CancellationToken cancellationToken)
    {
        bool destroyed = false;
        DateTimeOffset? destroyedAt = null;
        ushort bagIndex = 0;

        while (true)
        {
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientCastSpell,
                PacketCodec.ClientCastSpell(spellCastId++, bagIndex, targetGuid), cancellationToken);
            bagIndex = (ushort)((bagIndex + 1) % 4);

            DateTimeOffset cycleEnd = DateTimeOffset.UtcNow.AddSeconds(1.5);
            while (DateTimeOffset.UtcNow < cycleEnd || destroyed)
            {
                TimeSpan remaining = destroyed
                    ? destroyedAt!.Value.AddSeconds(5) - DateTimeOffset.UtcNow
                    : cycleEnd - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    return !destroyed ? GetRawProgress(objectiveIndex) > before : false;

                ProtocolPacket? packet = await TryReadWorldPacketAsync(remaining, cancellationToken);
                if (packet is null)
                    break;
                if (GetRawProgress(objectiveIndex) > before)
                    return true;
                if (packet.Opcode == GameMessageOpcode.ServerEntityDestroy
                    && PacketCodec.ParseEntityDestroy(packet.Body) == targetGuid)
                {
                    destroyed = true;
                    destroyedAt = DateTimeOffset.UtcNow;
                }
            }

            if (GetRawProgress(objectiveIndex) > before)
                return true;
            if (destroyed)
                return false;
        }
    }

    private async Task RunLocationObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        WorldLocation2Entry location = RequireGameData().GetWorldLocation(objective.WorldLocationId);
        uint before = GetRawProgress(objective.Index);
        bool includeWorld = currentWorldId != location.WorldId;
        if (includeWorld)
            entities.Clear();
        await SendCheatAsync(PacketCodec.TeleportCommand(location, includeWorld), cancellationToken);
        if (!includeWorld)
            await WaitForLocalTeleportAsync(cancellationToken);
        else
            await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerPlayerEnteredWorld, cancellationToken);
        lastPosition = new Vector3(location.Position0, location.Position1, location.Position2);
        await WaitForObjectiveProgressAsync(objective.Index, before, cancellationToken);
    }

    private async Task<string> WaitForAchievedAsync(CancellationToken cancellationToken)
    {
        await WaitForQuestStateAsync(QuestState.Achieved, cancellationToken);
        return "server reported state 2 (Achieved)";
    }

    private async Task<string> TurnInQuestAsync(CancellationToken cancellationToken)
    {
        QuestDefinition definition = RequireQuest();
        Spawn receiver = ChooseNearestSpawn(definition.ReceiverCreatureIds, questWorldId);
        TrackedEntity entity = await TeleportAndFindAsync(receiver, cancellationToken);
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
            PacketCodec.ClientEntityInteract(entity.Guid, InteractionType.TalkTo), cancellationToken);
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientQuestComplete,
            PacketCodec.ClientQuestComplete(definition.Id), cancellationToken);
        await WaitForQuestStateAsync(QuestState.Completed, cancellationToken);
        return $"completed with receiver creature {entity.CreatureId} guid {entity.Guid}; state 3";
    }

    private async Task<TrackedEntity> TeleportAndFindAsync(Spawn spawn, CancellationToken cancellationToken)
    {
        bool includeWorld = currentWorldId != spawn.WorldId;
        if (includeWorld)
            entities.Clear();
        await SendCheatAsync(PacketCodec.TeleportCommand(spawn, includeWorld), cancellationToken);
        if (!includeWorld)
            await WaitForLocalTeleportAsync(cancellationToken);
        lastPosition = new Vector3(spawn.X + 2f, spawn.Y, spawn.Z + 2f);

        while (true)
        {
            TrackedEntity? existing = currentWorldId == spawn.WorldId && !worldLoadPending
                ? entities.FindCreature(spawn.CreatureId, spawn)
                : null;
            if (existing is not null)
                return existing;
            await ReadAndHandleWorldPacketAsync(cancellationToken);
        }
    }

    private async Task WaitForLocalTeleportAsync(CancellationToken cancellationToken)
    {
        await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerLoadingScreen, cancellationToken);
        await ReadWorldUntilAsync(packet => packet.Opcode == GameMessageOpcode.ServerMovementControl, cancellationToken);
    }

    private async Task SendCheatAsync(string command, CancellationToken cancellationToken)
    {
        if (command.StartsWith("!quest", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("quest commands are forbidden");
        report.Cheats.Add(command);
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
            switch (packet.Opcode)
            {
                case GameMessageOpcode.ServerChangeWorld:
                {
                    (currentWorldId, lastPosition) = PacketCodec.ParseChangeWorld(packet.Body);
                    worldLoadPending = true;
                    enteredWorldSent = false;
                    entities.Clear();
                    break;
                }
                case GameMessageOpcode.ServerMovementControl:
                    playerGuid = PacketCodec.ParseMovementControl(packet.Body);
                    break;
                case GameMessageOpcode.ServerPlayerChanged:
                    playerGuid = PacketCodec.ParsePlayerChanged(packet.Body);
                    break;
                case GameMessageOpcode.ServerPlayerCreate when worldLoadPending && !enteredWorldSent:
                    await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEnteredWorld,
                        PacketCodec.ClientEnteredWorld(), cancellationToken);
                    enteredWorldSent = true;
                    break;
                case GameMessageOpcode.ServerPlayerEnteredWorld:
                    worldLoadPending = false;
                    break;
                case GameMessageOpcode.ServerEntityCreate:
                    _ = entities.ParseCreate(packet.Body);
                    break;
                case GameMessageOpcode.ServerEntityDestroy:
                    entities.Remove(PacketCodec.ParseEntityDestroy(packet.Body));
                    break;
                case GameMessageOpcode.ServerQuestStateChange:
                {
                    QuestStateUpdate state = PacketCodec.ParseQuestState(packet.Body);
                    if (quest is not null && state.QuestId == quest.Id)
                        questState = state.State;
                    break;
                }
                case GameMessageOpcode.ServerQuestObjectiveUpdate:
                {
                    QuestObjectiveUpdate update = PacketCodec.ParseQuestObjective(packet.Body);
                    if (quest is not null && update.QuestId == quest.Id)
                    {
                        objectiveProgress[update.Index] = update.Completed;
                        UpdateObjectiveReport(update.Index, update.Completed);
                    }
                    break;
                }
                case GameMessageOpcode.ServerChat:
                    log.Info($"server chat: {PacketCodec.ParseServerChatText(packet.Body)}");
                    break;
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
        catch (Exception exception) when (packet.Opcode is GameMessageOpcode.ServerEntityCreate or GameMessageOpcode.ServerChat)
        {
            log.Info($"skipped malformed {packet.Opcode} body: {exception.Message}");
        }
    }

    private void UpdateObjectiveReport(uint index, uint rawProgress)
    {
        ObjectiveReport? item = report.Objectives.SingleOrDefault(objective => objective.Index == index);
        QuestObjectiveDefinition? definition = quest?.Objectives.SingleOrDefault(objective => objective.Index == index);
        if (item is null || definition is null)
            return;

        item.Progress = definition.Type is QuestObjectiveType.KillCreature or QuestObjectiveType.KillCreature2
            && definition.Count > 1
            ? Math.Min(definition.Count, (rawProgress * definition.Count + 999u) / 1000u)
            : Math.Min(definition.Count, rawProgress);
    }

    private bool IsObjectiveComplete(QuestObjectiveDefinition objective)
    {
        uint raw = GetRawProgress(objective.Index);
        if (objective.Type is QuestObjectiveType.KillCreature or QuestObjectiveType.KillCreature2
            && objective.Count > 1)
            return raw >= 1000u;
        return raw >= Math.Max(1u, objective.Count);
    }

    private uint GetRawProgress(int index) => objectiveProgress.GetValueOrDefault((uint)index);

    private Spawn ChooseNearestSpawn(IEnumerable<uint> creatureIds, ushort worldId, ISet<uint>? excluded = null)
    {
        IReadOnlyList<Spawn> candidates = SpawnsFor(creatureIds, worldId)
            .Where(spawn => excluded is null || !excluded.Contains(spawn.Id))
            .OrderBy(spawn => spawn.DistanceSquared(lastPosition.X, lastPosition.Y, lastPosition.Z))
            .ToArray();
        if (candidates.Count == 0 && excluded is not null)
            return ChooseNearestSpawn(creatureIds, worldId);
        return candidates.FirstOrDefault()
            ?? throw new BotFailureException($"No spawn in world {worldId} for creature(s) {string.Join(',', creatureIds)}.");
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
