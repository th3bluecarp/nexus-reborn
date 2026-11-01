using NexusForever.Game.Static;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.Bot;

internal sealed record QuestObjectiveDefinition(
    int Index,
    QuestObjectiveType Type,
    uint Data,
    uint Count,
    uint WorldLocationId);

internal sealed record QuestDefinition(
    ushort Id,
    string Title,
    Quest2Entry Entry,
    IReadOnlyList<QuestObjectiveDefinition> Objectives,
    IReadOnlyList<uint> GiverCreatureIds,
    IReadOnlyList<uint> ReceiverCreatureIds);

internal sealed class BotGameData
{
    private readonly GameTable<Quest2Entry> quests;
    private readonly GameTable<QuestObjectiveEntry> objectives;
    private readonly GameTable<Creature2Entry> creatures;
    private readonly GameTable<CharacterCreationEntry> characterCreations;
    private readonly GameTable<WorldLocation2Entry> worldLocations;
    private readonly GameTable<TargetGroupEntry> targetGroups;
    private readonly TextTable english;

    public BotGameData(string tablePath)
    {
        if (!Directory.Exists(tablePath))
            throw new DirectoryNotFoundException($"Game table directory not found: {tablePath}");

        quests = Load<Quest2Entry>(tablePath, "Quest2.tbl");
        objectives = Load<QuestObjectiveEntry>(tablePath, "QuestObjective.tbl");
        creatures = Load<Creature2Entry>(tablePath, "Creature2.tbl");
        characterCreations = Load<CharacterCreationEntry>(tablePath, "CharacterCreation.tbl");
        worldLocations = Load<WorldLocation2Entry>(tablePath, "WorldLocation2.tbl");
        targetGroups = Load<TargetGroupEntry>(tablePath, "TargetGroup.tbl");
        english = new TextTable(RequiredPath(tablePath, "en-US.bin"));
    }

    public QuestDefinition GetQuest(ushort id)
    {
        Quest2Entry entry = quests.GetEntry(id)
            ?? throw new BotFailureException($"Quest2 entry {id} was not found.");

        var questObjectives = new List<QuestObjectiveDefinition>();
        int index = 0;
        foreach (uint objectiveId in entry.Objectives.Where(value => value != 0))
        {
            QuestObjectiveEntry objective = objectives.GetEntry(objectiveId)
                ?? throw new BotFailureException($"QuestObjective entry {objectiveId} was not found.");
            uint locationId = new[]
            {
                objective.WorldLocationsIdIndicator00,
                objective.WorldLocationsIdIndicator01,
                objective.WorldLocationsIdIndicator02,
                objective.WorldLocationsIdIndicator03
            }.FirstOrDefault(value => value != 0);
            questObjectives.Add(new QuestObjectiveDefinition(
                index++,
                (QuestObjectiveType)objective.Type,
                objective.Data,
                objective.Count,
                locationId));
        }

        uint[] givers = creatures.Entries
            .Where(creature => creature.QuestIdGiven.Contains(id))
            .Select(creature => creature.Id)
            .Distinct()
            .ToArray();
        uint[] receivers = creatures.Entries
            .Where(creature => creature.QuestIdReceive.Contains(id))
            .Select(creature => creature.Id)
            .Distinct()
            .ToArray();

        string title = english.GetEntry(entry.LocalizedTextIdTitle) ?? $"Quest {id}";
        return new QuestDefinition(id, title, entry, questObjectives, givers, receivers);
    }

    public IReadOnlyList<QuestDefinition> GetQuestClosure(ushort requestedId, bool includePrerequisites)
    {
        var result = new List<QuestDefinition>();
        var visited = new HashSet<ushort>();
        var visiting = new HashSet<ushort>();

        void Visit(ushort id)
        {
            if (visited.Contains(id))
                return;
            if (!visiting.Add(id))
                throw new BotFailureException($"Quest prerequisite cycle includes quest {id}.");

            QuestDefinition definition = GetQuest(id);
            if (includePrerequisites)
            {
                uint[] prerequisiteIds = definition.Entry.PrerequisiteQuests.Where(value => value != 0).ToArray();
                if ((definition.Entry.PrerequisiteFlags & 1u) != 0 && prerequisiteIds.Length > 1)
                    prerequisiteIds = [prerequisiteIds[0]];
                foreach (uint prerequisiteId in prerequisiteIds)
                {
                    if (prerequisiteId > ushort.MaxValue)
                        throw new BotFailureException($"Quest {id} has invalid prerequisite quest id {prerequisiteId}.");
                    Visit((ushort)prerequisiteId);
                }
            }

            visiting.Remove(id);
            visited.Add(id);
            result.Add(definition);
        }

        Visit(requestedId);
        return result;
    }

    public IReadOnlyList<uint> ResolveTargetGroup(uint id)
    {
        var result = new List<uint>();
        ResolveTargetGroup(id, new HashSet<uint>(), result);
        return result.Distinct().ToArray();
    }

    private void ResolveTargetGroup(uint id, HashSet<uint> visited, List<uint> result)
    {
        if (!visited.Add(id))
            return;

        TargetGroupEntry? entry = targetGroups.GetEntry(id);
        if (entry is null)
            return;

        switch ((TargetGroupType)entry.Type)
        {
            case TargetGroupType.CreatureIdGroup:
                result.AddRange(entry.DataEntries.Where(value => value != 0));
                break;
            case TargetGroupType.OtherTargetGroup:
            case TargetGroupType.OtherTargetGroupCreatures:
                foreach (uint childId in entry.DataEntries.Where(value => value != 0))
                    ResolveTargetGroup(childId, visited, result);
                break;
        }
    }

    public CharacterCreationEntry GetBotCharacterCreation(BotCharacterStart start)
    {
        CharacterCreationEntry[] candidates = characterCreations.Entries
            .Where(entry => entry.Enabled
                && entry.FactionId == Faction.Exile
                && entry.RaceId == Race.Human
                && entry.ClassId == (uint)Class.Warrior
                && entry.Sex == Sex.Male)
            .ToArray();

        CharacterCreationStart required = start == BotCharacterStart.Arkship
            ? CharacterCreationStart.PreTutorial   // "Novice" start: entry 467, 0 XP, arkship world 3460 (the Arkship(0) row is disabled)
            : CharacterCreationStart.Nexus;
        return candidates.FirstOrDefault(entry => entry.CharacterCreationStartEnum == required)
            ?? throw new BotFailureException($"No enabled Exile Human male Warrior {required} CharacterCreation entry was found.");
    }

    public WorldLocation2Entry GetWorldLocation(uint id)
    {
        if (id == 0)
            throw new BotFailureException("Objective has no WorldLocation2 indicator.");
        return worldLocations.GetEntry(id)
            ?? throw new BotFailureException($"WorldLocation2 entry {id} was not found.");
    }

    private static GameTable<T> Load<T>(string directory, string name) where T : class, new()
        => new(RequiredPath(directory, name));

    private static string RequiredPath(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Required game table was not found: {path}", path);
        return path;
    }
}
