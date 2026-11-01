using System.Diagnostics;
using System.Numerics;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable.Model;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.Bot;

internal sealed class BotRunner
{
    private static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromSeconds(75); // cross-world teleports wait on the server loading map grids
    private static readonly TimeSpan CombatTimeout = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan PerQuestTimeout = TimeSpan.FromMinutes(8);

    private readonly RunOptions options;
    private readonly RunReport report;
    private readonly BotLog log;
    private readonly EntityTracker entities = new();
    private readonly Dictionary<uint, uint> objectiveProgress = [];
    private readonly Dictionary<ushort, QuestDefinition> questDefinitions = [];
    private readonly HashSet<ushort> completedQuests = [];
    private readonly HashSet<ushort> finishedQuests = [];
    private readonly HashSet<ushort> reportedSkippedQuests = [];
    private readonly HashSet<uint> respondedCsiSpellIds = [];

    private BotGameData? gameData;
    private QuestDefinition? quest;
    private QuestRunReport? currentQuestReport;
    private QuestObjectiveDefinition? activeObjective;
    private ResurrectionShow? resurrectionShow;
    private DateTimeOffset resurrectionShowReceivedAt;
    private IReadOnlyList<Spawn> spawns = [];
    private IReadOnlyList<QuestDefinition> questPlan = [];
    private ProtocolClient? world;
    private RealmInfo? realm;
    private ulong characterId;
    private uint playerGuid;
    private ushort currentWorldId;
    private Vector3 lastPosition;
    private ushort questWorldId;
    private bool worldLoadPending;
    private bool enteredWorldSent;
    private bool levelCheatUsed;
    private bool botDied;
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
        using var wholeRun = new CancellationTokenSource(TimeSpan.FromMinutes(options.MaxMinutes));
        int exitCode = 2;
        bool setupComplete = false;

        try
        {
            await TimedStep.RunAsync(report, "load quest data", DefaultStepTimeout, wholeRun.Token, LoadQuestDataAsync);
            await TimedStep.RunAsync(report, "authenticate", DefaultStepTimeout, wholeRun.Token, AuthenticateAsync);
            await TimedStep.RunAsync(report, "connect world", DefaultStepTimeout, wholeRun.Token, ConnectWorldAsync);
            await TimedStep.RunAsync(report, "create character", DefaultStepTimeout, wholeRun.Token, CreateCharacterAsync);
            await TimedStep.RunAsync(report, "enter world", DefaultStepTimeout, wholeRun.Token, EnterWorldAsync);
            setupComplete = true;

            StartKeepAlive(wholeRun.Token);

            await RunRequestedQuestAsync(wholeRun.Token);

            bool failed = report.Quests.Any(questReport => questReport.Result == "FAIL");
            report.Result = failed ? "FAIL" : "PASS";
            report.Failure = failed ? $"{report.Quests.Count(questReport => questReport.Result == "FAIL")} quest(s) failed" : null;
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
        questPlan = gameData.GetRunPlan(options);
        foreach (QuestDefinition definition in questPlan)
            questDefinitions.Add(definition.Id, definition);

        QuestDefinition requested = questPlan[0];
        report.Title = options.ZoneId is uint zoneId ? $"Zone {zoneId} ({options.Faction})" : requested.Title;
        foreach (QuestObjectiveDefinition objective in options.ZoneId is null && options.QuestIds.Count == 1 ? requested.Objectives : [])
        {
            report.Objectives.Add(new ObjectiveReport
            {
                Index = objective.Index,
                Type = objective.Type.ToString(),
                Data = objective.Data,
                Count = objective.Count
            });
        }

        IEnumerable<uint> objectiveCreatures = questPlan.SelectMany(definition => definition.Objectives)
            .SelectMany(GetObjectiveCreatureIds);
        uint[] creatureIds = questPlan.SelectMany(definition => definition.GiverCreatureIds)
            .Concat(questPlan.SelectMany(definition => definition.ReceiverCreatureIds))
            .Concat(objectiveCreatures)
            .Distinct()
            .ToArray();
        spawns = await BotDatabase.LoadSpawnsAsync(options.Database, creatureIds, cancellationToken);
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
        return $"{selection}; {questPlan.Count} quest(s) loaded; {spawns.Count} relevant spawns{selfCheck}";
    }

    private IEnumerable<uint> GetObjectiveCreatureIds(QuestObjectiveDefinition objective)
    {
        if (objective.Type is QuestObjectiveType.KillTargetGroup or QuestObjectiveType.KillTargetGroups
            or QuestObjectiveType.ActivateTargetGroup or QuestObjectiveType.ActivateTargetGroupChecklist
            or QuestObjectiveType.Unknown10 or QuestObjectiveType.TalkToTargetGroup or QuestObjectiveType.VirtualCollect
            or QuestObjectiveType.Unknown28)
            return RequireGameData().ResolveTargetGroup(RequireGameData().GetObjectiveTargetGroup(objective));
        if (objective.Type is QuestObjectiveType.TalkTo
            or QuestObjectiveType.ActivateEntity
            or QuestObjectiveType.KillCreature
            or QuestObjectiveType.KillCreature2
            or QuestObjectiveType.SucceedCSI
            or QuestObjectiveType.GatheResource)
            return [objective.Data];
        return [];
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
        CharacterCreationEntry creation = RequireGameData().GetBotCharacterCreation(options.Start, options.Faction);
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
        await DrainWorldPacketsAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
        return $"world {currentWorldId}; player guid {playerGuid}; player entity ready";
    }

    private async Task RunRequestedQuestAsync(CancellationToken cancellationToken)
    {
        foreach (QuestDefinition definition in questPlan)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            using var questBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            questBudget.CancelAfter(PerQuestTimeout);
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
                    failedReport.Failure = "per-quest timeout after 8 minutes";
                    failedReport.Steps.Add(new StepReport
                    {
                        Name = "quest budget",
                        Ok = false,
                        ElapsedMs = (long)PerQuestTimeout.TotalMilliseconds,
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
        try
        {
            DateTimeOffset showDeadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (resurrectionShow is null)
            {
                TimeSpan remaining = showDeadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                    throw new BotFailureException("server did not offer resurrection within 15 seconds");
            }

            ResurrectionType type = (resurrectionShow.Flags & ResurrectionType.WakeHere) != 0
                ? ResurrectionType.WakeHere
                : (resurrectionShow.Flags & ResurrectionType.Holocrypt) != 0
                    ? ResurrectionType.Holocrypt
                    : (resurrectionShow.Flags & ResurrectionType.ExitInstance) != 0
                        ? ResurrectionType.ExitInstance
                        : ResurrectionType.None;
            if (type == ResurrectionType.None)
                throw new BotFailureException($"server offered no usable resurrection type ({resurrectionShow.Flags})");
            uint elapsedSinceOfferMs = (uint)Math.Min(uint.MaxValue,
                Math.Max(0d, (DateTimeOffset.UtcNow - resurrectionShowReceivedAt).TotalMilliseconds));
            uint remainingRezMs = resurrectionShow.TimeUntilRezMs > elapsedSinceOfferMs
                ? resurrectionShow.TimeUntilRezMs - elapsedSinceOfferMs
                : 0u;
            if (remainingRezMs > 30_000u)
                throw new BotFailureException($"resurrection is unavailable for another {remainingRezMs} ms");
            if (remainingRezMs > 0u)
                await Task.Delay(TimeSpan.FromMilliseconds(remainingRezMs + 100u), cancellationToken);

            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientResurrectAccept,
                PacketCodec.ClientResurrectAccept(resurrectionShow.GhostId, type), cancellationToken);
            DateTimeOffset aliveDeadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (botDied)
            {
                TimeSpan remaining = aliveDeadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                    throw new BotFailureException("server did not confirm resurrection within 15 seconds");
            }

            questReport.Steps.Add(new StepReport
            {
                Name = "recover from death",
                Ok = true,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Detail = $"resurrected with {type}"
            });
        }
        catch (Exception exception)
        {
            questReport.Steps.Add(new StepReport
            {
                Name = "recover from death",
                Ok = false,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Detail = exception.Message
            });
            log.Info($"death recovery failed: {exception.Message}");
        }
    }

    private ushort? GetMissingPrerequisite(QuestDefinition definition)
    {
        ushort[] prerequisites = definition.Entry.PrerequisiteQuests
            .Where(value => value != 0u)
            .Select(value => checked((ushort)value))
            .ToArray();
        if (prerequisites.Length == 0)
            return null;
        if ((definition.Entry.PrerequisiteFlags & 1u) != 0u)
            return prerequisites.Any(completedQuests.Contains) ? null : prerequisites[0];
        return prerequisites.FirstOrDefault(id => !completedQuests.Contains(id)) is ushort missing && missing != 0
            ? missing
            : null;
    }

    private void AddBlockedQuestReport(QuestDefinition definition, ushort prerequisiteId)
    {
        QuestRunReport questReport = CreateQuestReport(definition);
        string failure = $"blocked by prerequisite {prerequisiteId}";
        questReport.Result = "FAIL";
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

    private async Task RunQuestAsync(QuestDefinition definition, CancellationToken cancellationToken)
    {
        currentQuestReport = CreateQuestReport(definition);
        report.Quests.Add(currentQuestReport);
        QuestRunReport questReport = currentQuestReport;
        try
        {
            ConfigureQuest(definition);
            await RunQuestStepAsync("accept quest", DefaultStepTimeout, cancellationToken, AcceptQuestAsync);

            foreach (QuestObjectiveDefinition objective in definition.Objectives)
            {
                TimeSpan timeout = objective.Type is QuestObjectiveType.KillCreature
                    or QuestObjectiveType.KillCreature2
                    or QuestObjectiveType.KillTargetGroup
                    or QuestObjectiveType.KillTargetGroups
                    or QuestObjectiveType.VirtualCollect
                    or QuestObjectiveType.GatheResource
                    ? CombatTimeout
                    : objective.Type == QuestObjectiveType.SucceedCSI
                        ? TimeSpan.FromSeconds(60)
                        : DefaultStepTimeout;
                await RunQuestStepAsync($"objective {objective.Index}", timeout, cancellationToken,
                    token => RunObjectiveAsync(objective, token));
            }

            await RunQuestStepAsync("quest achieved", DefaultStepTimeout, cancellationToken, WaitForAchievedAsync);
            await RunQuestStepAsync("turn in quest", DefaultStepTimeout, cancellationToken, TurnInQuestAsync);
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

    private void ConfigureQuest(QuestDefinition definition)
    {
        quest = definition;
        questState = QuestState.Unknown;
        objectiveProgress.Clear();
        if (botDied)
            throw new BotFailureException("bot remains dead after the previous quest");
        resurrectionShow = null;

        questWorldId = DetermineQuestWorld(definition);
    }

    private ushort DetermineQuestWorld(QuestDefinition definition)
    {
        Spawn[] giverSpawns = spawns.Where(spawn => definition.GiverCreatureIds.Contains(spawn.CreatureId)).ToArray();
        Spawn[] receiverSpawns = spawns.Where(spawn => definition.ReceiverCreatureIds.Contains(spawn.CreatureId)).ToArray();
        if (giverSpawns.Length == 0)
            throw new BotFailureException($"No world spawn was found for a quest {definition.Id} giver.");
        if (receiverSpawns.Length == 0)
            throw new BotFailureException($"No world spawn was found for a quest {definition.Id} receiver.");

        ushort[] sharedWorlds = giverSpawns.Select(spawn => spawn.WorldId)
            .Intersect(receiverSpawns.Select(spawn => spawn.WorldId))
            .Distinct()
            .ToArray();
        if (sharedWorlds.Length == 0)
            throw new BotFailureException($"Quest {definition.Id} giver and receiver have no spawns in a common world.");

        return sharedWorlds
            .OrderBy(worldId => MinimumGiverReceiverDistance(giverSpawns, receiverSpawns, worldId))
            .First();
    }

    private QuestRunReport CreateQuestReport(QuestDefinition definition)
    {
        var questReport = new QuestRunReport
        {
            QuestId = definition.Id,
            Title = definition.Title,
            CharacterName = report.CharacterName
        };
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
        try
        {
            string reportName = questPlan.Count > 1 ? $"quest {RequireQuest().Id}: {name}" : name;
            return await TimedStep.RunAsync(report, reportName, timeout, cancellationToken, action);
        }
        finally
        {
            if (currentQuestReport is not null && report.Steps.Count > stepCount)
            {
                StepReport topLevel = report.Steps[^1];
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
        activeObjective = objective;
        ObjectiveReport objectiveReport = RequireObjectiveReport(objective.Index);
        try
        {
            switch (objective.Type)
            {
                case QuestObjectiveType.TalkTo:
                    await RunInteractionObjectiveAsync(objective, InteractionType.TalkTo, cancellationToken);
                    break;
                case QuestObjectiveType.TalkToTargetGroup:
                    await RunTargetGroupInteractionObjectiveAsync(objective, cancellationToken);
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
                case QuestObjectiveType.GatheResource:
                    await RunGatherResourceObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.SucceedCSI:
                    await RunSucceedCsiObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.KillCreature:
                case QuestObjectiveType.KillCreature2:
                case QuestObjectiveType.KillTargetGroup:
                case QuestObjectiveType.KillTargetGroups:
                    await RunKillObjectiveAsync(objective, cancellationToken);
                    break;
                case QuestObjectiveType.EnterArea:
                case QuestObjectiveType.EnterZone:
                    await RunLocationObjectiveAsync(objective, cancellationToken);
                    break;
                default:
                    throw new BotFailureException($"unsupported objective type {objective.Type}({(uint)objective.Type})");
            }

            objectiveReport.Ok = true;
            objectiveReport.Failure = null;
            return $"{objective.Type}; progress {objectiveReport.Progress}/{objectiveReport.Count}";
        }
        catch (Exception exception)
        {
            objectiveReport.Ok = false;
            objectiveReport.Failure = botDied ? "bot died" : exception.Message;
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
                continue;
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
                continue;
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
                PacketCodec.ClientEntityInteract(entity.Guid, InteractionType.TalkTo), cancellationToken);
            if (!await TryWaitForObjectiveProgressAsync(objective.Index, before, TimeSpan.FromSeconds(8), cancellationToken)
                && attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException($"talking to target group {objective.Data} produced no objective progress");
        }
    }

    private async Task RunActivateObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        if (objective.Data == 0u)
            throw new BotFailureException("ActivateEntity objective has no creature id in Data or objective text");
        var attemptedSpawns = new HashSet<uint>();
        IReadOnlyList<Spawn> candidates = SpawnsFor([objective.Data], questWorldId);
        while (!IsObjectiveComplete(objective))
        {
            uint before = GetRawProgress(objective.Index);
            Spawn spawn = ChooseNearestSpawn([objective.Data], questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity? entity = await TeleportAndTryFindAsync(spawn, TimeSpan.FromSeconds(5), cancellationToken);
            if (entity is null)
                continue;
            if (!await RunActivationFlowAsync(objective.Index, before, entity, TimeSpan.FromSeconds(8), cancellationToken)
                && attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException($"activation of creature {objective.Data} produced no objective progress");
        }
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
            if (checklist && attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException($"target group {objective.Data} has fewer distinct usable entities than checklist count {objective.Count}");

            Spawn spawn = ChooseNearestSpawn(creatureIds, questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity? entity = await TeleportAndTryFindAsync(spawn, TimeSpan.FromSeconds(5), cancellationToken);
            if (entity is null)
                continue;
            uint before = GetRawProgress(objective.Index);
            bool advanced = await RunActivationFlowAsync(objective.Index, before, entity, TimeSpan.FromSeconds(8), cancellationToken);
            if (!advanced && attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException(checklist
                    ? $"checklist entities for target group {objective.Data} produced no further progress"
                    : $"activation targets for group {objective.Data} produced no further progress");
        }
    }

    private async Task RunVirtualCollectObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        uint[] creatureIds = GetObjectiveCreatureIds(objective).ToArray();
        if (creatureIds.Length == 0)
            throw new BotFailureException($"target group {objective.Data} resolved to no creature ids");

        var attemptedSpawns = new HashSet<uint>();
        while (!IsObjectiveComplete(objective))
        {
            Spawn spawn = ChooseNearestSpawn(creatureIds, questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity entity = await TeleportAndFindAsync(spawn, cancellationToken);
            uint before = GetRawProgress(objective.Index);
            bool advanced = await RunActivationFlowAsync(objective.Index, before, entity, TimeSpan.FromSeconds(8), cancellationToken);
            if (advanced)
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
            Spawn spawn = ChooseNearestSpawn([objective.Data], questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity entity = await TeleportAndFindAsync(spawn, cancellationToken);
            uint before = GetRawProgress(objective.Index);
            bool advanced = await RunActivationFlowAsync(objective.Index, before, entity, TimeSpan.FromSeconds(8), cancellationToken);
            if (advanced)
                continue;
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                PacketCodec.ClientEntitySelect(entity.Guid), cancellationToken);
            _ = await FightUntilProgressAsync(objective.Index, before, entity.Guid, cancellationToken);
        }
    }

    private async Task<bool> RunActivationFlowAsync(
        int objectiveIndex,
        uint before,
        TrackedEntity entity,
        TimeSpan progressTimeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + progressTimeout;
        uint clientUniqueId = spellCastId++;
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnitCast,
            PacketCodec.ClientActivateUnitCast(clientUniqueId, entity.Guid), cancellationToken);
        SpellCastStart? spellStart = await TryWaitForCsiSpellStartAsync(
            clientUniqueId,
            TimeSpan.FromSeconds(Math.Min(5d, progressTimeout.TotalSeconds)),
            cancellationToken);
        if (spellStart is not null)
        {
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientSpellClientSideInteraction,
                PacketCodec.ClientSpellClientSideInteraction(spellStart.ServerUniqueId), cancellationToken);
            respondedCsiSpellIds.Add(spellStart.ServerUniqueId);
        }

        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnit,
            PacketCodec.ClientActivateUnit(entity.Guid), cancellationToken);
        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntityInteract,
            PacketCodec.ClientEntityInteract(entity.Guid, InteractionType.Interact), cancellationToken);
        TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
        return GetRawProgress(objectiveIndex) > before
            || remaining > TimeSpan.Zero
                && await TryWaitForObjectiveProgressAsync(objectiveIndex, before, remaining, cancellationToken);
    }

    private async Task RunSucceedCsiObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        var attemptedSpawns = new HashSet<uint>();
        IReadOnlyList<Spawn> candidates = SpawnsFor([objective.Data], questWorldId);
        if (candidates.Count == 0)
            throw new BotFailureException($"No spawn in world {questWorldId} for CSI creature {objective.Data}.");

        while (!IsObjectiveComplete(objective))
        {
            if (attemptedSpawns.Count >= candidates.Count)
                throw new BotFailureException($"CSI creature {objective.Data} did not provide enough activatable spawns");

            Spawn spawn = ChooseNearestSpawn([objective.Data], questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity? entity = await TeleportAndTryFindAsync(spawn, TimeSpan.FromSeconds(5), cancellationToken);
            if (entity is null)
                continue;
            uint before = GetRawProgress(objective.Index);
            uint clientUniqueId = spellCastId++;
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnitCast,
                PacketCodec.ClientActivateUnitCast(clientUniqueId, entity.Guid), cancellationToken);

            SpellCastStart? spellStart = await TryWaitForCsiSpellStartAsync(clientUniqueId, TimeSpan.FromSeconds(5), cancellationToken);
            if (spellStart is null)
            {
                await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientActivateUnit,
                    PacketCodec.ClientActivateUnit(entity.Guid), cancellationToken);
                continue;
            }

            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientSpellClientSideInteraction,
                PacketCodec.ClientSpellClientSideInteraction(spellStart.ServerUniqueId), cancellationToken);
            respondedCsiSpellIds.Add(spellStart.ServerUniqueId);
            await WaitForObjectiveProgressAsync(objective.Index, before, cancellationToken);
        }
    }

    private async Task<SpellCastStart?> TryWaitForCsiSpellStartAsync(
        uint clientUniqueId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return null;
            ProtocolPacket? packet = await TryReadWorldPacketAsync(remaining, cancellationToken);
            if (packet is null)
                return null;

            if (packet.Opcode == GameMessageOpcode.ServerSpellStart)
            {
                SpellCastStart start = PacketCodec.ParseSpellStart(packet.Body);
                if (start.CasterId == playerGuid && !respondedCsiSpellIds.Contains(start.ServerUniqueId))
                    return start;
            }
            else if (packet.Opcode == GameMessageOpcode.ServerSpellConfirmCast)
            {
                SpellCastStart start = PacketCodec.ParseSpellConfirmCast(packet.Body);
                if (start.ClientUniqueId == clientUniqueId && !respondedCsiSpellIds.Contains(start.ServerUniqueId))
                    return start;
            }
        }
    }

    private async Task RunKillObjectiveAsync(QuestObjectiveDefinition objective, CancellationToken cancellationToken)
    {
        if (!levelCheatUsed && CanUseLevelCheat())
        {
            await SendCheatAsync("!character level 10", cancellationToken);
            await DrainWorldPacketsAsync(TimeSpan.FromSeconds(1), cancellationToken);
            levelCheatUsed = true;
        }

        uint[] creatureIds = GetObjectiveCreatureIds(objective).ToArray();
        if (creatureIds.Length == 0)
            throw new BotFailureException($"target group {objective.Data} resolved to no creature ids");
        var attemptedSpawns = new HashSet<uint>();
        while (!IsObjectiveComplete(objective))
        {
            Spawn spawn = ChooseNearestSpawn(creatureIds, questWorldId, attemptedSpawns);
            attemptedSpawns.Add(spawn.Id);
            TrackedEntity entity = await TeleportAndFindAsync(spawn, cancellationToken);
            uint before = GetRawProgress(objective.Index);
            await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEntitySelect,
                PacketCodec.ClientEntitySelect(entity.Guid), cancellationToken);

            bool confirmed = await FightUntilProgressAsync(objective.Index, before, entity.Guid, cancellationToken);
            if (!confirmed && attemptedSpawns.Count >= SpawnsFor(creatureIds, questWorldId).Count)
                throw new BotFailureException($"target {objective.Data} died without a quest objective update");
        }
    }

    private bool CanUseLevelCheat()
        => questPlan.Where(definition => definition.HasLevelUpperBound)
            .All(definition => finishedQuests.Contains(definition.Id));

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

    private async Task<TrackedEntity?> TeleportAndTryFindAsync(
        Spawn spawn,
        TimeSpan entityTimeout,
        CancellationToken cancellationToken)
    {
        bool includeWorld = currentWorldId != spawn.WorldId;
        if (includeWorld)
            entities.Clear();
        await SendCheatAsync(PacketCodec.TeleportCommand(spawn, includeWorld), cancellationToken);
        if (!includeWorld)
            await WaitForLocalTeleportAsync(cancellationToken);
        lastPosition = new Vector3(spawn.X + 2f, spawn.Y, spawn.Z + 2f);

        DateTimeOffset deadline = DateTimeOffset.UtcNow + entityTimeout;
        while (true)
        {
            TrackedEntity? existing = currentWorldId == spawn.WorldId && !worldLoadPending
                ? entities.FindCreature(spawn.CreatureId, spawn)
                : null;
            if (existing is not null)
                return existing;

            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero || await TryReadWorldPacketAsync(remaining, cancellationToken) is null)
                return null;
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
        currentQuestReport?.Cheats.Add(command);
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
                    // ServerPlayerCreate only comes on the FIRST map add (Player.SendPacketsAfterAddToMap: PreviousMap == null);
                    // after a teleport the server ends the add with ServerPlayerChanged, so that is the "world loaded" signal too.
                    if (worldLoadPending && !enteredWorldSent)
                    {
                        await RequireWorld().SendEncryptedAsync(GameMessageOpcode.ClientEnteredWorld,
                            PacketCodec.ClientEnteredWorld(), cancellationToken);
                        enteredWorldSent = true;
                    }
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
                {
                    uint destroyedGuid = PacketCodec.ParseEntityDestroy(packet.Body);
                    entities.Remove(destroyedGuid);
                    if (!worldLoadPending && destroyedGuid == playerGuid)
                    {
                        botDied = true;
                        if (activeObjective is not null)
                            throw new BotFailureException("bot died");
                    }
                    break;
                }
                case GameMessageOpcode.ServerEntityDeathState:
                {
                    (uint unitId, bool dead) = PacketCodec.ParseEntityDeathState(packet.Body);
                    if (unitId == playerGuid && dead)
                    {
                        botDied = true;
                        if (activeObjective is not null)
                            throw new BotFailureException("bot died");
                    }
                    if (unitId == playerGuid && !dead)
                        botDied = false;
                    break;
                }
                case GameMessageOpcode.ServerResurrectionShow:
                    resurrectionShow = PacketCodec.ParseResurrectionShow(packet.Body);
                    resurrectionShowReceivedAt = DateTimeOffset.UtcNow;
                    break;
                case GameMessageOpcode.ServerQuestInit:
                {
                    QuestInitInfo init = PacketCodec.ParseQuestInit(packet.Body);
                    completedQuests.UnionWith(init.CompletedQuestIds);
                    break;
                }
                case GameMessageOpcode.ServerQuestStateChange:
                {
                    QuestStateUpdate state = PacketCodec.ParseQuestState(packet.Body);
                    if (state.State == QuestState.Completed)
                        completedQuests.Add(state.QuestId);
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
                {
                    string text = PacketCodec.ParseServerChatText(packet.Body);
                    log.Info($"server chat: {text}");
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
        catch (Exception exception) when (packet.Opcode is GameMessageOpcode.ServerEntityCreate or GameMessageOpcode.ServerChat)
        {
            log.Info($"skipped malformed {packet.Opcode} body: {exception.Message}");
        }
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

    private static bool IsDynamicKillObjective(QuestObjectiveDefinition objective)
        => objective.Type is QuestObjectiveType.KillCreature
            or QuestObjectiveType.KillCreature2
            or QuestObjectiveType.KillTargetGroup
            or QuestObjectiveType.KillTargetGroups;

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
