using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusForever.Bot;

internal sealed class RunReport
{
    public ushort QuestId { get; init; }
    public string Title { get; set; } = "";
    public string CharacterName { get; set; } = "";
    public DateTimeOffset StartTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndTimestamp { get; set; }
    public List<StepReport> Steps { get; } = [];
    public List<string> Cheats { get; } = [];
    public List<ObjectiveReport> Objectives { get; } = [];
    public List<QuestRunReport> Quests { get; } = [];
    public List<PathMissionRunReport> Missions { get; } = [];
    public RunSummary Summary { get; private set; } = new();
    public string Result { get; set; } = "FAIL";
    public string? Failure { get; set; }

    public async Task WriteAsync(string path)
    {
        EndTimestamp = DateTimeOffset.UtcNow;
        Summary = new RunSummary
        {
            Total = Quests.Count,
            Passed = Quests.Count(quest => quest.Result == "PASS"),
            Failed = Quests.Count(quest => quest.Result == "FAIL"),
            Skipped = Quests.Count(quest => quest.Result == "SKIPPED"),
            MissionTotal = Missions.Count,
            MissionPassed = Missions.Count(mission => mission.Result == "PASS"),
            MissionFailed = Missions.Count(mission => mission.Result == "FAIL"),
            MissionSkipped = Missions.Count(mission => mission.Result == "SKIPPED"),
            MissionUnsupported = Missions.Count(mission => mission.Result == "UNSUPPORTED"),
            UnsupportedTypes = Quests.SelectMany(quest => quest.Objectives)
                .Where(objective => objective.Failure?.StartsWith("unsupported objective type ", StringComparison.Ordinal) == true)
                .Select(objective => objective.Type)
                .Distinct()
                .OrderBy(value => value)
                .ToArray(),
            StartedAt = StartTimestamp,
            FinishedAt = EndTimestamp,
            Character = CharacterName
        };
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(this, options) + Environment.NewLine);
        await File.WriteAllTextAsync(Path.ChangeExtension(path, ".md"), BuildMarkdown());
    }

    private string BuildMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {Escape(Title)} run report");
        builder.AppendLine();
        builder.AppendLine($"{Summary.Total} quests: {Summary.Passed} passed, {Summary.Failed} failed, {Summary.Skipped} skipped. Character: {Escape(CharacterName)}. Elapsed: {(EndTimestamp - StartTimestamp).TotalSeconds:0.0}s.");
        builder.AppendLine($"{Summary.MissionTotal} path missions: {Summary.MissionPassed} passed, {Summary.MissionFailed} failed, {Summary.MissionSkipped} skipped, {Summary.MissionUnsupported} unsupported.");
        builder.AppendLine();
        builder.AppendLine("Interactability evidence is an approximation from entity create flags/checklist data and Creature2 activation fields received or loaded by the bot.");
        builder.AppendLine();
        builder.AppendLine("## Quests");
        builder.AppendLine();
        builder.AppendLine("| quest | title | result | failed at | detail | note |");
        builder.AppendLine("|---:|---|---|---|---|---|");
        foreach (QuestRunReport quest in Quests)
        {
            StepReport? failedStep = quest.Steps.FirstOrDefault(step => !step.Ok);
            ObjectiveReport? failedObjective = quest.Objectives.FirstOrDefault(objective => !objective.Ok && objective.Failure is not null);
            string failedAt = failedStep?.Name ?? (failedObjective is null ? "" : $"objective {failedObjective.Index}");
            string detail = quest.Failure ?? (quest.Result == "SKIPPED" ? "already completed" : "");
            KillEvidence[] passiveKills = quest.Objectives.SelectMany(objective => objective.Kills)
                .Where(kill => !kill.FoughtBack)
                .ToArray();
            string note = passiveKills.Length == 0
                ? ""
                : $"combat-AI finding: foughtBack=false for {passiveKills.Length} kill(s)";
            builder.AppendLine($"| {quest.QuestId} | {Escape(quest.Title)} | {quest.Result} | {Escape(failedAt)} | {Escape(detail)} | {Escape(note)} |");
        }
        builder.AppendLine();
        builder.AppendLine("Cheats per quest:");
        builder.AppendLine();
        foreach (QuestRunReport quest in Quests)
            builder.AppendLine($"- {quest.QuestId} {Escape(quest.Title)}: {quest.Cheats.Count}");
        builder.AppendLine();
        builder.AppendLine("## Path missions (Soldier)");
        builder.AppendLine();
        builder.AppendLine("| mission | name | type | result | failed at | detail | elapsed | fought back | cheats |");
        builder.AppendLine("|---:|---|---|---|---|---|---:|---|---:|");
        foreach (PathMissionRunReport mission in Missions)
        {
            string foughtBack = mission.FoughtBack.HasValue
                ? $"{mission.FoughtBack.Value.ToString().ToLowerInvariant()} (casts={mission.EnemyCasts}, damage={mission.DamageTaken})"
                : "";
            builder.AppendLine($"| {mission.MissionId} | {Escape(mission.Name)} | {Escape(mission.Type)} | {mission.Result} | {Escape(mission.FailedAt ?? "")} | {Escape(mission.Detail ?? "")} | {mission.ElapsedMs} ms | {Escape(foughtBack)} | {mission.Cheats.Count} |");
        }
        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}

internal sealed class RunSummary
{
    public int Total { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public int MissionTotal { get; init; }
    public int MissionPassed { get; init; }
    public int MissionFailed { get; init; }
    public int MissionSkipped { get; init; }
    public int MissionUnsupported { get; init; }
    public IReadOnlyList<string> UnsupportedTypes { get; init; } = [];
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; init; }
    public string Character { get; init; } = "";
}

internal sealed class PathMissionRunReport
{
    [JsonPropertyName("id")]
    public ushort MissionId { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string Result { get; set; } = "FAIL";
    public string? FailedAt { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset StartTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndTimestamp { get; set; }
    [JsonPropertyName("elapsed")]
    public long ElapsedMs => EndTimestamp == default ? 0L : Math.Max(0L, (long)(EndTimestamp - StartTimestamp).TotalMilliseconds);
    public List<string> Cheats { get; } = [];
    public bool? FoughtBack { get; set; }
    public uint EnemyCasts { get; set; }
    public uint DamageTaken { get; set; }
    public long? FirstEnemyActionMs { get; set; }
    public List<KillEvidence> Kills { get; } = [];
}

internal sealed class QuestRunReport
{
    [JsonPropertyName("id")]
    public ushort QuestId { get; init; }
    public string Title { get; init; } = "";
    public string CharacterName { get; set; } = "";
    public DateTimeOffset StartTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndTimestamp { get; set; }
    public List<StepReport> Steps { get; } = [];
    public List<string> Cheats { get; } = [];
    public List<string> ServerMessages { get; } = [];
    public List<ObjectiveReport> Objectives { get; } = [];
    public string Result { get; set; } = "FAIL";
    public string? Failure { get; set; }
    public bool Skipped { get; set; }
    public long ElapsedMs => EndTimestamp == default ? 0L : Math.Max(0L, (long)(EndTimestamp - StartTimestamp).TotalMilliseconds);
}

internal sealed class StepReport
{
    public required string Name { get; init; }
    public bool Ok { get; init; }
    public long ElapsedMs { get; init; }
    public required string Detail { get; init; }
}

internal sealed class ObjectiveReport
{
    public int Index { get; init; }
    public required string Type { get; init; }
    public uint Data { get; init; }
    public uint Count { get; init; }
    public uint Progress { get; set; }
    public bool Ok { get; set; }
    public string? Failure { get; set; }
    public bool? FoughtBack { get; set; }
    public uint EnemyCasts { get; set; }
    public uint DamageTaken { get; set; }
    public long? FirstEnemyActionMs { get; set; }
    public List<KillEvidence> Kills { get; } = [];
    public List<string> Interactables { get; } = [];
}

internal sealed class KillEvidence
{
    public uint TargetGuid { get; init; }
    public bool FoughtBack { get; init; }
    public uint EnemyCasts { get; init; }
    public uint DamageTaken { get; init; }
    public long? FirstEnemyActionMs { get; init; }
    public List<LootEvidence> Loot { get; } = [];
}

internal sealed class LootEvidence
{
    public uint ItemId { get; init; }
    public uint Count { get; init; }
}

internal sealed class BotFailureException(string message) : Exception(message);

internal sealed class BotLog : IAsyncDisposable
{
    private readonly StreamWriter writer;
    private readonly object sync = new();

    public BotLog(string path)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public void Info(string message)
    {
        string line = $"{DateTimeOffset.UtcNow:O} INFO {message}";
        lock (sync)
            writer.WriteLine(line);
        Console.WriteLine(message);
    }

    public void Debug(string message)
    {
        lock (sync)
            writer.WriteLine($"{DateTimeOffset.UtcNow:O} DEBUG {message}");
    }

    public void Packet(string direction, ushort opcode, int size, string? layer = null)
    {
        string suffix = layer is null ? "" : $" {layer}";
        lock (sync)
            writer.WriteLine($"{DateTimeOffset.UtcNow:O} {direction} opcode=0x{opcode:X4} size={size}{suffix}");
    }

    public ValueTask DisposeAsync()
    {
        lock (sync)
            writer.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal static class TimedStep
{
    public static async Task<string> RunAsync(
        RunReport report,
        string name,
        TimeSpan timeout,
        CancellationToken runCancellation,
        Func<CancellationToken, Task<string>> action)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(runCancellation);
        timeoutSource.CancelAfter(timeout);
        try
        {
            string detail = await action(timeoutSource.Token);
            report.Steps.Add(new StepReport { Name = name, Ok = true, ElapsedMs = stopwatch.ElapsedMilliseconds, Detail = detail });
            return detail;
        }
        catch (Exception exception)
        {
            string detail = exception is OperationCanceledException && !runCancellation.IsCancellationRequested
                ? $"timeout after {timeout.TotalSeconds:0} s"
                : exception.Message;
            report.Steps.Add(new StepReport { Name = name, Ok = false, ElapsedMs = stopwatch.ElapsedMilliseconds, Detail = detail });
            if (exception is OperationCanceledException && !runCancellation.IsCancellationRequested)
                throw new BotFailureException($"{name}: {detail}");
            throw;
        }
    }
}
