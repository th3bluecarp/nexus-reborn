using System.Net;

namespace NexusForever.Bot;

internal sealed record PrepOptions(string Database, string Email);

internal sealed record RunOptions(
    IReadOnlyList<ushort> QuestIds,
    uint? ZoneId,
    BotFaction Faction,
    bool Chain,
    BotCharacterStart Start,
    Endpoint Auth,
    Endpoint World,
    string Database,
    string Tables,
    string Email,
    Guid Token,
    string Report,
    string Log,
    int MaxMinutes);

internal enum BotFaction
{
    Exile,
    Dominion
}

internal enum BotCharacterStart
{
    Arkship
}

internal sealed record Endpoint(string Host, int? Port)
{
    public int GetPort(int defaultPort) => Port ?? defaultPort;

    public override string ToString() => Port is null ? Host : $"{Host}:{Port}";

    public static Endpoint Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Endpoint cannot be empty.");

        if (value[0] == '[')
        {
            int close = value.IndexOf(']');
            if (close < 0)
                throw new ArgumentException($"Invalid endpoint: {value}");

            string host = value[1..close];
            if (close == value.Length - 1)
                return new Endpoint(host, null);
            if (value[close + 1] != ':' || !int.TryParse(value[(close + 2)..], out int bracketPort))
                throw new ArgumentException($"Invalid endpoint: {value}");
            return Validated(host, bracketPort);
        }

        int colon = value.LastIndexOf(':');
        if (colon > 0 && value.IndexOf(':') == colon && int.TryParse(value[(colon + 1)..], out int port))
            return Validated(value[..colon], port);

        return new Endpoint(value, null);
    }

    private static Endpoint Validated(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host) || port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
            throw new ArgumentException($"Invalid endpoint: {host}:{port}");
        return new Endpoint(host, port);
    }
}

internal static class CliOptions
{
    public const string Usage = """
        Usage:
          NexusForever.Bot prep --db <connection-string> --email <email>
          NexusForever.Bot info --tables <path> --quest <id[,id...]> [--chain 1]
          NexusForever.Bot run (--zone <id> --faction exile|dominion | --quest <id[,id...]>) [--chain 0|1] [--start arkship] [--max-minutes <n>] --auth <host:port> --world <host[:port]> --db <connection-string> --tables <path> --email <email> --token <guid> --report <file.json> --log <file.log>
        """;

    public static object Parse(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException(Usage);

        Dictionary<string, string> values = ParsePairs(args[1..]);
        return args[0].ToLowerInvariant() switch
        {
            "prep" => ParsePrep(values),
            "run" => ParseRun(values),
            "info" => ParseInfo(values),
            _ => throw new ArgumentException(Usage)
        };
    }

    private static InfoOptions ParseInfo(Dictionary<string, string> values)
    {
        RequireOnly(values, "tables", "quest", "chain");
        List<ushort> ids = Required(values, "quest").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ushort.Parse).ToList();
        bool chain = values.TryGetValue("chain", out string? c) && c is "1" or "true";
        return new InfoOptions(Required(values, "tables"), ids, chain);
    }

    private static PrepOptions ParsePrep(Dictionary<string, string> values)
    {
        RequireOnly(values, "db", "email");
        return new PrepOptions(Required(values, "db"), Required(values, "email"));
    }

    private static RunOptions ParseRun(Dictionary<string, string> values)
    {
        RequireOnly(values, "quest", "zone", "faction", "chain", "start", "max-minutes", "auth", "world", "db", "tables", "email", "token", "report", "log");

        bool hasQuest = values.TryGetValue("quest", out string? questValue);
        bool hasZone = values.TryGetValue("zone", out string? zoneValue);
        if (hasQuest == hasZone)
            throw new ArgumentException("Specify exactly one of --quest or --zone.");

        List<ushort> questIds = [];
        uint? zoneId = null;
        if (hasQuest)
        {
            foreach (string item in questValue!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!ushort.TryParse(item, out ushort questId) || questId == 0)
                    throw new ArgumentException("--quest must be a comma-separated list of positive 16-bit integers.");
                questIds.Add(questId);
            }
            if (questIds.Count == 0 || questIds.Count != questIds.Distinct().Count())
                throw new ArgumentException("--quest must contain one or more distinct quest ids.");
        }
        else
        {
            if (!uint.TryParse(zoneValue, out uint parsedZone) || parsedZone == 0)
                throw new ArgumentException("--zone must be a positive integer.");
            zoneId = parsedZone;
        }

        BotFaction faction = values.GetValueOrDefault("faction", "exile").ToLowerInvariant() switch
        {
            "exile" => BotFaction.Exile,
            "dominion" => BotFaction.Dominion,
            _ => throw new ArgumentException("--faction must be exile or dominion.")
        };
        if (hasZone && !values.ContainsKey("faction"))
            throw new ArgumentException("--faction is required with --zone.");
        if (!Guid.TryParse(Required(values, "token"), out Guid token))
            throw new ArgumentException("--token must be a GUID.");

        bool chain = values.TryGetValue("chain", out string? chainValue) switch
        {
            false => false,
            true when chainValue is "1" or "true" => true,
            true when chainValue is "0" or "false" => false,
            _ => throw new ArgumentException("--chain must be 0 or 1.")
        };
        BotCharacterStart start = values.GetValueOrDefault("start", "arkship").ToLowerInvariant() switch
        {
            "arkship" => BotCharacterStart.Arkship,
            _ => throw new ArgumentException("--start must be arkship; zone runs always use the novice PreTutorial start.")
        };
        if (!int.TryParse(values.GetValueOrDefault("max-minutes", "180"), out int maxMinutes) || maxMinutes <= 0)
            throw new ArgumentException("--max-minutes must be a positive integer.");

        return new RunOptions(
            questIds,
            zoneId,
            faction,
            chain,
            start,
            Endpoint.Parse(Required(values, "auth")),
            Endpoint.Parse(Required(values, "world")),
            Required(values, "db"),
            Required(values, "tables"),
            Required(values, "email"),
            token,
            Required(values, "report"),
            Required(values, "log"),
            maxMinutes);
    }

    private static Dictionary<string, string> ParsePairs(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(Usage);
            string key = args[i][2..];
            if (!result.TryAdd(key, args[i + 1]))
                throw new ArgumentException($"Duplicate option --{key}.");
        }
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string name)
    {
        if (!values.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing --{name}.\n{Usage}");
        return value;
    }

    private static void RequireOnly(IReadOnlyDictionary<string, string> values, params string[] allowed)
    {
        HashSet<string> names = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? unexpected = values.Keys.FirstOrDefault(k => !names.Contains(k));
        if (unexpected is not null)
            throw new ArgumentException($"Unknown option --{unexpected}.\n{Usage}");
    }
}
