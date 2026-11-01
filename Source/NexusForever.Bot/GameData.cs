using System.Text.RegularExpressions;
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
    uint SourceData,
    uint Count,
    uint WorldLocationId,
    uint RewardTargetGroupId,
    uint EquipItemId,
    string Text);

internal sealed record QuestDefinition(
    ushort Id,
    string Title,
    Quest2Entry Entry,
    IReadOnlyList<QuestObjectiveDefinition> Objectives,
    IReadOnlyList<uint> GiverCreatureIds,
    IReadOnlyList<uint> ReceiverCreatureIds);

internal sealed record PrerequisiteBranchChoice(
    ushort QuestId,
    ushort BranchQuestId,
    string BranchTitle,
    bool HasGiverSpawn,
    bool HasCommunicatorOffer);

internal sealed record CommunicatorOffer(
    uint Id,
    uint WorldId,
    uint WorldZoneId,
    uint MinLevel,
    uint MaxLevel,
    IReadOnlyList<(ushort QuestId, QuestState State)> QuestConditions,
    uint PrerequisiteId);

internal sealed record CreatureActivationData(
    IReadOnlyList<uint> ActivationSpellIds,
    uint UnitVisualTypeId,
    uint ActivationFlags,
    uint ActivateSpellTextId,
    string ActivateSpellText);

internal sealed class BotGameData
{
    private readonly GameTable<Quest2Entry> quests;
    private readonly GameTable<QuestObjectiveEntry> objectives;
    private readonly GameTable<Creature2Entry> creatures;
    private readonly GameTable<CharacterCreationEntry> characterCreations;
    private readonly GameTable<WorldLocation2Entry> worldLocations;
    private readonly GameTable<TargetGroupEntry> targetGroups;
    private readonly GameTable<CommunicatorMessagesEntry>? communicatorMessages;
    private readonly TextTable english;
    private readonly Dictionary<ushort, PrerequisiteBranchChoice> prerequisiteBranchChoices = [];

    public BotGameData(string tablePath, bool allowMissingCommunicatorMessages = false)
    {
        if (!Directory.Exists(tablePath))
            throw new DirectoryNotFoundException($"Game table directory not found: {tablePath}");

        quests = Load<Quest2Entry>(tablePath, "Quest2.tbl");
        objectives = Load<QuestObjectiveEntry>(tablePath, "QuestObjective.tbl");
        creatures = Load<Creature2Entry>(tablePath, "Creature2.tbl");
        characterCreations = Load<CharacterCreationEntry>(tablePath, "CharacterCreation.tbl");
        worldLocations = Load<WorldLocation2Entry>(tablePath, "WorldLocation2.tbl");
        targetGroups = Load<TargetGroupEntry>(tablePath, "TargetGroup.tbl");
        string communicatorPath = Path.Combine(tablePath, "CommunicatorMessages.tbl");
        if (File.Exists(communicatorPath))
            communicatorMessages = new GameTable<CommunicatorMessagesEntry>(communicatorPath);
        else if (!allowMissingCommunicatorMessages)
            throw new FileNotFoundException($"Required game table was not found: {communicatorPath}", communicatorPath);
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
                type == QuestObjectiveType.ActivateEntity && objective.Data == 0u ? EquipItemFromText(text) : 0u,
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
        return new QuestDefinition(id, title, entry, questObjectives, givers, receivers);
    }

    public IReadOnlyList<uint> GetPrerequisiteBranchGiverCreatureIds(RunOptions options)
    {
        IEnumerable<QuestDefinition> roots = options.ZoneId is uint zoneId
            ? quests.Entries
                .Where(entry => entry.WorldZoneId == zoneId && MatchesFaction(entry, options.Faction))
                .Select(entry => GetQuest(checked((ushort)entry.Id)))
            : options.QuestIds.Select(GetQuest);

        var result = new HashSet<uint>();
        var visited = new HashSet<ushort>();
        void Visit(QuestDefinition definition)
        {
            if (!visited.Add(definition.Id))
                return;
            QuestDefinition[] candidates = definition.Entry.PrerequisiteQuests
                .Where(id => id != 0u)
                .Select(id => GetQuest(checked((ushort)id)))
                .ToArray();
            if ((definition.Entry.PrerequisiteFlags & 1u) != 0u)
                result.UnionWith(candidates.SelectMany(candidate => candidate.GiverCreatureIds));
            foreach (QuestDefinition candidate in candidates)
                Visit(candidate);
        }

        foreach (QuestDefinition root in roots)
            Visit(root);
        return result.Order().ToArray();
    }

    public IReadOnlyList<QuestDefinition> GetRunPlan(RunOptions options, IReadOnlySet<uint> spawnedGiverCreatureIds)
    {
        prerequisiteBranchChoices.Clear();
        if (options.ZoneId is null)
        {
            if (options.QuestIds.Count == 1 && options.Chain)
                return GetQuestClosure(options.QuestIds[0], includePrerequisites: true, options.Faction, spawnedGiverCreatureIds);

            Dictionary<ushort, QuestDefinition> requested = options.QuestIds
                .Select(GetQuest)
                .ToDictionary(definition => definition.Id);
            Dictionary<ushort, HashSet<ushort>> requestedDependencies = requested.Values.ToDictionary(
                definition => definition.Id,
                definition => SelectPrerequisites(definition, options.Faction, spawnedGiverCreatureIds)
                    .Select(prerequisite => prerequisite.Id)
                    .Where(requested.ContainsKey)
                    .ToHashSet());
            return TopologicalOrder(requested, requestedDependencies);
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
            var selected = SelectPrerequisites(definition, options.Faction, spawnedGiverCreatureIds).ToArray();
            dependencies[definition.Id] = selected.Select(item => item.Id).ToHashSet();
            foreach (QuestDefinition prerequisite in selected)
                Gather(prerequisite);
            gathering.Remove(definition.Id);
        }

        foreach (QuestDefinition root in roots)
            Gather(root);

        return TopologicalOrder(definitions, dependencies);
    }

    public IReadOnlyList<QuestDefinition> GetQuestClosure(
        ushort requestedId,
        bool includePrerequisites,
        BotFaction faction,
        IReadOnlySet<uint> spawnedGiverCreatureIds)
    {
        var definitions = new Dictionary<ushort, QuestDefinition>();
        var dependencies = new Dictionary<ushort, HashSet<ushort>>();
        var gathering = new HashSet<ushort>();
        void Gather(ushort id)
        {
            if (definitions.ContainsKey(id))
                return;
            if (!gathering.Add(id))
                throw new BotFailureException($"Quest prerequisite cycle includes quest {id}.");

            QuestDefinition definition = GetQuest(id);
            definitions.Add(id, definition);
            QuestDefinition[] selected = includePrerequisites
                ? SelectPrerequisites(definition, faction, spawnedGiverCreatureIds).ToArray()
                : [];
            dependencies[id] = selected.Select(prerequisite => prerequisite.Id).ToHashSet();
            if (includePrerequisites)
            {
                foreach (QuestDefinition prerequisite in selected)
                    Gather(prerequisite.Id);
            }
            gathering.Remove(id);
        }

        Gather(requestedId);
        return TopologicalOrder(definitions, dependencies);
    }

    public IReadOnlyList<uint> ResolveTargetGroup(uint id)
    {
        var result = new List<uint>();
        ResolveTargetGroup(id, new HashSet<uint>(), result);
        return result.Distinct().ToArray();
    }

    public bool IsCreature(uint id) => id != 0u && creatures.GetEntry(id) is not null;

    public IReadOnlyList<uint> GetCreatureIdsFromText(string text)
        => Regex.Matches(text, @"creature=([0-9]{1,9})", RegexOptions.IgnoreCase)
            .Select(match => uint.Parse(match.Groups[1].Value))
            .Distinct()
            .ToArray();

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

    public IReadOnlyList<CommunicatorOffer> GetCommunicatorOffers(ushort questId)
    {
        if (communicatorMessages is null)
            return [];

        return communicatorMessages.Entries
            .Where(entry => entry.QuestIdDelivered == questId)
            .Select(entry => new CommunicatorOffer(
                entry.Id,
                entry.WorldId,
                entry.WorldZoneId,
                entry.MinLevel,
                entry.MaxLevel,
                entry.Quests.Select((id, index) => (Id: id, Index: index))
                    .Where(item => item.Id != 0u)
                    .Select(item => (checked((ushort)item.Id), (QuestState)entry.States[item.Index]))
                    .ToArray(),
                entry.PrerequisiteId))
            .OrderBy(entry => entry.Id)
            .ToArray();
    }

    public CreatureActivationData GetCreatureActivationData(uint creatureId)
    {
        Creature2Entry entry = creatures.GetEntry(creatureId)
            ?? throw new BotFailureException($"Creature2 entry {creatureId} was not found.");
        return new CreatureActivationData(
            entry.Spell4IdActivate.Where(id => id != 0u).ToArray(),
            entry.UnitVisualTypeId,
            entry.ActivationFlags,
            entry.LocalizedTextIdActivateSpellText,
            english.GetEntry(entry.LocalizedTextIdActivateSpellText) ?? "");
    }

    public bool HasCommunicatorTable => communicatorMessages is not null;

    public PrerequisiteBranchChoice? GetPrerequisiteBranchChoice(ushort questId)
        => prerequisiteBranchChoices.GetValueOrDefault(questId);

    private static GameTable<T> Load<T>(string directory, string name) where T : class, new()
        => new(RequiredPath(directory, name));

    private IEnumerable<QuestDefinition> SelectPrerequisites(
        QuestDefinition definition,
        BotFaction faction,
        IReadOnlySet<uint> spawnedGiverCreatureIds)
    {
        QuestDefinition[] candidates = definition.Entry.PrerequisiteQuests
            .Where(id => id != 0u)
            .Select(id => GetQuest(checked((ushort)id)))
            .ToArray();
        if (candidates.Length == 0)
            return candidates;
        if ((definition.Entry.PrerequisiteFlags & 1u) == 0u)
            return candidates;

        QuestDefinition[] compatible = candidates.Where(candidate => MatchesFaction(candidate.Entry, faction)).ToArray();
        QuestDefinition selected = (compatible.Length > 0 ? compatible : candidates)
            .OrderByDescending(candidate => HasAvailableGiver(candidate, spawnedGiverCreatureIds))
            .ThenBy(candidate => candidate.Entry.ConLevel)
            .ThenBy(candidate => candidate.Id)
            .First();
        bool hasSpawn = selected.GiverCreatureIds.Any(spawnedGiverCreatureIds.Contains);
        bool hasCommunicator = GetCommunicatorOffers(selected.Id).Count > 0;
        prerequisiteBranchChoices[definition.Id] = new PrerequisiteBranchChoice(
            definition.Id,
            selected.Id,
            selected.Title,
            hasSpawn,
            hasCommunicator);
        return [selected];
    }

    private bool HasAvailableGiver(QuestDefinition definition, IReadOnlySet<uint> spawnedGiverCreatureIds)
        => definition.GiverCreatureIds.Any(spawnedGiverCreatureIds.Contains)
            || GetCommunicatorOffers(definition.Id).Count > 0;

    private static IReadOnlyList<QuestDefinition> TopologicalOrder(
        IReadOnlyDictionary<ushort, QuestDefinition> definitions,
        IReadOnlyDictionary<ushort, HashSet<ushort>> dependencies)
    {
        var remaining = definitions.Keys.ToHashSet();
        var result = new List<QuestDefinition>(remaining.Count);
        while (remaining.Count > 0)
        {
            QuestDefinition? next = remaining
                .Where(id => dependencies[id].All(dependency => !remaining.Contains(dependency)))
                .Select(id => definitions[id])
                .OrderBy(definition => definition.Entry.ConLevel)
                .ThenBy(definition => definition.Id)
                .FirstOrDefault();
            if (next is null)
                throw new BotFailureException("Quest prerequisite graph contains a cycle.");
            remaining.Remove(next.Id);
            result.Add(next);
        }
        return result;
    }

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

    private static uint EquipItemFromText(string text)
    {
        if (!text.TrimStart().StartsWith("Equip", StringComparison.OrdinalIgnoreCase))
            return 0u;

        uint[] ids = Regex.Matches(text, @"item=([0-9]{1,9})", RegexOptions.IgnoreCase)
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
