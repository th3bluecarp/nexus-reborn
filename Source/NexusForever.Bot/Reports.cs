using System.Diagnostics;
using System.Text.Json;

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
    public string Result { get; set; } = "FAIL";
    public string? Failure { get; set; }

    public async Task WriteAsync(string path)
    {
        EndTimestamp = DateTimeOffset.UtcNow;
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(this, options) + Environment.NewLine);
    }
}

internal sealed class QuestRunReport
{
    public ushort QuestId { get; init; }
    public string Title { get; init; } = "";
    public string CharacterName { get; set; } = "";
    public DateTimeOffset StartTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndTimestamp { get; set; }
    public List<StepReport> Steps { get; } = [];
    public List<string> Cheats { get; } = [];
    public List<ObjectiveReport> Objectives { get; } = [];
    public string Result { get; set; } = "FAIL";
    public string? Failure { get; set; }
    public bool Skipped { get; set; }
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
