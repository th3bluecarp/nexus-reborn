using System.Text;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.Bot;

internal sealed record InfoOptions(string Tables, IReadOnlyList<ushort> Quests, bool Chain);

/// <summary>Prints quest prerequisites, objectives, givers and receivers straight from the game tables (no server needed).</summary>
internal static class QuestInfo
{
    public static int Run(InfoOptions options)
    {
        string tables = options.Tables;
        var quests = new GameTable<Quest2Entry>(Path.Combine(tables, "Quest2.tbl"));
        var objectives = new GameTable<QuestObjectiveEntry>(Path.Combine(tables, "QuestObjective.tbl"));
        var creatures = new GameTable<Creature2Entry>(Path.Combine(tables, "Creature2.tbl"));
        var targetGroups = new GameTable<TargetGroupEntry>(Path.Combine(tables, "TargetGroup.tbl"));
        var english = new TextTable(Path.Combine(tables, "en-US.bin"));
        var prerequisites = new GameTable<PrerequisiteEntry>(Path.Combine(tables, "Prerequisite.tbl"));

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

        foreach (ushort id in order)
        {
            Quest2Entry? entry = quests.GetEntry(id);
            if (entry == null) { Console.WriteLine($"quest {id}: NOT FOUND"); continue; }
            var sb = new StringBuilder();
            sb.Append($"quest {id} \"{english.GetEntry(entry.LocalizedTextIdTitle)}\" zone={entry.WorldZoneId} flags=0x{entry.Flags:X} type={entry.Type} conLevel={entry.ConLevel}");
            sb.Append($" | prereq: level={entry.PrerequisiteLevel} flags={entry.PrerequisiteFlags} quests=[{string.Join(",", entry.PrerequisiteQuests.Where(q => q != 0))}] race={entry.PrerequisiteRace} class={entry.PrerequisiteClass} item={entry.PrerequisiteItem} faction={entry.QuestPlayerFactionEnum}");
            Console.WriteLine(sb);
            if (entry.PrerequisiteId != 0)
            {
                PrerequisiteEntry? p = prerequisites.GetEntry(entry.PrerequisiteId);
                Console.WriteLine(p == null ? $"  prerequisiteId={entry.PrerequisiteId} NOT FOUND" : $"  prerequisiteId={entry.PrerequisiteId} mode={p.Flags} checks=[{string.Join("; ", p.PrerequisiteTypeId.Select((t, i) => t == 0 ? null : $"{t}({(uint)t}) {p.PrerequisiteComparisonId[i]} obj={p.ObjectId[i]} value={p.Value[i]}").Where(s => s != null))}] failText=\"{english.GetEntry(p.LocalizedTextIdFailure)}\"");
            }
            IEnumerable<Creature2Entry> givers = creatures.Entries.Where(c => c.QuestIdGiven.Contains(id));
            IEnumerable<Creature2Entry> receivers = creatures.Entries.Where(c => c.QuestIdReceive.Contains(id));
            Console.WriteLine($"  givers: {string.Join(", ", givers.Select(c => $"{c.Id} {english.GetEntry(c.LocalizedTextIdName)}"))}");
            Console.WriteLine($"  receivers: {string.Join(", ", receivers.Select(c => $"{c.Id} {english.GetEntry(c.LocalizedTextIdName)}"))}");
            int index = 0;
            foreach (uint objectiveId in entry.Objectives.Where(o => o != 0))
            {
                QuestObjectiveEntry? o = objectives.GetEntry(objectiveId);
                if (o == null) { Console.WriteLine($"  objective {index++}: id {objectiveId} NOT FOUND"); continue; }
                string extra = "";
                if ((QuestObjectiveType)o.Type is QuestObjectiveType.KillTargetGroup or QuestObjectiveType.KillTargetGroups or QuestObjectiveType.ActivateTargetGroup or QuestObjectiveType.TalkToTargetGroup)
                    extra = " creatures=[" + string.Join(",", ResolveTargetGroup(targetGroups, o.Data, new HashSet<uint>()).Select(c => $"{c} {english.GetEntry(creatures.GetEntry(c)?.LocalizedTextIdName ?? 0)}")) + "]";
                else if (o.Data != 0 && creatures.GetEntry(o.Data) is { } c)
                    extra = $" creature=\"{english.GetEntry(c.LocalizedTextIdName)}\"";
                Console.WriteLine($"  objective {index++}: id={objectiveId} type={(QuestObjectiveType)o.Type}({o.Type}) data={o.Data} count={o.Count} flags=0x{o.Flags:X} loc={o.WorldLocationsIdIndicator00} \"{english.GetEntry(o.LocalizedTextIdFull)}\"{extra}");
            }
        }
        return 0;
    }

    private static IEnumerable<uint> ResolveTargetGroup(GameTable<TargetGroupEntry> table, uint id, HashSet<uint> visited)
    {
        if (!visited.Add(id)) yield break;
        TargetGroupEntry? entry = table.GetEntry(id);
        if (entry == null) yield break;
        switch ((TargetGroupType)entry.Type)
        {
            case TargetGroupType.CreatureIdGroup:
                foreach (uint c in entry.DataEntries.Where(d => d != 0)) yield return c;
                break;
            case TargetGroupType.OtherTargetGroup:
            case TargetGroupType.OtherTargetGroupCreatures:
                foreach (uint g in entry.DataEntries.Where(d => d != 0))
                    foreach (uint c in ResolveTargetGroup(table, g, visited)) yield return c;
                break;
        }
    }
}
