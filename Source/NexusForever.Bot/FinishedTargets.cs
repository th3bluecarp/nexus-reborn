using System.Collections;

namespace NexusForever.Bot;

/// <summary>
/// Guids of targets the bot already finished, each pinned to the entity create it saw at the time. The server can reuse a
/// guid for the respawn of the same spawn (the Celestion Zeta Warbot 32583 came back as guid 4159 again), and a plain guid set
/// then excluded the only source of Mechanisms of War's start item for the rest of the hunt: 44 teleports past a live warbot
/// 3 m away. A guid counts as finished only while the tracker still holds the same create.
/// </summary>
internal sealed class FinishedTargets(EntityTracker entities) : IReadOnlySet<uint>
{
    private readonly Dictionary<uint, long> finished = [];

    public void Add(uint guid) => finished[guid] = entities.CreateSerial(guid);

    public bool Contains(uint guid)
        => finished.TryGetValue(guid, out long serial) && entities.CreateSerial(guid) == serial;

    private HashSet<uint> Current() => finished.Keys.Where(Contains).ToHashSet();

    public int Count => Current().Count;
    public IEnumerator<uint> GetEnumerator() => Current().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool IsProperSubsetOf(IEnumerable<uint> other) => Current().IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<uint> other) => Current().IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<uint> other) => Current().IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<uint> other) => Current().IsSupersetOf(other);
    public bool Overlaps(IEnumerable<uint> other) => Current().Overlaps(other);
    public bool SetEquals(IEnumerable<uint> other) => Current().SetEquals(other);
}
