using System.Numerics;
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
    uint FactionId,
    uint ClassId,
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
    IReadOnlyList<CartographyPoint> CartographyPoints,
    IReadOnlyList<ushort> QuestIds,
    IReadOnlyList<ScavengerClueDefinition> ScavengerClues,
    SettlerInfrastructureDefinition? Infrastructure,
    IReadOnlyList<uint> ExperimentationConsoleCreatureIds,
    bool GadgetTargets = false)
{
    public string TypeName => Enum.IsDefined(Type) ? Type.ToString() : $"Unknown{RawType}";
}

internal sealed record PrerequisiteContext(Class Class, BotPath Path, uint Level, Func<ushort, QuestState> QuestStateOf);
internal sealed record CartographyPoint(uint HexGroupId, ushort WorldId, float X, float Y, float Z);
internal sealed record ScavengerClueDefinition(
    uint Id,
    IReadOnlyList<uint> CreatureIds,
    WorldLocation2Entry? Location,
    float ActiveRadius);
internal sealed record SettlerInfrastructureDefinition(
    uint Id,
    uint DepotCreatureId,
    IReadOnlyList<uint> ResourceCreatureIds,
    uint MaxStackCount,
    uint MissionCount);
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
    private readonly GameTable<Spell4Entry> spells;
    private readonly GameTable<Creature2DifficultyEntry> creatureDifficulties;
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
    private readonly GameTable<PathExplorerActivateEntry> pathExplorerActivations;
    private readonly GameTable<PathExplorerScavengerHuntEntry> pathExplorerScavengerHunts;
    private readonly GameTable<PathExplorerScavengerClueEntry> pathExplorerScavengerClues;
    private readonly GameTable<PathExplorerNodeEntry> pathExplorerNodes;
    private readonly GameTable<PathSettlerHubEntry> pathSettlerHubs;
    private readonly GameTable<PathSettlerImprovementGroupEntry> pathSettlerImprovementGroups;
    private readonly GameTable<PathSettlerImprovementEntry> pathSettlerImprovements;
    private readonly GameTable<PathSettlerMayorEntry> pathSettlerMayors;
    private readonly GameTable<PathSettlerSheriffEntry> pathSettlerSheriffs;
    private readonly GameTable<PathSettlerInfrastructureEntry> pathSettlerInfrastructure;
    private readonly GameTable<PathScientistExperimentationEntry> pathScientistExperimentations;
    private readonly GameTable<PathScientistSpecimenSurveyEntry> pathScientistSpecimenSurveys;
    private readonly GameTable<MapZoneEntry> mapZones;
    private readonly GameTable<MapZoneHexGroupEntry> mapZoneHexGroups;
    private readonly GameTable<MapZoneHexGroupEntryEntry> mapZoneHexGroupEntries;
    private readonly GameTable<CommunicatorMessagesEntry>? communicatorMessages;
    private readonly TextTable english;
    private readonly Dictionary<ushort, PrerequisiteBranchChoice> prerequisiteBranchChoices = [];
    private readonly Lazy<Dictionary<uint, uint[]>> infrastructureCreaturesByActivateSpell;

    private const uint GenericActivateSpell = 1817u;

    public BotGameData(string tablePath, bool allowMissingCommunicatorMessages = false)
    {
        if (!Directory.Exists(tablePath))
            throw new DirectoryNotFoundException($"Game table directory not found: {tablePath}");

        quests = Load<Quest2Entry>(tablePath, "Quest2.tbl");
        questActivationItems = new Lazy<Dictionary<uint, uint>>(() =>
        {
            var map = new Dictionary<uint, uint>();
            foreach (Item2Entry item in items.Entries.Where(item => item.Quest2IdActivation != 0u).OrderBy(item => item.Id))
                map.TryAdd(item.Quest2IdActivation, item.Id);
            return map;
        });
        prerequisites = Load<PrerequisiteEntry>(tablePath, "Prerequisite.tbl");
        worldZones = Load<WorldZoneEntry>(tablePath, "WorldZone.tbl");
        objectives = Load<QuestObjectiveEntry>(tablePath, "QuestObjective.tbl");
        creatures = Load<Creature2Entry>(tablePath, "Creature2.tbl");
        infrastructureCreaturesByActivateSpell = new Lazy<Dictionary<uint, uint[]>>(() =>
            creatures.Entries
                .SelectMany(creature => creature.Spell4IdActivate
                    .Where(spellId => spellId != 0u && spellId != GenericActivateSpell)
                    .Distinct()
                    .Select(spellId => (SpellId: spellId, CreatureId: creature.Id)))
                .GroupBy(pair => pair.SpellId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(pair => pair.CreatureId).Distinct().Order().ToArray()));
        spells = Load<Spell4Entry>(tablePath, "Spell4.tbl");
        creatureDifficulties = Load<Creature2DifficultyEntry>(tablePath, "Creature2Difficulty.tbl");
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
        pathExplorerActivations = Load<PathExplorerActivateEntry>(tablePath, "PathExplorerActivate.tbl");
        pathExplorerScavengerHunts = Load<PathExplorerScavengerHuntEntry>(tablePath, "PathExplorerScavengerHunt.tbl");
        pathExplorerScavengerClues = Load<PathExplorerScavengerClueEntry>(tablePath, "PathExplorerScavengerClue.tbl");
        pathExplorerNodes = Load<PathExplorerNodeEntry>(tablePath, "PathExplorerNode.tbl");
        pathSettlerHubs = Load<PathSettlerHubEntry>(tablePath, "PathSettlerHub.tbl");
        pathSettlerImprovementGroups = Load<PathSettlerImprovementGroupEntry>(tablePath, "PathSettlerImprovementGroup.tbl");
        pathSettlerImprovements = Load<PathSettlerImprovementEntry>(tablePath, "PathSettlerImprovement.tbl");
        pathSettlerMayors = Load<PathSettlerMayorEntry>(tablePath, "PathSettlerMayor.tbl");
        pathSettlerSheriffs = Load<PathSettlerSheriffEntry>(tablePath, "PathSettlerSheriff.tbl");
        pathSettlerInfrastructure = Load<PathSettlerInfrastructureEntry>(tablePath, "PathSettlerInfrastructure.tbl");
        pathScientistExperimentations = Load<PathScientistExperimentationEntry>(tablePath, "PathScientistExperimentation.tbl");
        pathScientistSpecimenSurveys = Load<PathScientistSpecimenSurveyEntry>(tablePath, "PathScientistSpecimenSurvey.tbl");
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
        // an auto-complete quest nobody gives is offered by the object that receives it (GlobalQuestManager does the same)
        if (givers.Length == 0 && (entry.Flags & 1u) != 0u && entry.PrerequisiteItem == 0u)
            givers = receivers;
        // givers the server adds for quests whose offering script is lost (GlobalQuestManager.LostQuestGivers)
        if (givers.Length == 0 && LostQuestGivers.TryGetValue(id, out uint[]? lostGivers))
            givers = lostGivers;

        string title = english.GetEntry(entry.LocalizedTextIdTitle) ?? $"Quest {id}";
        return new QuestDefinition(id, title, entry, questObjectives, givers, receivers);
    }

    private static readonly Dictionary<ushort, uint[]> LostQuestGivers = new()
    {
        [9033] = [31603u] // A Captain in Need: Naida Zavlonov
    };

    /// <summary>
    /// The item whose use starts the quest (Item2.Quest2IdActivation; A Heart of Stone starts from a dropped gronyx heart),
    /// or 0. Quest2.PrerequisiteItem covers the quests that ask for the item in hand instead.
    /// </summary>
    public uint GetQuestActivationItem(ushort questId)
        => questActivationItems.Value.GetValueOrDefault(questId);

    private readonly Lazy<Dictionary<uint, uint>> questActivationItems;

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
        // SWAT missions whose retail gadget (VirtualItemIdDisplay) was used on the targets: the server counts a use of the
        // target as the gadget, and some targets (Fire in the Hole's canimid burrows) cannot be attacked at all
        bool gadgetTargets = false;
        IReadOnlyList<IReadOnlyList<uint>> scanGroups = [];
        uint scansPerGroup = 0u;
        SettlerHubDefinition? settlerHub = null;
        IReadOnlyList<CartographyPoint> cartographyPoints = [];
        IReadOnlyList<ushort> questIds = [];
        IReadOnlyList<ScavengerClueDefinition> scavengerClues = [];
        SettlerInfrastructureDefinition? infrastructure = null;
        IReadOnlyList<uint> experimentationConsoles = [];
        IReadOnlyList<uint> additionalObjectiveCreatures = [];
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
                        // 47 of 55 SWAT entries name a gadget; only the ones whose targets are objects (Creature2 archetype 29: the
                        // burrows, not the Deadstar camps or the yeti of Caving In) are used instead of killed
                        IReadOnlyList<uint> swatTargets = objective.TargetGroupId != 0u ? ResolveTargetGroup(objective.TargetGroupId) : [objective.Creature2Id];
                        gadgetTargets = objective.VirtualItemIdDisplay != 0u && swatTargets.Count > 0
                            && swatTargets.All(id => creatures.GetEntry(id)?.Creature2ArcheTypeId == 29u);
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

        // checklists (type 14) are scans of N different units of the listed kinds on the server (PathMissionManager.Scientist ScanRules)
        if (entry.PathTypeEnum == (uint)BotPath.Scientist && type is PathMissionType.Scan or PathMissionType.Checklist or PathMissionType.FieldStudy)
        {
            (scanGroups, scansPerGroup) = ResolveScientistScanRule(entry.Id);
            count = Math.Min((uint)scanGroups.Count * scansPerGroup, 32u);
        }

        if (entry.PathTypeEnum == (uint)BotPath.Explorer && type == PathMissionType.Exploration)
        {
            PathExplorerActivateEntry? objective = pathExplorerActivations.GetEntry(entry.ObjectId);
            if (objective is not null)
            {
                creatureId = objective.Creature2Id;
                targetGroupId = objective.TargetGroupId;
                count = Math.Max(objective.Count, 1u);
            }
        }

        if (entry.PathTypeEnum == (uint)BotPath.Explorer && type == PathMissionType.ScavengerHunt)
        {
            scavengerClues = ResolveScavengerClues(entry.ObjectId);
            count = (uint)scavengerClues.Count;
            additionalObjectiveCreatures = scavengerClues.SelectMany(clue => clue.CreatureIds).Distinct().ToArray();
        }

        if (entry.PathTypeEnum == (uint)BotPath.Settler && type == PathMissionType.Expansion)
        {
            settlerHub = ResolveSettlerHub(entry.ObjectId);
            if (settlerHub is not null)
                count = Math.Max(settlerHub.MissionCount, 1u);
        }

        if (entry.PathTypeEnum == (uint)BotPath.Settler && type == PathMissionType.Sheriff)
        {
            PathSettlerSheriffEntry? sheriff = pathSettlerSheriffs.GetEntry(entry.ObjectId);
            if (sheriff is not null)
            {
                questIds = new[]
                    {
                        sheriff.Quest2IdSheriff00, sheriff.Quest2IdSheriff01,
                        sheriff.Quest2IdSheriff02, sheriff.Quest2IdSheriff03,
                        sheriff.Quest2IdSheriff04, sheriff.Quest2IdSheriff05,
                        sheriff.Quest2IdSheriff06, sheriff.Quest2IdSheriff07
                    }
                    .Where(id => id != 0u)
                    .Select(id => checked((ushort)id))
                    .ToArray();
                count = (uint)questIds.Count;
            }
        }

        if (entry.PathTypeEnum == (uint)BotPath.Settler && type == PathMissionType.Infrastructure)
        {
            infrastructure = ResolveSettlerInfrastructure(entry.ObjectId);
            if (infrastructure is not null)
            {
                count = Math.Max(infrastructure.MissionCount, 1u);
                additionalObjectiveCreatures = infrastructure.ResourceCreatureIds
                    .Append(infrastructure.DepotCreatureId)
                    .Distinct()
                    .ToArray();
            }
        }

        if (entry.PathTypeEnum == (uint)BotPath.Scientist && type == PathMissionType.Experimentation
            && pathScientistExperimentations.GetEntry(entry.ObjectId) is not null)
        {
            experimentationConsoles = creatures.Entries
                .Where(creature => creature.PathScientistExperimentationId == entry.ObjectId)
                .Select(creature => creature.Id)
                .Distinct()
                .Order()
                .ToArray();
            additionalObjectiveCreatures = experimentationConsoles;
        }

        if (entry.PathTypeEnum == (uint)BotPath.Scientist && type == PathMissionType.DatacubeDiscovery)
        {
            additionalObjectiveCreatures = ResolveDatacubeRule(entry.Id);
            count = (uint)additionalObjectiveCreatures.Count;
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
            // the datacube missions of Algoroc, Celestion, Deradune and Ellevar (3103, 3104, 3105, 2167) carry a location in world 49
            // (InGamePreview), which has no map; a location outside the episode's world is not somewhere to go
            .Where(location => episode.WorldId == 0u || location.WorldId == episode.WorldId)
            .ToArray();
        if (entry.PathTypeEnum == (uint)BotPath.Settler && type == PathMissionType.Mayor
            && pathSettlerMayors.GetEntry(entry.ObjectId) is PathSettlerMayorEntry mayor)
        {
            locations = new[]
                {
                    mayor.WorldLocation2Id00, mayor.WorldLocation2Id01,
                    mayor.WorldLocation2Id02, mayor.WorldLocation2Id03,
                    mayor.WorldLocation2Id04, mayor.WorldLocation2Id05,
                    mayor.WorldLocation2Id06, mayor.WorldLocation2Id07
                }
                .Where(id => id != 0u)
                .Select(id => worldLocations.GetEntry(id))
                .Where(location => location is not null)
                .Cast<WorldLocation2Entry>()
                .ToArray();
        }
        if (entry.PathTypeEnum == (uint)BotPath.Scientist && type == PathMissionType.SpecimenSurvey
            && pathScientistSpecimenSurveys.GetEntry(entry.ObjectId) is PathScientistSpecimenSurveyEntry survey)
        {
            locations = new[]
                {
                    survey.WorldLocation2Id00, survey.WorldLocation2Id01,
                    survey.WorldLocation2Id02, survey.WorldLocation2Id03,
                    survey.WorldLocation2Id04, survey.WorldLocation2Id05,
                    survey.WorldLocation2Id06, survey.WorldLocation2Id07,
                    survey.WorldLocation2Id08, survey.WorldLocation2Id09
                }
                .Where(id => id != 0u)
                .Select(id => worldLocations.GetEntry(id))
                .Where(location => location is not null)
                .Cast<WorldLocation2Entry>()
                .ToArray();
        }
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
        if (type is PathMissionType.Mayor or PathMissionType.Scout or PathMissionType.SpecimenSurvey)
            count = (uint)locations.Length;
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
                .Concat(additionalObjectiveCreatures)
                .Distinct()
                .ToArray(),
            count,
            holdoutConsoles,
            scanGroups,
            scansPerGroup,
            settlerHub,
            cartographyPoints,
            questIds,
            scavengerClues,
            infrastructure,
            experimentationConsoles,
            gadgetTargets);
    }

    private IReadOnlyList<ScavengerClueDefinition> ResolveScavengerClues(uint huntId)
    {
        PathExplorerScavengerHuntEntry? hunt = pathExplorerScavengerHunts.GetEntry(huntId);
        if (hunt is null)
            return [];

        uint[] clueIds =
        [
            hunt.PathExplorerScavengerClueId00, hunt.PathExplorerScavengerClueId01,
            hunt.PathExplorerScavengerClueId02, hunt.PathExplorerScavengerClueId03,
            hunt.PathExplorerScavengerClueId04, hunt.PathExplorerScavengerClueId05,
            hunt.PathExplorerScavengerClueId06
        ];
        return clueIds
            .Where(id => id != 0u)
            .Select(id => pathExplorerScavengerClues.GetEntry(id))
            .Where(clue => clue is not null)
            .Cast<PathExplorerScavengerClueEntry>()
            .Select(clue => new ScavengerClueDefinition(
                clue.Id,
                (clue.Creature2Id == 0u ? [] : new[] { clue.Creature2Id })
                    .Concat(ResolveTargetGroup(clue.TargetGroupId))
                    .Distinct()
                    .ToArray(),
                clue.WorldLocation2IdMiniMap == 0u ? null : worldLocations.GetEntry(clue.WorldLocation2IdMiniMap),
                clue.ActiveRadius))
            .ToArray();
    }

    private SettlerInfrastructureDefinition? ResolveSettlerInfrastructure(uint infrastructureId)
    {
        PathSettlerInfrastructureEntry? infrastructure = pathSettlerInfrastructure.GetEntry(infrastructureId);
        if (infrastructure is null)
            return null;

        uint[] resourceCreatures =
        [
            infrastructure.Creature2IdResource00,
            infrastructure.Creature2IdResource01,
            infrastructure.Creature2IdResource02
        ];
        resourceCreatures = resourceCreatures.Where(id => id != 0u).Distinct().ToArray();
        // same order as PathMissionManager.ResolveResourceCreatures: fields, exact text name, resource spell, singular text name
        string? resourceName = null;
        if (resourceCreatures.Length == 0)
        {
            string objective = english.GetEntry(infrastructure.LocalizedTextIdObjective) ?? "";
            Match match = Regex.Match(objective, @"^Collect (.+?) (?:for|from|to)\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                resourceName = NormalizeName(match.Groups[1].Value);
                resourceCreatures = creatures.Entries
                    .Where(creature =>
                    {
                        string name = CreatureName(creature);
                        return string.Equals(name, resourceName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(name + "s", resourceName, StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(creature => creature.Id)
                    .Distinct()
                    .Order()
                    .ToArray();
            }
        }
        if (resourceCreatures.Length == 0)
        {
            uint[] resourceSpells =
            [
                infrastructure.Spell4IdResource00,
                infrastructure.Spell4IdResource01,
                infrastructure.Spell4IdResource02
            ];
            resourceCreatures = resourceSpells
                .Where(spellId => spellId != 0u && spellId != GenericActivateSpell)
                .Distinct()
                .SelectMany(spellId => infrastructureCreaturesByActivateSpell.Value.GetValueOrDefault(spellId) ?? [])
                .Distinct()
                .Order()
                .ToArray();
        }
        if (resourceCreatures.Length == 0 && resourceName is not null)
        {
            string singularResourceName = SingularizeWords(resourceName);
            resourceCreatures = creatures.Entries
                .Where(creature => string.Equals(
                    SingularizeWords(CreatureName(creature)),
                    singularResourceName,
                    StringComparison.OrdinalIgnoreCase))
                .Select(creature => creature.Id)
                .Distinct()
                .Order()
                .ToArray();
        }

        // Mirror PathMissionManager.Infrastructure.cs: the server currently caps carried resources
        // from the first two MaxStackCountResource fields and always allows at least one.
        uint maxStackCount = Math.Max(
            Math.Max(infrastructure.MaxStackCountResource00, infrastructure.MaxStackCountResource01),
            1u);
        return new SettlerInfrastructureDefinition(
            infrastructure.Id,
            infrastructure.Creature2IdDepot,
            resourceCreatures,
            maxStackCount,
            Math.Max(infrastructure.MissionCount, 1u));
    }

    private static IReadOnlyList<uint> ResolveDatacubeRule(uint missionId)
        => missionId switch
        {
            2136u =>
            [
                21894u, 21896u, 21897u, 21898u, 21899u, 21900u, 21901u,
                21902u, 21903u, 21904u, 21905u, 21906u, 21907u
            ],
            // Celestion, the server's DatacubeRules (PathMissionManager.Discovery)
            3104u =>
            [
                30418u, 30419u, 30420u, 30421u, 30422u, 30423u, 30424u, 30425u,
                30426u, 30427u, 30428u, 30429u, 30430u, 30431u, 30432u, 34503u
            ],
            _ => []
        };

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
            // Algoroc scans (mission type 2), the server's ScanRules names
            264u => ([["Grimstone Digger", "Grimstone Ruffian", "Grimstone Mangler", "Grimstone Savage", "Grimstone Ravager"]], 3u),
            826u => ([["Augmented Slateform Slammer", "Augmented Slateform Charger", "Augmented Slateform Demolisher", "Hulking Gronyx", "Gronyx"]], 3u),
            827u => ([["Pinewrath Barktooth", "Pinewrath Trunksplitter", "Pinewrath Stumpmaker", "Pinewrath Logchomper", "Pinewrath Branchsnapper", "Barktooth Stumpkin", "Trunksplitter Stumpkin"]], 3u),
            201u => ([["Irradiated Icetail Leaper", "Irradiated Icetail Pouncer", "Irradiated Icetail Slayer", "Irradiated Icetail Stalker", "Irradiated Snoglug"]], 3u),
            218u => ([["Large Loftite Formation"]], 3u),
            266u => ([["Fiera Flower"]], 3u),
            1978u => ([["Augmented XAS Scientist"]], 3u),
            2139u => ([["Eldan Repair Construct"]], 3u),
            // Algoroc checklists (mission type 14), the server's ScanRules names
            262u => ([["Darkspur Cartel Terminal"]], 5u),
            701u => ([["Eldan Relic", "Eldan Relics"]], 8u),
            2132u => ([["DREDplex Data Terminal"]], 10u),
            2133u => ([["Deadstar Data Terminal", "Deadstar Footlocker", "Deadstar Clothesline"]], 3u),
            // Celestion scans and checklists, the server's ScanRules names
            1714u => ([["Hoogle Guardian Totem", "Transmorphing Flight Hoogle Totem"]], 3u),
            1719u => ([["Draken Garden Gnome"], ["Draken Backscratcher"], ["Draken Chamberpot"]], 1u),
            1724u => ([["Weave Portal"]], 3u),
            1725u => ([["Moodie Mojo"]], 3u),
            1828u => ([["Augmented Celestial Razortusk", "Augmented Nightfang Creeper", "Augmented Nightfang Weaver", "Augmented Nightfang Widow", "Augmented Buzzbing", "Augmented Nightfang Flayer"]], 3u),
            1830u => ([["Amorous Rowsdower"]], 10u),
            1840u => ([["Rowsdower's Corpse", "Sunhorn Roamer's Corpse", "Ravenous Corpse"]], 3u),
            1841u => ([["Mordesh Vitality Extractor"]], 3u),
            1845u => ([["Water of Life"]], 5u),
            1847u => ([["Ravenous Ripper", "Ravenous Ghoul", "Ravenous Horror", "Ravenous Fiend", "Ravenous Reaver", "Ravenous Malignant", "Ravenous Mordesh"]], 3u),
            1854u => ([["Communication Controls", "Communication Dish"]], 4u),
            1859u => ([["Shield Generator", "Bio-Containment Shield"]], 3u),
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
                .Where(creature => string.Equals(CreatureName(creature), name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(CreatureName(creature) + "s", name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(CreatureName(creature), name + "s", StringComparison.OrdinalIgnoreCase))
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

    private static string SingularizeWords(string value)
    {
        return Regex.Replace(value, @"\b[\p{L}]{4,}\b", match =>
        {
            string word = match.Value;
            if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase))
                return word[..^2];
            if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                return word[..^1];
            return word;
        });
    }

    /// <summary>
    /// The zone and every zone below it (WorldZone.ParentZoneId).
    /// </summary>
    public WorldLocation2Entry? TryGetWorldLocation(uint id) => worldLocations.GetEntry(id);

    /// <summary>
    /// The WorldLocation2 rows of a zone and the zones below it in a world: where the server counts a player as standing in
    /// the zone (within 60 m of one of them) when the map file says nothing about it.
    /// </summary>
    public IReadOnlyList<WorldLocation2Entry> ZoneLocations(uint zoneId, ushort worldId)
    {
        IReadOnlySet<uint> family = ZoneFamily(zoneId);
        return worldLocations.Entries
            .Where(entry => entry.WorldId == worldId && family.Contains(entry.WorldZoneId))
            .OrderBy(entry => entry.Id)
            .ToList();
    }

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
        if (entry.GroupSize >= 2u)
            return $"group quest for {entry.GroupSize}+ players ({english.GetEntry(entry.LocalizedTextIdTitle) ?? $"quest {entry.Id}"}); the bot plays alone";
        if (TradeskillPrerequisiteTier(entry) is uint tier)
            return $"tradeskill quest (prerequisite {entry.PrerequisiteId} needs TradeskillTier {tier})";
        return null;
    }

    /// <summary>
    /// Why this character fails the quest's Prerequisite entry at accept, or null. Only the checks a player could not change
    /// by playing on are judged: class, path, a level window, a quest that must be untouched, a tradeskill tier. A quest that
    /// must be completed is left to the run order, a zone the player must stand in is entered before the accept.
    /// </summary>
    public string? PrerequisiteBlockReason(Quest2Entry entry, PrerequisiteContext context)
    {
        return PrerequisiteBlockReason(entry.PrerequisiteId, context);
    }

    private string? PrerequisiteBlockReason(uint prerequisiteId, PrerequisiteContext context)
    {
        if (prerequisiteId == 0u)
            return null;
        var reasons = new List<string>();
        bool Evaluate(uint prerequisiteId, int depth)
        {
            PrerequisiteEntry? prerequisite = prerequisites.GetEntry(prerequisiteId);
            if (prerequisite is null || depth > 4)
                return true;
            bool any = false, all = true, evaluated = false;
            for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
            {
                PrerequisiteComparison comparison = prerequisite.PrerequisiteComparisonId[i];
                uint value = prerequisite.Value[i];
                uint objectId = prerequisite.ObjectId[i];
                bool met = true;
                switch (prerequisite.PrerequisiteTypeId[i])
                {
                    case PrerequisiteType.None:
                        continue;
                    case PrerequisiteType.Level:
                        met = Compare(context.Level, comparison, value);
                        if (!met)
                            reasons.Add($"level must be {Describe(comparison, value)}, character is {context.Level}");
                        break;
                    case PrerequisiteType.Class:
                        met = Compare((uint)context.Class, comparison, value);
                        if (!met)
                            reasons.Add($"class must be {Describe(comparison, value)} ({(Class)value}), character is {context.Class}");
                        break;
                    case PrerequisiteType.Path:
                        met = Compare((uint)context.Path, comparison, value);
                        if (!met)
                            reasons.Add($"path must be {Describe(comparison, value)} ({(NexusForever.Game.Static.PlayerPath.Path)(byte)value}), character is {context.Path}");
                        break;
                    // Achieved (2) or Completed (3): a quest that must be done is ordered before this one, or reported as
                    // blocking (Rummage for Clues wants Slushed Away achieved or completed, an OR of the two states)
                    case PrerequisiteType.QuestState when comparison == PrerequisiteComparison.Equal && value is 2u or 3u:
                        met = true;
                        break;
                    case PrerequisiteType.QuestState when objectId != 0u && objectId <= ushort.MaxValue:
                        QuestState state = context.QuestStateOf((ushort)objectId);
                        met = Compare((uint)state, comparison, value);
                        if (!met)
                            reasons.Add($"quest {objectId} state must be {Describe(comparison, value)}, is {state}");
                        break;
                    case PrerequisiteType.TradeSkillProfession:
                        met = Compare(0u, comparison, value);
                        if (!met)
                            reasons.Add($"tradeskill tier {objectId} required");
                        break;
                    case PrerequisiteType.UnderSpell:
                        // The zone run starts without encounter auras and has no way to seek one out. Quests offered only
                        // while a spell is active are therefore unavailable, just like tradeskill and public-event quests.
                        met = (value != 0u || objectId != 0u) && comparison == PrerequisiteComparison.NotEqual;
                        if (!met)
                            reasons.Add($"active spell {value} required (prerequisite type 15); the bot has no source for it");
                        break;
                    case PrerequisiteType.OtherPrerequisite:
                        met = Evaluate(objectId, depth + 1);
                        break;
                    case PrerequisiteType.PublicEvent117:
                        // offered only while a public event runs (Algoroc's Loftite Pike); the bot cannot start events
                        met = false;
                        reasons.Add($"public event {objectId} must be running (prerequisite type 117)");
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
        return Evaluate(prerequisiteId, 0)
            ? null
            : $"prerequisite {prerequisiteId}: {string.Join(" / ", reasons.Distinct())}";
    }

    private static bool Compare(uint actual, PrerequisiteComparison comparison, uint value) => comparison switch
    {
        PrerequisiteComparison.Equal              => actual == value,
        PrerequisiteComparison.NotEqual           => actual != value,
        PrerequisiteComparison.GreaterThanOrEqual => actual >= value,
        PrerequisiteComparison.GreaterThan        => actual > value,
        PrerequisiteComparison.LessThanOrEqual    => actual <= value,
        PrerequisiteComparison.LessThan           => actual < value,
        _                                         => true
    };

    private static string Describe(PrerequisiteComparison comparison, uint value) => comparison switch
    {
        PrerequisiteComparison.Equal              => $"= {value}",
        PrerequisiteComparison.NotEqual           => $"!= {value}",
        PrerequisiteComparison.GreaterThanOrEqual => $">= {value}",
        PrerequisiteComparison.GreaterThan        => $"> {value}",
        PrerequisiteComparison.LessThanOrEqual    => $"<= {value}",
        PrerequisiteComparison.LessThan           => $"< {value}",
        _                                         => $"{comparison} {value}"
    };

    /// <summary>
    /// The zone the player must stand in to accept the quest (an InSubZone prerequisite, as the communicator offers on zone entry), or null.
    /// </summary>
    public uint? RequiredSubZone(Quest2Entry entry)
    {
        uint? found = null;
        void Collect(uint prerequisiteId, int depth)
        {
            PrerequisiteEntry? prerequisite = prerequisites.GetEntry(prerequisiteId);
            if (prerequisite is null || depth > 4 || found is not null)
                return;
            for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
            {
                if (prerequisite.PrerequisiteTypeId[i] == PrerequisiteType.InSubZone
                    && prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.Equal && prerequisite.Value[i] != 0u)
                    found ??= prerequisite.Value[i];
                else if (prerequisite.PrerequisiteTypeId[i] == PrerequisiteType.OtherPrerequisite)
                    Collect(prerequisite.ObjectId[i], depth + 1);
            }
        }
        if (entry.PrerequisiteId != 0u)
            Collect(entry.PrerequisiteId, 0);
        return found;
    }

    /// <summary>
    /// A WorldLocation2 inside the zone (or any zone below it) in the world, nearest to the point when one is given.
    /// </summary>
    public WorldLocation2Entry? LocationInZone(uint zoneId, ushort worldId, Vector3? near)
    {
        IReadOnlySet<uint> family = ZoneFamily(zoneId);
        IEnumerable<WorldLocation2Entry> candidates = worldLocations.Entries
            .Where(location => location.WorldId == worldId && family.Contains(location.WorldZoneId));
        return near is Vector3 point
            ? candidates.OrderBy(location => Vector3.DistanceSquared(point, new Vector3(location.Position0, location.Position1, location.Position2))).FirstOrDefault()
            : candidates.OrderBy(location => location.Id).FirstOrDefault();
    }

    /// <summary>
    /// Centre of the map bounds for a zone (or one of its child zones), with height borrowed from the nearest known location
    /// in the same world. This is a last-resort destination for EnterZone objectives with no indicator or zone location.
    /// </summary>
    public WorldLocation2Entry? ZoneMapCentre(uint zoneId, ushort worldId)
    {
        IReadOnlySet<uint> family = ZoneFamily(zoneId);
        MapZoneEntry? mapZone = mapZones.Entries
            .Where(entry => family.Contains(entry.WorldZoneId)
                && entry.HexLimX >= entry.HexMinX && entry.HexLimY >= entry.HexMinY)
            .OrderBy(entry => entry.WorldZoneId == zoneId ? 0 : 1)
            .ThenBy(entry => entry.Id)
            .FirstOrDefault();
        if (mapZone is null)
            return null;

        const float zoneMapBoundary = 64f * 512f; // MapDefines.WorldGridOrigin * MapDefines.GridSize
        float hexX = (mapZone.HexMinX + mapZone.HexLimX) / 2f;
        float hexY = (mapZone.HexMinY + mapZone.HexLimY) / 2f;
        float x = 48f * hexX - 16f - zoneMapBoundary;
        float z = 27.712812f * (2f * hexY - 1f) - zoneMapBoundary;
        WorldLocation2Entry? heightAnchor = worldLocations.Entries
            .Where(location => location.WorldId == worldId)
            .OrderBy(location =>
            {
                double dx = location.Position0 - x;
                double dz = location.Position2 - z;
                return dx * dx + dz * dz;
            })
            .FirstOrDefault();
        if (heightAnchor is null)
            return null;

        return new WorldLocation2Entry
        {
            Id = 0u,
            Position0 = x,
            Position1 = heightAnchor.Position1,
            Position2 = z,
            WorldId = worldId,
            WorldZoneId = zoneId
        };
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
                    // an objective of quest ObjectId complete (Composing a Toxoid and Flush the Ambush follow Predators and Prey): that
                    // quest done satisfies it, and a quest the bot failed blocks this one like a missing state prerequisite
                    case PrerequisiteType.QuestObjective when prerequisite.ObjectId[i] != 0u && prerequisite.ObjectId[i] <= ushort.MaxValue:
                        met = isCompleted((ushort)prerequisite.ObjectId[i]);
                        if (!met)
                            missing ??= (ushort)prerequisite.ObjectId[i];
                        break;
                    // Achieved (2) counts as done: the bot turns a quest in right after it is achieved, and a quest it failed is neither
                    case PrerequisiteType.QuestState when prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.Equal
                        && prerequisite.Value[i] is 2u or 3u && prerequisite.ObjectId[i] != 0u && prerequisite.ObjectId[i] <= ushort.MaxValue:
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
    /// <summary>
    /// Quests a giver creature's visibility prerequisite needs done: AND terms "quest completed" or "quest state not none", and
    /// for an OR prerequisite the first such term (any branch shows the giver).
    /// </summary>
    public IReadOnlyList<ushort> GiverVisibilityPrerequisiteQuests(uint creatureId)
    {
        Creature2Entry? creature = creatures.GetEntry(creatureId);
        if (creature is null || creature.PrerequisiteIdVisibility == 0u)
            return [];
        PrerequisiteEntry? prerequisite = prerequisites.GetEntry(creature.PrerequisiteIdVisibility);
        if (prerequisite is null)
            return [];
        bool any = prerequisite.Flags == EvaluationMode.EvaluateOR;
        var result = new List<ushort>();
        for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
        {
            if (prerequisite.PrerequisiteTypeId[i] != PrerequisiteType.QuestState
                || prerequisite.ObjectId[i] == 0u || prerequisite.ObjectId[i] > ushort.MaxValue)
                continue;
            bool doneSatisfies = prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.Equal && prerequisite.Value[i] == 3u
                || prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.NotEqual && prerequisite.Value[i] == 0u
                || prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.GreaterThanOrEqual && prerequisite.Value[i] <= 3u;
            if (!doneSatisfies)
                continue;
            result.Add((ushort)prerequisite.ObjectId[i]);
            if (any)
                break;
        }
        return result;
    }

    /// <summary>
    /// Quests whose progress (accepting, or for "not Achieved/Completed" terms finishing it, as the run always does) definitely
    /// makes a creature's visibility prerequisite false.
    /// An AND tree is broken by any such term; an OR tree is broken only when every branch requires the same quest to
    /// remain untouched. Other checks are deliberately left unknown rather than guessed.
    /// </summary>
    public IReadOnlyList<ushort> GiverVisibilityEndingQuests(uint creatureId)
    {
        Creature2Entry? creature = creatures.GetEntry(creatureId);
        if (creature is null || creature.PrerequisiteIdVisibility == 0u)
            return [];

        HashSet<ushort> DefiniteBlockers(uint prerequisiteId, HashSet<uint> visited, int depth)
        {
            PrerequisiteEntry? prerequisite = prerequisites.GetEntry(prerequisiteId);
            if (prerequisite is null || depth > 4 || !visited.Add(prerequisiteId))
                return [];

            var branches = new List<HashSet<ushort>>();
            for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
            {
                HashSet<ushort> branch;
                switch (prerequisite.PrerequisiteTypeId[i])
                {
                    // Mentioned reads as Unknown on the server; accepting the quest is the first state change that
                    // makes an "Equal Unknown" visibility term false.
                    case PrerequisiteType.QuestState
                        when prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.Equal
                            && prerequisite.Value[i] == (uint)QuestState.Unknown
                            && prerequisite.ObjectId[i] != 0u
                            && prerequisite.ObjectId[i] <= ushort.MaxValue:
                        branch = [checked((ushort)prerequisite.ObjectId[i])];
                        break;
                    // "QuestState NotEqual Achieved/Completed": the giver leaves once that quest is finished. Alchemist
                    // Polovic (32804, prerequisite 14187 -> 14186) stays only while Horizon (6959) is neither Achieved nor
                    // Completed, and he alone offers Spilling Lifeblood (6964).
                    case PrerequisiteType.QuestState
                        when prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.NotEqual
                            && (prerequisite.Value[i] == (uint)QuestState.Achieved
                                || prerequisite.Value[i] == (uint)QuestState.Completed)
                            && prerequisite.ObjectId[i] != 0u
                            && prerequisite.ObjectId[i] <= ushort.MaxValue:
                        branch = [checked((ushort)prerequisite.ObjectId[i])];
                        break;
                    case PrerequisiteType.OtherPrerequisite
                        when prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.Equal:
                        branch = DefiniteBlockers(prerequisite.ObjectId[i], new HashSet<uint>(visited), depth + 1);
                        break;
                    case PrerequisiteType.None:
                        continue;
                    default:
                        branch = [];
                        break;
                }
                branches.Add(branch);
            }

            if (branches.Count == 0)
                return [];
            if (prerequisite.Flags == EvaluationMode.EvaluateAND)
                return branches.SelectMany(branch => branch).ToHashSet();

            HashSet<ushort> intersection = new(branches[0]);
            foreach (HashSet<ushort> branch in branches.Skip(1))
                intersection.IntersectWith(branch);
            return intersection;
        }

        return DefiniteBlockers(creature.PrerequisiteIdVisibility, [], 0).Order().ToArray();
    }

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
                else if (prerequisite.PrerequisiteTypeId[i] == PrerequisiteType.QuestObjective
                    && prerequisite.ObjectId[i] != 0u && prerequisite.ObjectId[i] <= ushort.MaxValue)
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

    /// <summary>The highest MaxLevel among the creatures (0 for none), the level their fight is scaled to.</summary>
    public uint MaxCreatureLevel(IEnumerable<uint> creatureIds)
        => creatureIds.Select(id => creatures.GetEntry(id)?.MaxLevel ?? 0u).DefaultIfEmpty(0u).Max();

    /// <summary>A creature the bot fights: hostile to the bot and not a usable object (faction 219 or an activation spell).</summary>
    public bool IsAttackableCreature(uint creatureId)
    {
        Creature2Entry? creature = creatures.GetEntry(creatureId);
        return creature is not null
            && IsHoldoutHostile(creatureId)
            && (uint)creature.FactionId != 219u
            && (creature.Spell4IdActivate == null || creature.Spell4IdActivate.All(id => id == 0u))
            && creature.Creature2DifficultyId != 0u;
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
                return GetQuestClosure(options.QuestIds[0], includePrerequisites: true, options.Faction, options.Path, spawnedGiverCreatureIds);

            Dictionary<ushort, QuestDefinition> requested = options.QuestIds
                .Select(GetQuest)
                .ToDictionary(definition => definition.Id);
            Dictionary<ushort, HashSet<ushort>> requestedDependencies = requested.Values.ToDictionary(
                definition => definition.Id,
                definition => SelectPrerequisites(definition, options.Faction, options.Path, spawnedGiverCreatureIds)
                    .Select(prerequisite => prerequisite.Id)
                    .Where(requested.ContainsKey)
                    .ToHashSet());
            AddGiverVisibilityOrderConstraints(requested, requestedDependencies);
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
            var selected = SelectPrerequisites(definition, options.Faction, options.Path, spawnedGiverCreatureIds).ToArray();
            dependencies[definition.Id] = selected.Select(item => item.Id).ToHashSet();
            foreach (QuestDefinition prerequisite in selected)
                Gather(prerequisite);
            gathering.Remove(definition.Id);
        }

        foreach (QuestDefinition root in roots)
            Gather(root);

        AddGiverVisibilityOrderConstraints(definitions, dependencies);
        return TopologicalOrder(definitions, dependencies);
    }

    public IReadOnlyList<QuestDefinition> GetQuestClosure(
        ushort requestedId,
        bool includePrerequisites,
        BotFaction faction,
        BotPath path,
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
                ? SelectPrerequisites(definition, faction, path, spawnedGiverCreatureIds).ToArray()
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
        AddGiverVisibilityOrderConstraints(definitions, dependencies);
        return TopologicalOrder(definitions, dependencies);
    }

    public IReadOnlyList<uint> ResolveTargetGroup(uint id)
    {
        var result = new List<uint>();
        ResolveTargetGroup(id, new HashSet<uint>(), result);
        return result.Distinct().ToArray();
    }

    public bool IsCreature(uint id) => id != 0u && creatures.GetEntry(id) is not null;

    /// <summary>
    /// Group content: the creature's difficulty row has a GroupValue (5, 20 or 40 players); the bot plays alone.
    /// </summary>
    public uint GroupSizeOfCreature(uint creatureId)
    {
        Creature2Entry? creature = creatures.GetEntry(creatureId);
        Creature2DifficultyEntry? difficulty = creature is null ? null : creatureDifficulties.GetEntry(creature.Creature2DifficultyId);
        return difficulty?.GroupValue ?? 0u;
    }

    /// <summary>
    /// The WorldLocation2 ids an objective text links (<text Link="Location:N">), in text order.
    /// </summary>
    public IReadOnlyList<uint> GetLocationIdsFromText(string text)
        => Regex.Matches(text, @"Location:([0-9]{1,9})", RegexOptions.IgnoreCase)
            .Select(match => uint.Parse(match.Groups[1].Value))
            .Distinct()
            .ToArray();

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
            // a faction group names every creature of its factions (Unconventional Technology: constructs and augmented beings
            // = the Eldan, canimid and augmented factions), a race group every creature of its Creature2 races
            case TargetGroupType.FactionIdGroup:
            case TargetGroupType.CreatureRaceIdGroup:
            {
                var values = entry.DataEntries.Where(value => value != 0u).ToHashSet();
                bool byFaction = (TargetGroupType)entry.Type == TargetGroupType.FactionIdGroup;
                result.AddRange(creatures.Entries
                    .Where(creature => values.Contains(byFaction ? (uint)creature.FactionId : creature.UnitRaceId))
                    .Select(creature => creature.Id));
                break;
            }
            // type 9 names marker entities (Icemaw Cavern's "Cave Entrance" objects for "Kill Yeti in Icemaw Cavern"): an area,
            // not the creatures to kill; neither the server nor the bot resolves it yet (SERVER GAP)
        }
    }

    public bool IsAreaMarkerTargetGroup(uint id)
        => targetGroups.GetEntry(id) is TargetGroupEntry entry && (TargetGroupType)entry.Type == TargetGroupType.Unknown9;

    /// <summary>The marker creature ids a type 9 target group names (entries that are creatures; nested groups are followed).</summary>
    public IReadOnlyList<uint> AreaMarkerCreatureIds(uint id)
    {
        var result = new List<uint>();
        void Collect(uint groupId, int depth)
        {
            TargetGroupEntry? entry = targetGroups.GetEntry(groupId);
            if (entry is null || depth > 3)
                return;
            foreach (uint value in entry.DataEntries.Where(value => value != 0u))
            {
                if (creatures.GetEntry(value) is not null)
                    result.Add(value);
                else if (targetGroups.GetEntry(value) is not null)
                    Collect(value, depth + 1);
            }
        }
        Collect(id, 0);
        return result.Distinct().ToArray();
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
            // A message conditioned on the delivered quest already being Achieved/Completed is its turn-in call, not an
            // offer. Self-Unknown remains a real zone-entry offer (A Hidden Threat).
            .Where(entry => !entry.Quests.Select((id, index) => (Id: id, Index: index))
                .Any(item => item.Id == questId && (QuestState)entry.States[item.Index] != QuestState.Unknown))
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
                entry.FactionId,
                entry.ClassId,
                entry.PrerequisiteId))
            .OrderBy(entry => entry.Id)
            .ToArray();
    }

    public IReadOnlyList<ushort> GetCommunicatorPrerequisiteQuests(QuestDefinition definition, BotFaction faction, BotPath path)
        => CommunicatorPrerequisiteQuests(definition, faction, path);

    /// <summary>
    /// Quest-state conditions every usable communicator offer requires Achieved or Completed. Completing these quests first
    /// makes the server send the offer; conditions that differ between alternative messages are not mandatory.
    /// </summary>
    private IReadOnlyList<ushort> CommunicatorPrerequisiteQuests(
        QuestDefinition definition,
        BotFaction faction,
        BotPath path)
    {
        Faction requiredFaction = faction == BotFaction.Exile ? Faction.Exile : Faction.Dominion;
        uint plannedLevel = definition.Entry.ConLevel + 1u;
        CommunicatorOffer[] offers = GetCommunicatorOffers(definition.Id)
            .Where(offer => (offer.FactionId == 0u || offer.FactionId == (uint)requiredFaction)
                && (offer.ClassId == 0u || offer.ClassId == (uint)BotClass)
                && (offer.MinLevel == 0u || plannedLevel >= offer.MinLevel)
                && (offer.MaxLevel == 0u || plannedLevel <= offer.MaxLevel)
                && CommunicatorPrerequisiteCanApply(offer.PrerequisiteId, requiredFaction, path, plannedLevel))
            .ToArray();
        if (offers.Length == 0)
            return [];

        HashSet<ushort>? requiredByEveryOffer = null;
        foreach (CommunicatorOffer offer in offers)
        {
            HashSet<ushort> required = offer.QuestConditions
                .Where(condition => condition.QuestId != definition.Id
                    && condition.State is QuestState.Achieved or QuestState.Completed)
                .Select(condition => condition.QuestId)
                .Concat(CommunicatorPrerequisiteQuestIds(offer.PrerequisiteId))
                .ToHashSet();
            if (requiredByEveryOffer is null)
                requiredByEveryOffer = required;
            else
                requiredByEveryOffer.IntersectWith(required);
        }

        return requiredByEveryOffer?.Order().ToArray() ?? [];
    }

    /// <summary>
    /// Whether a communicator prerequisite's immutable character checks can apply to this run. Quest-state/objective and
    /// other dynamic checks remain feasible here; mandatory quest dependencies are extracted separately below.
    /// </summary>
    private bool CommunicatorPrerequisiteCanApply(uint prerequisiteId, Faction faction, BotPath path, uint level)
    {
        bool? Evaluate(uint id, int depth)
        {
            PrerequisiteEntry? prerequisite = prerequisites.GetEntry(id);
            if (prerequisite is null || depth > 4)
                return null;
            var results = new List<bool?>();
            for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
            {
                PrerequisiteComparison comparison = prerequisite.PrerequisiteComparisonId[i];
                uint value = prerequisite.Value[i];
                bool? met = prerequisite.PrerequisiteTypeId[i] switch
                {
                    PrerequisiteType.None                 => null,
                    PrerequisiteType.Level                => Compare(level, comparison, value),
                    PrerequisiteType.Class                => Compare((uint)BotClass, comparison, value),
                    PrerequisiteType.Faction              => Compare((uint)faction, comparison, value),
                    PrerequisiteType.Path                 => Compare((uint)path, comparison, value),
                    PrerequisiteType.TradeSkillProfession => Compare(0u, comparison, value),
                    PrerequisiteType.UnderSpell           => (value != 0u || prerequisite.ObjectId[i] != 0u)
                        && comparison == PrerequisiteComparison.NotEqual,
                    PrerequisiteType.PublicEvent117       => false,
                    PrerequisiteType.OtherPrerequisite    => comparison switch
                    {
                        PrerequisiteComparison.Equal    => Evaluate(prerequisite.ObjectId[i], depth + 1),
                        PrerequisiteComparison.NotEqual => Evaluate(prerequisite.ObjectId[i], depth + 1) is bool child ? !child : null,
                        _                               => null
                    },
                    _                                      => null
                };
                if (prerequisite.PrerequisiteTypeId[i] == PrerequisiteType.None)
                    continue;
                results.Add(met);
            }
            if (results.Count == 0)
                return null;
            if (prerequisite.Flags == EvaluationMode.EvaluateOR)
                return results.Any(result => result == true) ? true
                    : results.All(result => result == false) ? false : null;
            return results.Any(result => result == false) ? false
                : results.All(result => result == true) ? true : null;
        }

        // Dynamic/unknown checks may become true during play and do not make the message statically unusable.
        return prerequisiteId == 0u || Evaluate(prerequisiteId, 0) != false;
    }

    /// <summary>
    /// Mandatory quest progress inside a communicator message's prerequisite. OR trees are alternative branches and do not
    /// impose one dependency; AND trees order states that a completed prerequisite quest will satisfy.
    /// </summary>
    private IReadOnlyList<ushort> CommunicatorPrerequisiteQuestIds(uint prerequisiteId)
    {
        HashSet<ushort> Collect(uint id, int depth)
        {
            PrerequisiteEntry? prerequisite = prerequisites.GetEntry(id);
            if (prerequisite is null || depth > 4)
                return [];
            var branches = new List<HashSet<ushort>>();
            for (int i = 0; i < prerequisite.PrerequisiteTypeId.Length; i++)
            {
                uint objectId = prerequisite.ObjectId[i];
                HashSet<ushort> branch = [];
                if (prerequisite.PrerequisiteTypeId[i] == PrerequisiteType.OtherPrerequisite
                    && prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.Equal)
                {
                    if (objectId != 0u)
                        branch = Collect(objectId, depth + 1);
                }
                else if (objectId != 0u && objectId <= ushort.MaxValue
                    && prerequisite.PrerequisiteTypeId[i] == PrerequisiteType.QuestState
                    && (prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.Equal
                            && prerequisite.Value[i] is (uint)QuestState.Achieved or (uint)QuestState.Completed
                        || prerequisite.PrerequisiteComparisonId[i] == PrerequisiteComparison.NotEqual
                            && prerequisite.Value[i] == (uint)QuestState.Unknown))
                    branch.Add((ushort)objectId);
                else if (objectId != 0u && objectId <= ushort.MaxValue
                    && prerequisite.PrerequisiteTypeId[i] == PrerequisiteType.QuestObjective)
                    branch.Add((ushort)objectId);

                if (prerequisite.PrerequisiteTypeId[i] != PrerequisiteType.None)
                    branches.Add(branch);
            }

            if (branches.Count == 0)
                return [];
            if (prerequisite.Flags == EvaluationMode.EvaluateAND)
                return branches.SelectMany(branch => branch).ToHashSet();

            HashSet<ushort> common = new(branches[0]);
            foreach (HashSet<ushort> branch in branches.Skip(1))
                common.IntersectWith(branch);
            return common;
        }

        return prerequisiteId == 0u ? [] : Collect(prerequisiteId, 0).Order().ToArray();
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

    /// <summary>
    /// The spell's own cooldown in milliseconds (Spell4.SpellCoolDown), 0 when the spell is unknown or has none.
    /// </summary>
    // shields carry ItemFlags 0x4000 on every plain one; other gear of the bot is flag-free
    private const uint ShieldItemFlag = 0x4000u;

    /// <summary>
    /// The gear a Warrior wears, by Item2Type (the class is on the type, not on the item): greatsword, heavy armor, shield.
    /// </summary>
    public static readonly IReadOnlyList<GearSlot> WarriorGear =
    [
        new GearSlot(51u, EquippedItem.WeaponPrimary, "greatsword"),
        new GearSlot(15u, EquippedItem.Chest, "chest"),
        new GearSlot(16u, EquippedItem.Legs, "legs"),
        new GearSlot(17u, EquippedItem.Head, "head"),
        new GearSlot(18u, EquippedItem.Shoulder, "shoulders"),
        new GearSlot(19u, EquippedItem.Feet, "boots"),
        new GearSlot(20u, EquippedItem.Hands, "gloves"),
        new GearSlot(53u, EquippedItem.Shields, "shield"),
    ];

    /// <summary>
    /// A plain green item of the type for the level: the highest power level at or below it whose required level the
    /// character meets (a player of that level wears quest and vendor gear of about that power level).
    /// </summary>
    public Item2Entry? ChooseGearForLevel(GearSlot slot, uint level)
        => items.Entries
            .Where(item => item.Item2TypeId == slot.ItemType && item.ClassRequired == 0u && item.ItemQualityId == 2u
                && item.Quest2IdActivation == 0u && ((uint)item.Flags & ~(slot.Slot == EquippedItem.Shields ? ShieldItemFlag : 0u)) == 0u
                && item.RequiredLevel <= level && item.PowerLevel <= level && item.PowerLevel > 0u)
            .OrderByDescending(item => item.PowerLevel)
            .ThenBy(item => item.Id)
            .FirstOrDefault();

    /// <summary>
    /// A plain green weapon of the bot's class for the level: the highest power level at or below it whose required level
    /// the character meets (a player of that level wears quest and vendor gear of about that power level).
    /// </summary>
    public Item2Entry? ChooseWeaponForLevel(uint level)
        => ChooseGearForLevel(WarriorGear[0], level);

    public string ItemName(uint itemId)
        => items.GetEntry(itemId) is Item2Entry item ? english.GetEntry(item.LocalizedTextIdName) ?? itemId.ToString() : itemId.ToString();

    public uint GetSpellCooldownMs(uint spellId)
        => spells.GetEntry(spellId)?.SpellCoolDown ?? 0u;

    private static GameTable<T> Load<T>(string directory, string name) where T : class, new()
        => new(RequiredPath(directory, name));

    private IEnumerable<QuestDefinition> SelectPrerequisites(
        QuestDefinition definition,
        BotFaction faction,
        BotPath path,
        IReadOnlySet<uint> spawnedGiverCreatureIds)
    {
        QuestDefinition[] candidates = definition.Entry.PrerequisiteQuests
            .Where(id => id != 0u)
            .Select(id => GetQuest(checked((ushort)id)))
            .ToArray();
        // quests the Prerequisite table requires completed are prerequisites too (Algoroc chains use it instead of PrerequisiteQuests),
        // and so are quests a giver's visibility prerequisite needs done (Belle Walker of Power Struggle stays hidden until
        // Closing the Circuit is at least accepted; this bot completes a prerequisite, which satisfies "accepted or later")
        QuestDefinition[] stateRequired = QuestStatePrerequisiteQuests(definition.Entry)
            .Concat(definition.GiverCreatureIds.SelectMany(GiverVisibilityPrerequisiteQuests))
            .Concat(CommunicatorPrerequisiteQuests(definition, faction, path))
            .Where(id => id != definition.Id && quests.GetEntry(id) is not null && candidates.All(candidate => candidate.Id != id))
            .Distinct()
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

    /// <summary>
    /// Order a quest before the quest that phases out its only table-listed giver. Spilling Lifeblood (6964), for
    /// example, is available from Alchemist Polovic's earlier creature row while Horizon (6959) is untouched; accepting
    /// Horizon swaps in the later row at the same location. This is an offer-window constraint, not a quest prerequisite.
    /// </summary>
    private void AddGiverVisibilityOrderConstraints(
        IReadOnlyDictionary<ushort, QuestDefinition> definitions,
        IReadOnlyDictionary<ushort, HashSet<ushort>> dependencies)
    {
        bool DependsOn(ushort questId, ushort possibleDependency, HashSet<ushort> visited)
        {
            if (!visited.Add(questId) || !dependencies.TryGetValue(questId, out HashSet<ushort>? direct))
                return false;
            return direct.Contains(possibleDependency)
                || direct.Any(dependency => DependsOn(dependency, possibleDependency, visited));
        }

        foreach (QuestDefinition definition in definitions.Values)
        {
            if (definition.GiverCreatureIds.Count != 1
                || definition.Entry.PrerequisiteItem != 0u
                || GetQuestActivationItem(definition.Id) != 0u
                || GetCommunicatorOffers(definition.Id).Count != 0)
                continue;

            uint giverId = definition.GiverCreatureIds[0];
            foreach (ushort endingQuestId in GiverVisibilityEndingQuests(giverId))
            {
                if (endingQuestId == definition.Id
                    || !definitions.ContainsKey(endingQuestId)
                    // If the offered quest needs the phase-changing quest first, the table describes no usable early
                    // offer window; do not turn that data problem into a dependency cycle.
                    || DependsOn(definition.Id, endingQuestId, []))
                    continue;

                dependencies[endingQuestId].Add(definition.Id);
            }
        }

        // a CompleteQuest objective (type 24) is credited when the named quest is turned in while the owner is accepted:
        // the named quest follows its owner (Last-Second Support 4759 owns Defend the Gate 4954, Power Restoration 4955 and
        // Anti-Air Support 4761); the bot plays them inside the owner's objective
        foreach (QuestDefinition definition in definitions.Values)
        {
            foreach (QuestObjectiveDefinition objective in definition.Objectives)
            {
                if (objective.Type != QuestObjectiveType.CompleteQuest
                    || objective.Data == 0u || objective.Data > ushort.MaxValue
                    || objective.Data == definition.Id
                    || !definitions.ContainsKey((ushort)objective.Data)
                    || DependsOn(definition.Id, (ushort)objective.Data, []))
                    continue;
                dependencies[(ushort)objective.Data].Add(definition.Id);
            }
        }
    }

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

/// <summary>An equipment slot of the bot's class with the Item2Type worn there.</summary>
internal sealed record GearSlot(uint ItemType, EquippedItem Slot, string Kind);
