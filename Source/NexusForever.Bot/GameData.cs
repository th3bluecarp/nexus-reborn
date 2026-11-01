using System.Text.RegularExpressions;
using NexusForever.Game.Static;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Prerequisite;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.Bot;

internal sealed record QuestObjectiveDefinition(
    int Index,
    QuestObjectiveType Type,
    uint Data,
    uint SourceData,
    uint Count,
    uint WorldLocationId,
    uint RewardTargetGroupId,
    string Text);

internal sealed record QuestDefinition(
    ushort Id,
    string Title,
    Quest2Entry Entry,
    IReadOnlyList<QuestObjectiveDefinition> Objectives,
    IReadOnlyList<uint> GiverCreatureIds,
    IReadOnlyList<uint> ReceiverCreatureIds,
    bool HasLevelUpperBound,
    uint? LevelUpperBound,
    bool NeedsCombat);

internal sealed class BotGameData
{
    private readonly GameTable<Quest2Entry> quests;
    private readonly GameTable<QuestObjectiveEntry> objectives;
    private readonly GameTable<Creature2Entry> creatures;
    private readonly GameTable<CharacterCreationEntry> characterCreations;
    private readonly GameTable<WorldLocation2Entry> worldLocations;
    private readonly GameTable<TargetGroupEntry> targetGroups;
    private readonly GameTable<PrerequisiteEntry> prerequisites;
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
        prerequisites = Load<PrerequisiteEntry>(tablePath, "Prerequisite.tbl");
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
            QuestObjectiveType type = (QuestObjectiveType)objective.Type;
            string text = english.GetEntry(objective.LocalizedTextIdFull) ?? "";
            uint data = objective.Data;
            if (data == 0u && type is QuestObjectiveType.ActivateEntity or QuestObjectiveType.SucceedCSI or QuestObjectiveType.KillCreature)
                data = CreatureFromText(text);
            uint locationId = new[]
            {
                objective.WorldLocationsIdIndicator00,
                objective.WorldLocationsIdIndicator01,
                objective.WorldLocationsIdIndicator02,
                objective.WorldLocationsIdIndicator03
            }.FirstOrDefault(value => value != 0);
            questObjectives.Add(new QuestObjectiveDefinition(
                index++,
                type,
                data,
                objective.Data,
                objective.Count,
                locationId,
                objective.TargetGroupIdRewardPane,
                text));
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
        uint? levelUpperBound = GetLevelUpperBound(entry);
        bool needsCombat = questObjectives.Any(objective => objective.Type is
            QuestObjectiveType.KillCreature or QuestObjectiveType.KillCreature2 or
            QuestObjectiveType.KillTargetGroup or QuestObjectiveType.KillTargetGroups or
            QuestObjectiveType.VirtualCollect or QuestObjectiveType.GatheResource);
        return new QuestDefinition(id, title, entry, questObjectives, givers, receivers,
            levelUpperBound.HasValue, levelUpperBound, needsCombat);
    }

    public IReadOnlyList<QuestDefinition> GetRunPlan(RunOptions options)
    {
        if (options.ZoneId is null)
        {
            if (options.QuestIds.Count == 1 && options.Chain)
                return GetQuestClosure(options.QuestIds[0], includePrerequisites: true);
            return options.QuestIds.Select(GetQuest).ToArray();
        }

        QuestDefinition[] roots = quests.Entries
            .Where(entry => entry.WorldZoneId == options.ZoneId.Value && MatchesFaction(entry, options.Faction))
            .Select(entry => GetQuest(checked((ushort)entry.Id)))
            .ToArray();
        if (roots.Length == 0)
            throw new BotFailureException($"No {options.Faction} quests were found in zone {options.ZoneId}.");

        var definitions = new Dictionary<ushort, QuestDefinition>();
        var dependencies = new Dictionary<ushort, HashSet<ushort>>();
        var gathering = new HashSet<ushort>();

        void Gather(QuestDefinition definition)
        {
            if (definitions.ContainsKey(definition.Id))
                return;
            if (!gathering.Add(definition.Id))
                throw new BotFailureException($"Quest prerequisite cycle includes quest {definition.Id}.");

            definitions.Add(definition.Id, definition);
            var selected = SelectPrerequisites(definition, options.Faction).ToArray();
            dependencies[definition.Id] = selected.Select(item => item.Id).ToHashSet();
            foreach (QuestDefinition prerequisite in selected)
                Gather(prerequisite);
            gathering.Remove(definition.Id);
        }

        foreach (QuestDefinition root in roots)
            Gather(root);

        var remaining = definitions.Keys.ToHashSet();
        var result = new List<QuestDefinition>(remaining.Count);
        while (remaining.Count > 0)
        {
            QuestDefinition? next = remaining
                .Where(id => dependencies[id].All(dependency => !remaining.Contains(dependency)))
                .Select(id => definitions[id])
                .OrderBy(OrderBucket)
                .ThenBy(definition => definition.Entry.ConLevel)
                .ThenBy(definition => definition.Id)
                .FirstOrDefault();
            if (next is null)
                throw new BotFailureException("Quest prerequisite graph contains a cycle.");
            remaining.Remove(next.Id);
            result.Add(next);
        }

        return result;
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

    public uint GetObjectiveTargetGroup(QuestObjectiveDefinition objective)
    {
        if ((objective.Type is QuestObjectiveType.VirtualCollect or QuestObjectiveType.Unknown28)
            && objective.RewardTargetGroupId != 0u
            && ResolveTargetGroup(objective.RewardTargetGroupId).Count > 0)
            return objective.RewardTargetGroupId;
        if (ResolveTargetGroup(objective.Data).Count > 0)
            return objective.Data;
        if (objective.RewardTargetGroupId != 0u && ResolveTargetGroup(objective.RewardTargetGroupId).Count > 0)
            return objective.RewardTargetGroupId;
        return objective.Data;
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

    public CharacterCreationEntry GetBotCharacterCreation(BotCharacterStart start, BotFaction faction)
    {
        Faction requiredFaction = faction == BotFaction.Exile ? Faction.Exile : Faction.Dominion;
        CharacterCreationEntry[] candidates = characterCreations.Entries
            .Where(entry => entry.Enabled
                && entry.FactionId == requiredFaction
                && entry.ClassId == (uint)Class.Warrior
                && entry.Sex == Sex.Male)
            .ToArray();

        _ = start;
        CharacterCreationStart required = CharacterCreationStart.PreTutorial; // "Novice" start: entry 467, level 1, world 3460.
        return candidates.FirstOrDefault(entry => entry.CharacterCreationStartEnum == required)
            ?? throw new BotFailureException($"No enabled {requiredFaction} male Warrior {required} CharacterCreation entry was found.");
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

    private IEnumerable<QuestDefinition> SelectPrerequisites(QuestDefinition definition, BotFaction faction)
    {
        QuestDefinition[] candidates = definition.Entry.PrerequisiteQuests
            .Where(id => id != 0u)
            .Select(id => GetQuest(checked((ushort)id)))
            .ToArray();
        if ((definition.Entry.PrerequisiteFlags & 1u) == 0u)
            return candidates;

        QuestDefinition[] compatible = candidates.Where(candidate => MatchesFaction(candidate.Entry, faction)).ToArray();
        return (compatible.Length > 0 ? compatible : candidates)
            .OrderBy(OrderBucket)
            .ThenBy(candidate => candidate.Entry.ConLevel)
            .ThenBy(candidate => candidate.Id)
            .Take(1);
    }

    private uint? GetLevelUpperBound(Quest2Entry entry)
    {
        if (entry.PrerequisiteId == 0u)
            return null;
        PrerequisiteEntry? prerequisite = prerequisites.GetEntry(entry.PrerequisiteId);
        if (prerequisite is null)
            return null;

        uint? result = null;
        for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
        {
            if (prerequisite.PrerequisiteTypeId[i] != PrerequisiteType.Level
                || prerequisite.PrerequisiteComparisonId[i] != PrerequisiteComparison.LessThanOrEqual)
                continue;
            result = result is null ? prerequisite.Value[i] : Math.Min(result.Value, prerequisite.Value[i]);
        }
        return result;
    }

    private static int OrderBucket(QuestDefinition definition)
        => definition.HasLevelUpperBound ? 0 : definition.NeedsCombat ? 2 : 1;

    private static bool MatchesFaction(Quest2Entry entry, BotFaction faction)
        => entry.QuestPlayerFactionEnum switch
        {
            0u => faction == BotFaction.Exile,
            1u => faction == BotFaction.Dominion,
            _ => true
        };

    private static uint CreatureFromText(string text)
    {
        uint[] ids = Regex.Matches(text, @"creature=([0-9]{1,9})", RegexOptions.IgnoreCase)
            .Select(match => uint.Parse(match.Groups[1].Value))
            .Distinct()
            .ToArray();
        return ids.Length == 1 ? ids[0] : 0u;
    }

    private static string RequiredPath(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Required game table was not found: {path}", path);
        return path;
    }
}
