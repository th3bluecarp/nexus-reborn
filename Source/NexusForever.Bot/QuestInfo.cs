using System.Text;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.Bot;

internal sealed record InfoOptions(
    string Tables,
    IReadOnlyList<ushort> Quests,
    uint? PathZone,
    bool Chain,
    string? Database);

/// <summary>Prints quest prerequisites, objectives, givers and receivers straight from the game tables (no server needed).</summary>
internal static class QuestInfo
{
    public static async Task<int> RunAsync(InfoOptions options)
    {
        string tables = options.Tables;
        var quests = new GameTable<Quest2Entry>(Path.Combine(tables, "Quest2.tbl"));
        var objectives = new GameTable<QuestObjectiveEntry>(Path.Combine(tables, "QuestObjective.tbl"));
        var creatures = new GameTable<Creature2Entry>(Path.Combine(tables, "Creature2.tbl"));
        var gameData = new BotGameData(tables, allowMissingCommunicatorMessages: true);
        var english = new TextTable(Path.Combine(tables, "en-US.bin"));
        var prerequisites = new GameTable<PrerequisiteEntry>(Path.Combine(tables, "Prerequisite.tbl"));
        if (options.PathZone is uint pathZone)
        {
            PrintPathMissions(gameData, pathZone);
            return 0;
        }
        if (!gameData.HasCommunicatorTable)
            Console.WriteLine($"communicators: unavailable (missing {Path.Combine(tables, "CommunicatorMessages.tbl")})");

        var order = new List<ushort>();
        var seen = new HashSet<ushort>();
        void Visit(ushort id)
        {
            if (!seen.Add(id)) return;
            Quest2Entry? entry = quests.GetEntry(id);
            if (entry != null && options.Chain)
                foreach (uint prereq in entry.PrerequisiteQuests.Where(q => q != 0))
                    Visit((ushort)prereq);
            order.Add(id);
        }
        foreach (ushort id in options.Quests) Visit(id);
        IReadOnlyDictionary<uint, ItemDropSources> itemDropSources = new Dictionary<uint, ItemDropSources>();
        if (options.Database is not null)
        {
            uint[] itemIds = order
                .Select(id => quests.GetEntry(id)?.PrerequisiteItem ?? 0u)
                .Where(itemId => itemId != 0u)
                .Distinct()
                .ToArray();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            itemDropSources = await BotDatabase.LoadItemDropSourcesAsync(options.Database, itemIds, timeout.Token);
        }
        if (options.Quests.Count == 1 && options.Quests[0] == 0)
        {
            var creations = new GameTable<CharacterCreationEntry>(Path.Combine(tables, "CharacterCreation.tbl"));
            foreach (CharacterCreationEntry c in creations.Entries.Where(c => c.FactionId == NexusForever.Game.Static.Reputation.Faction.Exile && c.RaceId == NexusForever.Game.Static.Entity.Race.Human))
                Console.WriteLine($"creation {c.Id} class={c.ClassId} sex={c.Sex} start={c.CharacterCreationStartEnum}({(uint)c.CharacterCreationStartEnum}) enabled={c.Enabled} xp={c.Xp} costumeOnly={c.CostumeOnly}");
            return 0;
        }
        if (options.Quests.Count == 1 && options.Quests[0] == 65535)
        {
            // prerequisite research: every Prerequisite row with a Level(1) check, who uses it, and the quest con level
            var byQuest = new Dictionary<uint, List<Quest2Entry>>();
            foreach (Quest2Entry q in quests.Entries.Where(q => q.PrerequisiteId != 0))
            { if (!byQuest.ContainsKey(q.PrerequisiteId)) byQuest[q.PrerequisiteId] = new(); byQuest[q.PrerequisiteId].Add(q); }
            var hist = new Dictionary<string, int>();
            foreach (PrerequisiteEntry p in prerequisites.Entries)
                for (int i = 0; i < p.PrerequisiteTypeId.Length; i++)
                {
                    if ((uint)p.PrerequisiteTypeId[i] != 1) continue;
                    string key = $"{p.PrerequisiteComparisonId[i]} value={p.Value[i]} obj={p.ObjectId[i]}";
                    hist[key] = hist.GetValueOrDefault(key) + 1;
                    if (byQuest.TryGetValue(p.Id, out var qs))
                        foreach (Quest2Entry q in qs.Take(3))
                            Console.WriteLine($"LEVELCHECK prereq={p.Id} {key} mode={p.Flags} quest={q.Id} \"{english.GetEntry(q.LocalizedTextIdTitle)}\" conLevel={q.ConLevel} prereqLevel={q.PrerequisiteLevel} zone={q.WorldZoneId}");
                }
            foreach (var kv in hist.OrderByDescending(k => k.Value)) Console.WriteLine($"HIST {kv.Value,5}  {kv.Key}");
            return 0;
        }

        foreach (ushort id in order)
        {
            Quest2Entry? entry = quests.GetEntry(id);
            if (entry == null) { Console.WriteLine($"quest {id}: NOT FOUND"); continue; }
            var sb = new StringBuilder();
            sb.Append($"quest {id} \"{english.GetEntry(entry.LocalizedTextIdTitle)}\" zone={entry.WorldZoneId} flags=0x{entry.Flags:X} type={entry.Type} conLevel={entry.ConLevel}");
            sb.Append($" | prereq: level={entry.PrerequisiteLevel} flags={entry.PrerequisiteFlags} quests=[{string.Join(",", entry.PrerequisiteQuests.Where(q => q != 0))}] race={entry.PrerequisiteRace} class={entry.PrerequisiteClass} item={entry.PrerequisiteItem} faction={entry.QuestPlayerFactionEnum}");
            Console.WriteLine(sb);
            if (entry.PrerequisiteItem != 0u)
            {
                uint activationQuest = gameData.GetItemActivationQuest(entry.PrerequisiteItem);
                if (itemDropSources.TryGetValue(entry.PrerequisiteItem, out ItemDropSources? sources))
                {
                    string creaturesText = sources.CreatureIds.Count == 0
                        ? "none"
                        : string.Join(", ", sources.CreatureIds.Select(creatureId =>
                            $"{creatureId} {english.GetEntry(creatures.GetEntry(creatureId)?.LocalizedTextIdName ?? 0)}"));
                    string containersText = sources.ContainerItemIds.Count == 0
                        ? "none"
                        : string.Join(",", sources.ContainerItemIds);
                    Console.WriteLine($"  item start: item={entry.PrerequisiteItem} activationQuest={activationQuest} dropSources=[{creaturesText}] containerItems=[{containersText}]");
                }
                else
                    Console.WriteLine($"  item start: item={entry.PrerequisiteItem} activationQuest={activationQuest} dropSources=unavailable (--db not supplied)");
            }
            if (entry.PrerequisiteId != 0)
            {
                PrerequisiteEntry? p = prerequisites.GetEntry(entry.PrerequisiteId);
                Console.WriteLine(p == null ? $"  prerequisiteId={entry.PrerequisiteId} NOT FOUND" : $"  prerequisiteId={entry.PrerequisiteId} mode={p.Flags} checks=[{string.Join("; ", p.PrerequisiteTypeId.Select((t, i) => t == 0 ? null : $"{t}({(uint)t}) {p.PrerequisiteComparisonId[i]} obj={p.ObjectId[i]} value={p.Value[i]}").Where(s => s != null))}] failText=\"{english.GetEntry(p.LocalizedTextIdFailure)}\"");
            }
            IEnumerable<Creature2Entry> givers = creatures.Entries.Where(c => c.QuestIdGiven.Contains(id));
            IEnumerable<Creature2Entry> receivers = creatures.Entries.Where(c => c.QuestIdReceive.Contains(id));
            Console.WriteLine($"  givers: {string.Join(", ", givers.Select(c => $"{c.Id} {english.GetEntry(c.LocalizedTextIdName)}"))}");
            Console.WriteLine($"  receivers: {string.Join(", ", receivers.Select(c => $"{c.Id} {english.GetEntry(c.LocalizedTextIdName)}"))}");
            IReadOnlyList<CommunicatorOffer> offers = gameData.GetCommunicatorOffers(id);
            Console.WriteLine(offers.Count == 0
                ? "  communicators: none"
                : $"  communicators: {string.Join("; ", offers.Select(FormatCommunicator))}");
            int index = 0;
            foreach (uint objectiveId in entry.Objectives.Where(o => o != 0))
            {
                QuestObjectiveEntry? o = objectives.GetEntry(objectiveId);
                if (o == null) { Console.WriteLine($"  objective {index++}: id {objectiveId} NOT FOUND"); continue; }
                var extra = new StringBuilder();
                if ((QuestObjectiveType)o.Type is QuestObjectiveType.KillTargetGroup or QuestObjectiveType.KillTargetGroups or QuestObjectiveType.ActivateTargetGroup or QuestObjectiveType.TalkToTargetGroup)
                    extra.Append(" creatures=[" + string.Join(",", gameData.ResolveTargetGroup(o.Data).Select(c => $"{c} {english.GetEntry(creatures.GetEntry(c)?.LocalizedTextIdName ?? 0)}")) + "]");
                else if (o.Data != 0 && creatures.GetEntry(o.Data) is { } c)
                    extra.Append($" creature=\"{english.GetEntry(c.LocalizedTextIdName)}\"");
                uint[] rewardPaneCreatures = gameData.ResolveTargetGroup(o.TargetGroupIdRewardPane).ToArray();
                extra.Append($" rewardPane={o.TargetGroupIdRewardPane} [{string.Join(',', rewardPaneCreatures)}]");
                if (!gameData.IsCreature(o.Data))
                    extra.Append($" dataAsTargetGroup=[{string.Join(',', gameData.ResolveTargetGroup(o.Data))}]");
                Console.WriteLine($"  objective {index++}: id={objectiveId} type={(QuestObjectiveType)o.Type}({o.Type}) data={o.Data} count={o.Count} flags=0x{o.Flags:X} loc={o.WorldLocationsIdIndicator00} \"{english.GetEntry(o.LocalizedTextIdFull)}\"{extra}");
            }
        }
        return 0;
    }

    private static string FormatCommunicator(CommunicatorOffer offer)
    {
        string questConditions = offer.QuestConditions.Count == 0
            ? "none"
            : string.Join(",", offer.QuestConditions.Select(item => $"{item.QuestId}:{item.State}"));
        return $"id={offer.Id} world={offer.WorldId} zone={offer.WorldZoneId} levels=[{offer.MinLevel},{offer.MaxLevel}] questStates=[{questConditions}] prerequisite={offer.PrerequisiteId}";
    }

    private static void PrintPathMissions(BotGameData gameData, uint zoneId)
    {
        IReadOnlyList<PathMissionDefinition> missions = gameData.GetPathMissions(zoneId);
        Console.WriteLine($"path missions for zone {zoneId}: {missions.Count}");
        foreach (BotPath path in Enum.GetValues<BotPath>())
        {
            PathMissionDefinition[] pathMissions = missions.Where(mission => mission.Path == path).ToArray();
            Console.WriteLine($"{path}: {pathMissions.Length}");
            foreach (IGrouping<uint, PathMissionDefinition> episode in pathMissions.GroupBy(mission => mission.Episode.Id))
            {
                PathMissionDefinition first = episode.First();
                Console.WriteLine($"  episode {episode.Key} \"{gameData.GetPathEpisodeName(first.Episode)}\" world={first.Episode.WorldId}");
                foreach (PathMissionDefinition mission in episode)
                {
                    string locations = mission.Locations.Count == 0
                        ? "none"
                        : string.Join(", ", mission.Locations.Select(location =>
                            FormattableString.Invariant($"{location.Id}@w{location.WorldId}({location.Position0:R},{location.Position1:R},{location.Position2:R})")));
                    Console.WriteLine($"    mission {mission.Id} \"{mission.Name}\" type={mission.TypeName}({mission.RawType}) unlockCreature={mission.Entry.Creature2IdUnlock} locations=[{locations}]");
                }
            }
        }
    }
}
