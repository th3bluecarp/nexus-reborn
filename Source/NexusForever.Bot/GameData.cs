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
    uint EquipItemId,
    string Text,
    uint Flags = 0u);

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

internal enum BotPath : uint
{
    Soldier = 0,
    Settler = 1,
    Scientist = 2,
    Explorer = 3
}

// PathMission.tbl stores this client enum as an integer. The server currently implements the
// named values below; the unused numeric slots are kept so an unfamiliar row is reported instead
// of accidentally being treated as another mission kind.
internal enum PathMissionType : uint
{
    Holdout = 0,
    Unknown1 = 1,
    Scan = 2,
    StakingClaim = 3,
    Assassination = 4,
    Demolition = 5,
    Rescue = 6,
    SWAT = 7,
    Unknown8 = 8,
    Unknown9 = 9,
    Unknown10 = 10,
    Unknown11 = 11,
    Surveillance = 12,
    ScavengerHunt = 13,
    Checklist = 14,
    Vista = 15,
    Cartography = 16,
    Exploration = 17,
    Tracking = 18,
    Expansion = 19,
    FieldStudy = 20,
    Infrastructure = 21,
    Experimentation = 22,
    SpecimenSurvey = 23,
    DatacubeDiscovery = 24,
    Mayor = 25,
    Sheriff = 26,
    Scout = 27
}

internal sealed record PathMissionDefinition(
    ushort Id,
    string Name,
    BotPath Path,
    PathMissionType Type,
    uint RawType,
    PathEpisodeEntry Episode,
    PathMissionEntry Entry,
    IReadOnlyList<WorldLocation2Entry> Locations,
    IReadOnlyList<uint> ObjectiveCreatureIds,
    uint Count,
    IReadOnlyList<uint> HoldoutConsoleCreatureIds,
    IReadOnlyList<IReadOnlyList<uint>> ScanCreatureGroups,
    uint ScansPerGroup,
    SettlerHubDefinition? SettlerHub,
    IReadOnlyList<CartographyPoint> CartographyPoints)
{
    public string TypeName => Enum.IsDefined(Type) ? Type.ToString() : $"Unknown{RawType}";
}

internal sealed record CartographyPoint(uint HexGroupId, ushort WorldId, float X, float Y, float Z);
internal sealed record SettlerResourceDefinition(uint ItemId, string Name, IReadOnlyList<uint> CreatureIds);
internal sealed record SettlerBuildDefinition(
    uint GroupId,
    uint DepotCreatureId,
    WorldLocation2Entry DisplayLocation,
    uint Tier,
    uint Contribution,
    IReadOnlyDictionary<uint, uint> Costs);
internal sealed record SettlerHubDefinition(
    uint HubId,
    uint MissionCount,
    IReadOnlyList<SettlerResourceDefinition> Resources,
    IReadOnlyList<SettlerBuildDefinition> Builds);

internal sealed class BotGameData
{
    private readonly GameTable<Quest2Entry> quests;
    private readonly GameTable<PrerequisiteEntry> prerequisites;
    private readonly GameTable<WorldZoneEntry> worldZones;
    private readonly Dictionary<uint, HashSet<uint>> zoneFamilies = [];
    /// <summary>The class every bot character is created with (see GetBotCharacterCreation).</summary>
    public const Class BotClass = Class.Warrior;
    private readonly GameTable<QuestObjectiveEntry> objectives;
    private readonly GameTable<Creature2Entry> creatures;
    private readonly GameTable<Item2Entry> items;
    private readonly GameTable<CharacterCreationEntry> characterCreations;
    private readonly GameTable<WorldLocation2Entry> worldLocations;
    private readonly GameTable<TargetGroupEntry> targetGroups;
    private readonly GameTable<PathMissionEntry> pathMissions;
    private readonly GameTable<PathEpisodeEntry> pathEpisodes;
    private readonly GameTable<PathSoldierEventEntry> pathSoldierEvents;
    private readonly GameTable<PathSoldierAssassinateEntry> pathSoldierAssassinations;
    private readonly GameTable<PathSoldierSWATEntry> pathSoldierSwats;
    private readonly GameTable<PathSoldierActivateEntry> pathSoldierActivations;
    private readonly GameTable<PathExplorerNodeEntry> pathExplorerNodes;
    private readonly GameTable<PathSettlerHubEntry> pathSettlerHubs;
    private readonly GameTable<PathSettlerImprovementGroupEntry> pathSettlerImprovementGroups;
    private readonly GameTable<PathSettlerImprovementEntry> pathSettlerImprovements;
    private readonly GameTable<MapZoneEntry> mapZones;
    private readonly GameTable<MapZoneHexGroupEntry> mapZoneHexGroups;
    private readonly GameTable<MapZoneHexGroupEntryEntry> mapZoneHexGroupEntries;
    private readonly GameTable<CommunicatorMessagesEntry>? communicatorMessages;
    private readonly TextTable english;
    private readonly Dictionary<ushort, PrerequisiteBranchChoice> prerequisiteBranchChoices = [];

    public BotGameData(string tablePath, bool allowMissingCommunicatorMessages = false)
    {
        if (!Directory.Exists(tablePath))
            throw new DirectoryNotFoundException($"Game table directory not found: {tablePath}");

        quests = Load<Quest2Entry>(tablePath, "Quest2.tbl");
        prerequisites = Load<PrerequisiteEntry>(tablePath, "Prerequisite.tbl");
        worldZones = Load<WorldZoneEntry>(tablePath, "WorldZone.tbl");
        objectives = Load<QuestObjectiveEntry>(tablePath, "QuestObjective.tbl");
        creatures = Load<Creature2Entry>(tablePath, "Creature2.tbl");
        items = Load<Item2Entry>(tablePath, "Item2.tbl");
        characterCreations = Load<CharacterCreationEntry>(tablePath, "CharacterCreation.tbl");
        worldLocations = Load<WorldLocation2Entry>(tablePath, "WorldLocation2.tbl");
        targetGroups = Load<TargetGroupEntry>(tablePath, "TargetGroup.tbl");
        pathMissions = Load<PathMissionEntry>(tablePath, "PathMission.tbl");
        pathEpisodes = Load<PathEpisodeEntry>(tablePath, "PathEpisode.tbl");
        pathSoldierEvents = Load<PathSoldierEventEntry>(tablePath, "PathSoldierEvent.tbl");
        pathSoldierAssassinations = Load<PathSoldierAssassinateEntry>(tablePath, "PathSoldierAssassinate.tbl");
        pathSoldierSwats = Load<PathSoldierSWATEntry>(tablePath, "PathSoldierSWAT.tbl");
        pathSoldierActivations = Load<PathSoldierActivateEntry>(tablePath, "PathSoldierActivate.tbl");
        pathExplorerNodes = Load<PathExplorerNodeEntry>(tablePath, "PathExplorerNode.tbl");
        pathSettlerHubs = Load<PathSettlerHubEntry>(tablePath, "PathSettlerHub.tbl");
        pathSettlerImprovementGroups = Load<PathSettlerImprovementGroupEntry>(tablePath, "PathSettlerImprovementGroup.tbl");
        pathSettlerImprovements = Load<PathSettlerImprovementEntry>(tablePath, "PathSettlerImprovement.tbl");
        mapZones = Load<MapZoneEntry>(tablePath, "MapZone.tbl");
        mapZoneHexGroups = Load<MapZoneHexGroupEntry>(tablePath, "MapZoneHexGroup.tbl");
        mapZoneHexGroupEntries = Load<MapZoneHexGroupEntryEntry>(tablePath, "MapZoneHexGroupEntry.tbl");
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
                text,
                objective.Flags));
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

    public uint GetItemActivationQuest(uint itemId)
    {
        Item2Entry entry = items.GetEntry(itemId)
            ?? throw new BotFailureException($"Item2 entry {itemId} was not found.");
        return entry.Quest2IdActivation;
    }

    public IReadOnlyList<PathMissionDefinition> GetPathMissions(
        uint zoneId,
        BotPath? requiredPath = null,
        BotFaction? faction = null)
    {
        IReadOnlySet<uint> zoneFamily = ZoneFamily(zoneId);
        PathEpisodeEntry[] episodes = pathEpisodes.Entries
            .Where(entry => zoneFamily.Contains(entry.WorldZoneId)
                && (!requiredPath.HasValue || entry.PathTypeEnum == (uint)requiredPath.Value))
            .OrderBy(entry => entry.Id)
            .ToArray();
        var result = new List<PathMissionDefinition>();
        foreach (PathEpisodeEntry episode in episodes)
        {
            foreach (PathMissionEntry entry in pathMissions.Entries
                .Where(entry => entry.PathEpisodeId == episode.Id
                    && entry.PathTypeEnum == episode.PathTypeEnum
                    && (!faction.HasValue || MatchesPathFaction(entry, faction.Value)))
                .OrderBy(entry => entry.Id))
            {
                result.Add(CreatePathMission(entry, episode));
            }
        }
        return result;
    }

    private PathMissionDefinition CreatePathMission(PathMissionEntry entry, PathEpisodeEntry episode)
    {
        var type = (PathMissionType)entry.PathMissionTypeEnum;
        uint creatureId = 0u;
        uint targetGroupId = 0u;
        uint count = 1u;
        IReadOnlyList<IReadOnlyList<uint>> scanGroups = [];
        uint scansPerGroup = 0u;
        SettlerHubDefinition? settlerHub = null;
        IReadOnlyList<CartographyPoint> cartographyPoints = [];
        if (entry.PathTypeEnum == (uint)BotPath.Soldier)
        {
            switch (type)
            {
                case PathMissionType.Holdout:
                    _ = pathSoldierEvents.GetEntry(entry.ObjectId);
                    break;
                case PathMissionType.Assassination:
                {
                    PathSoldierAssassinateEntry? objective = pathSoldierAssassinations.GetEntry(entry.ObjectId);
                    if (objective is not null)
                    {
                        creatureId = objective.Creature2Id;
                        targetGroupId = objective.TargetGroupId;
                        count = Math.Max(objective.Count, 1u);
                    }
                    break;
                }
                case PathMissionType.SWAT:
                {
                    PathSoldierSWATEntry? objective = pathSoldierSwats.GetEntry(entry.ObjectId);
                    if (objective is not null)
                    {
                        creatureId = objective.Creature2Id;
                        targetGroupId = objective.TargetGroupId;
                        count = Math.Max(objective.Count, 1u);
                    }
                    break;
                }
                case PathMissionType.Demolition:
                case PathMissionType.Rescue:
                {
                    PathSoldierActivateEntry? objective = pathSoldierActivations.GetEntry(entry.ObjectId);
                    if (objective is not null)
                    {
                        creatureId = objective.Creature2Id;
                        targetGroupId = objective.TargetGroupId;
                        count = Math.Max(objective.Count, 1u);
                    }
                    break;
                }
            }
        }

        if (entry.PathTypeEnum == (uint)BotPath.Scientist && type == PathMissionType.Scan)
        {
            (scanGroups, scansPerGroup) = ResolveScientistScanRule(entry.Id);
            count = Math.Min((uint)scanGroups.Count * scansPerGroup, 32u);
        }

        if (entry.PathTypeEnum == (uint)BotPath.Settler && type == PathMissionType.Expansion)
        {
            settlerHub = ResolveSettlerHub(entry.ObjectId);
            if (settlerHub is not null)
                count = Math.Max(settlerHub.MissionCount, 1u);
        }

        if (entry.PathTypeEnum == (uint)BotPath.Explorer && type == PathMissionType.Cartography)
        {
            count = 95u; // PathMissionManager.CartographyCompletePercent
            cartographyPoints = ResolveCartographyPoints(episode);
        }

        uint[] objectiveCreatures = (creatureId == 0u ? [] : new[] { creatureId })
            .Concat(ResolveTargetGroup(targetGroupId))
            .Distinct()
            .ToArray();
        WorldLocation2Entry[] locations = new[]
            {
                entry.WorldLocation2Id00,
                entry.WorldLocation2Id01,
                entry.WorldLocation2Id02,
                entry.WorldLocation2Id03
            }
            .Where(id => id != 0u)
            .Select(id => worldLocations.GetEntry(id))
            .Where(location => location is not null)
            .Cast<WorldLocation2Entry>()
            .ToArray();
        if (locations.Length == 0
            && entry.PathTypeEnum == (uint)BotPath.Explorer
            && type is PathMissionType.StakingClaim or PathMissionType.Surveillance or PathMissionType.Vista)
        {
            locations = pathExplorerNodes.Entries
                .Where(node => node.PathExplorerAreaId == entry.ObjectId && node.WorldLocation2Id != 0u)
                .Select(node => worldLocations.GetEntry(node.WorldLocation2Id))
                .Where(location => location is not null)
                .Cast<WorldLocation2Entry>()
                .Take(32)
                .ToArray();
        }
        uint[] holdoutConsoles = creatures.Entries
            .Where(creature => creature.PathMissionIdSoldier == entry.Id)
            .Select(creature => creature.Id)
            .Distinct()
            .ToArray();
        return new PathMissionDefinition(
            checked((ushort)entry.Id),
            english.GetEntry(entry.LocalizedTextIdName) ?? $"Mission {entry.Id}",
            (BotPath)entry.PathTypeEnum,
            type,
            entry.PathMissionTypeEnum,
            episode,
            entry,
            locations,
            objectiveCreatures
                .Concat(scanGroups.SelectMany(group => group))
                .Concat(settlerHub?.Resources.SelectMany(resource => resource.CreatureIds) ?? [])
                .Concat(settlerHub?.Builds.Select(build => build.DepotCreatureId) ?? [])
                .Distinct()
                .ToArray(),
            count,
            holdoutConsoles,
            scanGroups,
            scansPerGroup,
            settlerHub,
            cartographyPoints);
    }

    private (IReadOnlyList<IReadOnlyList<uint>> Groups, uint ScansPerGroup) ResolveScientistScanRule(uint missionId)
    {
        (string[][] Groups, uint ScansPerGroup)? rule = missionId switch
        {
            42u => ([
                ["Dominion Turret", "Inactive Dominion Turret"],
                ["Dominion Secure Database", "Dominion Secure Database Hologram"]
            ], 1u),
            160u => ([["Vitalium Crystal"]], 3u),
            648u => ([["Skeech Diviner", "Skeech Scratcher", "Skeech Shaman", "Cleaved Skeech Shaman"]], 3u),
            _ => null
        };
        if (rule is null)
            return ([], 0u);

        IReadOnlyList<IReadOnlyList<uint>> groups = rule.Value.Groups
            .Select(names => (IReadOnlyList<uint>)creatures.Entries
                .Where(creature => names.Contains(CreatureName(creature), StringComparer.OrdinalIgnoreCase))
                .Select(creature => creature.Id)
                .Distinct()
                .Order()
                .ToArray())
            .ToArray();
        return (groups, rule.Value.ScansPerGroup);
    }

    private SettlerHubDefinition? ResolveSettlerHub(uint hubId)
    {
        PathSettlerHubEntry? hub = pathSettlerHubs.GetEntry(hubId);
        if (hub is null)
            return null;

        uint[] itemIds = [hub.Item2IdResource00, hub.Item2IdResource01, hub.Item2IdResource02];
        var resources = new List<SettlerResourceDefinition>();
        foreach (uint itemId in itemIds.Where(id => id != 0u))
        {
            Item2Entry? item = items.GetEntry(itemId);
            string name = item is null ? $"item {itemId}" : NormalizeName(english.GetEntry(item.LocalizedTextIdName) ?? $"item {itemId}");
            uint[] resourceCreatures = creatures.Entries
                .Where(creature => string.Equals(CreatureName(creature), name, StringComparison.OrdinalIgnoreCase))
                .Select(creature => creature.Id)
                .Distinct()
                .Order()
                .ToArray();
            resources.Add(new SettlerResourceDefinition(itemId, name, resourceCreatures));
        }

        var builds = new List<SettlerBuildDefinition>();
        foreach (PathSettlerImprovementGroupEntry group in pathSettlerImprovementGroups.Entries
            .Where(candidate => candidate.PathSettlerHubId == hub.Id)
            .OrderBy(candidate => candidate.Id))
        {
            WorldLocation2Entry? display = worldLocations.GetEntry(group.WorldLocation2IdDisplayPoint);
            if (display is null || group.Creature2IdDepot == 0u)
                continue;

            uint[] tiers = [
                group.PathSettlerImprovementIdTier00,
                group.PathSettlerImprovementIdTier01,
                group.PathSettlerImprovementIdTier02,
                group.PathSettlerImprovementIdTier03
            ];
            for (uint tier = 0u; tier < tiers.Length && tiers[tier] != 0u; tier++)
            {
                PathSettlerImprovementEntry? improvement = pathSettlerImprovements.GetEntry(tiers[tier]);
                if (improvement is null)
                    continue;

                uint[] counts = [improvement.CountResource00, improvement.CountResource01, improvement.CountResource02];
                var costs = new Dictionary<uint, uint>();
                for (int i = 0; i < itemIds.Length; i++)
                    if (itemIds[i] != 0u && counts[i] != 0u)
                        costs[itemIds[i]] = counts[i];

                builds.Add(new SettlerBuildDefinition(
                    group.Id,
                    group.Creature2IdDepot,
                    display,
                    tier,
                    Math.Max(group.ContributionValue + tier * group.PerTierBonusContributionValue, 1u),
                    costs));
            }
        }

        return new SettlerHubDefinition(hub.Id, hub.MissionCount, resources, builds);
    }

    private IReadOnlyList<CartographyPoint> ResolveCartographyPoints(PathEpisodeEntry episode)
    {
        MapZoneEntry? mapZone = mapZones.Entries.FirstOrDefault(zone => zone.WorldZoneId == episode.WorldZoneId);
        if (mapZone is null)
            return [];

        const float zoneMapBoundary = 64f * 512f; // MapDefines.WorldGridOrigin * MapDefines.GridSize
        var points = new List<CartographyPoint>();
        foreach (MapZoneHexGroupEntry group in mapZoneHexGroups.Entries
            .Where(candidate => candidate.MapZoneId == mapZone.Id)
            .OrderBy(candidate => candidate.Id))
        {
            MapZoneHexGroupEntryEntry? hex = mapZoneHexGroupEntries.Entries
                .Where(candidate => candidate.MapZoneHexGroupId == group.Id)
                .OrderBy(candidate => candidate.Id)
                .FirstOrDefault();
            if (hex is null)
                continue;

            float x = 48f * hex.HexX - 16f - zoneMapBoundary;
            float z = 27.712812f * (2f * hex.HexY - 1f) - zoneMapBoundary;
            WorldLocation2Entry? heightAnchor = worldLocations.Entries
                .Where(location => location.WorldId == episode.WorldId)
                .OrderBy(location =>
                {
                    double dx = location.Position0 - x;
                    double dz = location.Position2 - z;
                    return dx * dx + dz * dz;
                })
                .FirstOrDefault();
            float y = heightAnchor?.Position1 ?? 0f;
            points.Add(new CartographyPoint(group.Id, checked((ushort)episode.WorldId), x, y, z));
        }
        return points;
    }

    private string CreatureName(Creature2Entry creature)
        => NormalizeName(english.GetEntry(creature.LocalizedTextIdName) ?? "");

    private static string NormalizeName(string name)
        => Regex.Replace(name, @"\{p:[^}]*\}", "");

    /// <summary>
    /// The zone and every zone below it (WorldZone.ParentZoneId).
    /// </summary>
    public WorldLocation2Entry? TryGetWorldLocation(uint id) => worldLocations.GetEntry(id);

    public IReadOnlySet<uint> ZoneFamily(uint zoneId)
    {
        if (zoneFamilies.TryGetValue(zoneId, out HashSet<uint>? cached))
            return cached;
        var family = new HashSet<uint> { zoneId };
        var frontier = new Queue<uint>();
        frontier.Enqueue(zoneId);
        while (frontier.TryDequeue(out uint parent))
            foreach (WorldZoneEntry child in worldZones.Entries.Where(zone => zone.ParentZoneId == parent && zone.Id != parent))
                if (family.Add(child.Id))
                    frontier.Enqueue(child.Id);
        zoneFamilies[zoneId] = family;
        return family;
    }

    /// <summary>
    /// Why this character never receives the quest, or null: tradeskill work orders (Quest2 type 5), quests gated on a
    /// tradeskill tier and class quests for other classes. A player sees none of these on a Warrior without tradeskills.
    /// </summary>
    public string? QuestSkipReason(Quest2Entry entry)
    {
        if (entry.Type == 5u)
            return "tradeskill work order (Quest2 type 5)";
        if (TradeskillPrerequisiteTier(entry) is uint tier)
            return $"tradeskill quest (prerequisite {entry.PrerequisiteId} needs TradeskillTier {tier})";
        if (entry.PrerequisiteId != 0u && !PrerequisiteAllowsClass(entry.PrerequisiteId, 0))
            return $"class quest (prerequisite {entry.PrerequisiteId} excludes {BotClass})";
        return null;
    }

    private bool PrerequisiteAllowsClass(uint prerequisiteId, int depth)
    {
        PrerequisiteEntry? prerequisite = prerequisites.GetEntry(prerequisiteId);
        if (prerequisite is null || depth > 4)
            return true;
        bool any = false, all = true, evaluated = false;
        for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
        {
            bool met;
            switch (prerequisite.PrerequisiteTypeId[i])
            {
                case PrerequisiteType.Class:
                    met = prerequisite.PrerequisiteComparisonId[i] switch
                    {
                        PrerequisiteComparison.Equal    => prerequisite.Value[i] == (uint)BotClass,
                        PrerequisiteComparison.NotEqual => prerequisite.Value[i] != (uint)BotClass,
                        _                               => true
                    };
                    break;
                case PrerequisiteType.OtherPrerequisite:
                    met = PrerequisiteAllowsClass(prerequisite.ObjectId[i], depth + 1);
                    break;
                case PrerequisiteType.None:
                    continue;
                default:
                    met = true;
                    break;
            }
            evaluated = true;
            any |= met;
            all &= met;
        }
        if (!evaluated)
            return true;
        return prerequisite.Flags == EvaluationMode.EvaluateOR ? any : all;
    }

    /// <summary>
    /// A quest the Prerequisite table requires completed (QuestState 3) and the character has not completed, or null.
    /// </summary>
    public ushort? MissingQuestStatePrerequisite(Quest2Entry entry, Func<ushort, bool> isCompleted)
    {
        if (entry.PrerequisiteId == 0u)
            return null;
        ushort? missing = null;
        bool Evaluate(uint prerequisiteId, int depth)
        {
            PrerequisiteEntry? prerequisite = prerequisites.GetEntry(prerequisiteId);
            if (prerequisite is null || depth > 4)
                return true;
            bool any = false, all = true, evaluated = false;
            for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
            {
                bool met = true;
                switch (prerequisite.PrerequisiteTypeId[i])
                {
                    case PrerequisiteType.QuestState when prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.Equal
                        && prerequisite.Value[i] == 3u && prerequisite.ObjectId[i] != 0u && prerequisite.ObjectId[i] <= ushort.MaxValue:
                        met = isCompleted((ushort)prerequisite.ObjectId[i]);
                        if (!met)
                            missing ??= (ushort)prerequisite.ObjectId[i];
                        break;
                    case PrerequisiteType.OtherPrerequisite:
                        met = Evaluate(prerequisite.ObjectId[i], depth + 1);
                        break;
                    case PrerequisiteType.None:
                        continue;
                }
                evaluated = true;
                any |= met;
                all &= met;
            }
            if (!evaluated)
                return true;
            return prerequisite.Flags == EvaluationMode.EvaluateOR ? any : all;
        }
        return Evaluate(entry.PrerequisiteId, 0) ? null : missing;
    }

    /// <summary>
    /// Quest ids a quest's Prerequisite table entry requires completed (AND trees only; an OR tree is a branch choice the
    /// run leaves to the server).
    /// </summary>
    public IReadOnlyList<ushort> QuestStatePrerequisiteQuests(Quest2Entry entry)
    {
        var result = new List<ushort>();
        void Collect(uint prerequisiteId, int depth)
        {
            PrerequisiteEntry? prerequisite = prerequisites.GetEntry(prerequisiteId);
            if (prerequisite is null || depth > 4 || prerequisite.Flags == EvaluationMode.EvaluateOR)
                return;
            for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
            {
                if (prerequisite.PrerequisiteTypeId[i] == PrerequisiteType.QuestState
                    && prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.Equal
                    && prerequisite.Value[i] == 3u && prerequisite.ObjectId[i] != 0u && prerequisite.ObjectId[i] <= ushort.MaxValue)
                    result.Add((ushort)prerequisite.ObjectId[i]);
                else if (prerequisite.PrerequisiteTypeId[i] == PrerequisiteType.OtherPrerequisite)
                    Collect(prerequisite.ObjectId[i], depth + 1);
            }
        }
        if (entry.PrerequisiteId != 0u)
            Collect(entry.PrerequisiteId, 0);
        return result;
    }

    /// <summary>
    /// The TradeskillTier a quest requires through a TradeSkillProfession prerequisite (Algoroc's tradeskill quest lines),
    /// or null. A character without that tradeskill never receives the quest, so the bot skips it like a player would.
    /// </summary>
    public uint? TradeskillPrerequisiteTier(Quest2Entry entry)
    {
        if (entry.PrerequisiteId == 0u)
            return null;
        PrerequisiteEntry? prerequisite = prerequisites.GetEntry(entry.PrerequisiteId);
        if (prerequisite is null)
            return null;
        for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
        {
            if (prerequisite.PrerequisiteTypeId[i] != PrerequisiteType.TradeSkillProfession)
                continue;
            // reached is 1 or 0: "GreaterThan 0", "GreaterThanOrEqual 1" and "Equal 1" all require the tier
            bool requiresTier = prerequisite.PrerequisiteComparisonId[i] switch
            {
                PrerequisiteComparison.GreaterThan => prerequisite.Value[i] == 0u,
                PrerequisiteComparison.GreaterThanOrEqual => prerequisite.Value[i] >= 1u,
                PrerequisiteComparison.Equal => prerequisite.Value[i] == 1u,
                PrerequisiteComparison.NotEqual => prerequisite.Value[i] == 0u,
                _ => false
            };
            if (requiresTier)
                return prerequisite.ObjectId[i];
        }
        return null;
    }

    public bool IsHoldoutHostile(uint creatureId)
    {
        Creature2Entry? creature = creatures.GetEntry(creatureId);
        return creature is not null
            && (uint)creature.FactionId != 1086u
            && creature.PathMissionIdSoldier == 0u;
    }

    public string GetPathEpisodeName(PathEpisodeEntry episode)
        => english.GetEntry(episode.LocalizedTextIdName) ?? $"Episode {episode.Id}";

    public IReadOnlyList<uint> GetPrerequisiteBranchGiverCreatureIds(RunOptions options)
    {
        IEnumerable<QuestDefinition> roots = options.ZoneId is uint zoneId
            ? quests.Entries
                .Where(entry => ZoneFamily(zoneId).Contains(entry.WorldZoneId) && MatchesFaction(entry, options.Faction))
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

        // a zone's quests sit in its sub zones (Algoroc 13 has 5 child zones and 25 grandchildren; only 57 of its quests name 13)
        IReadOnlySet<uint> zoneFamily = ZoneFamily(options.ZoneId.Value);
        QuestDefinition[] roots = quests.Entries
            .Where(entry => zoneFamily.Contains(entry.WorldZoneId) && MatchesFaction(entry, options.Faction))
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

        // "Novice" start: entry 467, level 1, world 3460 (arkship). Nexus start: entry 339, level 3, straight into Northern Wilds.
        CharacterCreationStart required = start == BotCharacterStart.Nexus ? CharacterCreationStart.Nexus : CharacterCreationStart.PreTutorial;
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
        // quests the Prerequisite table requires completed are prerequisites too (Algoroc chains use it instead of PrerequisiteQuests)
        QuestDefinition[] stateRequired = QuestStatePrerequisiteQuests(definition.Entry)
            .Where(id => quests.GetEntry(id) is not null && candidates.All(candidate => candidate.Id != id))
            .Select(GetQuest)
            .ToArray();
        if (candidates.Length == 0)
            return stateRequired;
        if ((definition.Entry.PrerequisiteFlags & 1u) == 0u)
            return candidates.Concat(stateRequired).ToArray();

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

    private static bool MatchesPathFaction(PathMissionEntry entry, BotFaction faction)
        => entry.PathMissionFactionEnum switch
        {
            1u => faction == BotFaction.Exile,
            2u => faction == BotFaction.Dominion,
            _ => true
        };

    internal static uint CreatureFromText(string text)
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
