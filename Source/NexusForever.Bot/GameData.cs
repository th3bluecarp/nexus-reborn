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

        if (givers.Length == 0)
            throw new BotFailureException($"No Creature2 quest giver was found for quest {id}.");
        if (receivers.Length == 0)
            throw new BotFailureException($"No Creature2 quest receiver was found for quest {id}.");

        string title = english.GetEntry(entry.LocalizedTextIdTitle) ?? $"Quest {id}";
        return new QuestDefinition(id, title, entry, questObjectives, givers, receivers);
    }

    public CharacterCreationEntry GetBotCharacterCreation()
    {
        CharacterCreationEntry[] candidates = characterCreations.Entries
            .Where(entry => entry.Enabled
                && entry.FactionId == Faction.Exile
                && entry.RaceId == Race.Human
                && entry.ClassId == (uint)Class.Warrior
                && entry.Sex == Sex.Male)
            .ToArray();

        return candidates.FirstOrDefault(entry => entry.CharacterCreationStartEnum == CharacterCreationStart.Nexus)
            ?? candidates.FirstOrDefault(entry => entry.CharacterCreationStartEnum == CharacterCreationStart.Arkship)
            ?? throw new BotFailureException("No enabled Exile Human male Warrior Nexus or Arkship CharacterCreation entry was found.");
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
