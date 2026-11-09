using System.Numerics;
using System.Text.RegularExpressions;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using Path = NexusForever.Game.Static.PlayerPath.Path;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Settler infrastructure missions (mission type 21, "PROJECT"): carry resources to the builder of a project.
    /// </summary>
    /// <remarks>
    /// The mission's object is a PathSettlerInfrastructure: activating one of its resource creatures picks up a resource (up to
    /// MaxStackCountResource carried, the Spell4IdResource buff shows it), coming within <see cref="BuilderRange"/> of the
    /// builder (Creature2IdDepot) hands them in, MissionCount deliveries complete the mission. Projects without a resource
    /// creature take the creatures named in their objective ("Collect Eldan Relics for ..."). Retail added every settler's
    /// deliveries to a shared project total (Count) with a timer, that part is not kept.
    /// </remarks>
    public partial class PathMissionManager
    {
        private const float BuilderRange = 8f;
        private const uint GenericActivateSpell = 1817u;

        private readonly Dictionary<uint, uint> carriedResources = [];

        private static readonly Lazy<Dictionary<uint, uint[]>> infrastructureCreaturesByActivateSpell = new(() =>
            GameTableManager.Instance.Creature2.Entries
                .SelectMany(creature => creature.Spell4IdActivate
                    .Where(spellId => spellId != 0u && spellId != GenericActivateSpell)
                    .Distinct()
                    .Select(spellId => (SpellId: spellId, CreatureId: creature.Id)))
                .GroupBy(pair => pair.SpellId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(pair => pair.CreatureId).Distinct().Order().ToArray()));

        private static readonly Lazy<Dictionary<string, uint[]>> infrastructureCreaturesByName = new(() =>
            GameTableManager.Instance.Creature2.Entries
                .Select(creature => (creature.Id, Name: CreatureName(creature)))
                .Where(creature => creature.Name != null)
                .GroupBy(creature => creature.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(creature => creature.Id).Distinct().Order().ToArray(),
                    StringComparer.OrdinalIgnoreCase));

        /// <summary>
        /// State of a settler infrastructure project as the InfrastructureState prerequisite reads it: 2 built, 1 under way, 0 not started.
        /// </summary>
        /// <remarks>
        /// Retail shared projects between all settlers of a zone; here it is the player's own project mission (see docs/reborn/SEMANTICS.md).
        /// </remarks>
        public uint GetInfrastructureState(uint infrastructureId)
        {
            PathMission mission = missions.Values.FirstOrDefault(m => m.Kind == MissionKind.Infrastructure && m.Objects.Length > 0 && m.Objects[0] == infrastructureId);
            if (mission == null)
                return 0u;
            if (mission.IsComplete)
                return 2u;
            return mission.Progress > 0u || mission.State == Static.PlayerPath.PathMissionState.Started ? 1u : 0u;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, uint[]> resourceCreatureCache = new();

        private static uint[] ResourceCreatures(PathSettlerInfrastructureEntry infrastructure)
            => resourceCreatureCache.GetOrAdd(infrastructure.Id, _ => ResolveResourceCreatures(infrastructure));

        private static uint[] ResolveResourceCreatures(PathSettlerInfrastructureEntry infrastructure)
        {
            uint[] resources = new[] { infrastructure.Creature2IdResource00, infrastructure.Creature2IdResource01, infrastructure.Creature2IdResource02 }
                .Where(c => c != 0u)
                .Distinct()
                .ToArray();
            if (resources.Length > 0)
                return resources;

            // exact name first, so projects that resolved through their text before (Algoroc 53: Eldan Relics) keep it
            string resourceName = ObjectiveResource(infrastructure);
            if (resourceName != null)
            {
                resources = ExactNameCreatures(resourceName);
                if (resources.Length > 0)
                    return resources;
            }

            // creatures whose activation casts the project resource spell (Celestion 34: Sylvan Glade Tenders give Arborian Seeds)
            resources = new[] { infrastructure.Spell4IdResource00, infrastructure.Spell4IdResource01, infrastructure.Spell4IdResource02 }
                .Where(spellId => spellId != 0u && spellId != GenericActivateSpell)
                .Distinct()
                .SelectMany(spellId => infrastructureCreaturesByActivateSpell.Value.GetValueOrDefault(spellId) ?? [])
                .Distinct()
                .Order()
                .ToArray();
            if (resources.Length > 0)
                return resources;

            if (resourceName == null)
                return [];

            string singularResourceName = SingularizeWords(resourceName);
            return infrastructureCreaturesByName.Value
                .Where(pair => string.Equals(SingularizeWords(pair.Key), singularResourceName, StringComparison.OrdinalIgnoreCase))
                .SelectMany(pair => pair.Value)
                .Distinct()
                .Order()
                .ToArray();
        }

        private static uint[] ExactNameCreatures(string resourceName)
        {
            var resources = new HashSet<uint>();
            if (infrastructureCreaturesByName.Value.TryGetValue(resourceName, out uint[] exact))
                resources.UnionWith(exact);

            foreach ((string name, uint[] creatureIds) in infrastructureCreaturesByName.Value)
                if (string.Equals(name + "s", resourceName, StringComparison.OrdinalIgnoreCase))
                    resources.UnionWith(creatureIds);

            return resources.Order().ToArray();
        }

        private static string CreatureName(Creature2Entry creature)
        {
            string name = GameTableManager.Instance.TextEnglish?.GetEntry(creature.LocalizedTextIdName);
            return name == null ? null : Regex.Replace(name, @"\{p:[^}]*\}", "");
        }

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
        /// The resource named in the objective text, for projects without a resource creature.
        /// </summary>
        private static string ObjectiveResource(PathSettlerInfrastructureEntry infrastructure)
        {
            string text = GameTableManager.Instance.TextEnglish?.GetEntry(infrastructure.LocalizedTextIdObjective);
            if (text == null)
                return null;

            Match match = Regex.Match(text, @"^Collect (.+?) (?:for|from|to)\b", RegexOptions.IgnoreCase);
            return match.Success ? Regex.Replace(match.Groups[1].Value, @"\{p:[^}]*\}", "") : null;
        }

        private static bool IsResource(PathSettlerInfrastructureEntry infrastructure, uint creatureId)
        {
            return ResourceCreatures(infrastructure).Contains(creatureId);
        }

        private void GatherInfrastructureResource(uint creatureId)
        {
            if (player.Path != Path.Settler)
                return;

            foreach (PathMission mission in missions.Values.Where(m => m.Kind == MissionKind.Infrastructure && !m.IsComplete).ToList())
            {
                PathSettlerInfrastructureEntry infrastructure = GameTableManager.Instance.PathSettlerInfrastructure.GetEntry(mission.Objects[0]);
                if (infrastructure == null || !IsResource(infrastructure, creatureId))
                    continue;

                uint max = Math.Max(Math.Max(infrastructure.MaxStackCountResource00, infrastructure.MaxStackCountResource01), 1u);
                carriedResources.TryGetValue(infrastructure.Id, out uint carried);
                if (carried >= max)
                    continue;

                carriedResources[infrastructure.Id] = carried + 1u;
                if (infrastructure.Spell4IdResource00 != 0u)
                    player.CastSpell(infrastructure.Spell4IdResource00, new SpellParameters { UserInitiatedSpellCast = false });

                if (mission.State == Static.PlayerPath.PathMissionState.Unlocked)
                    Advance(mission, mission.Progress);
                return;
            }
        }

        private void DeliverInfrastructureResources()
        {
            if (player.Path != Path.Settler || carriedResources.Count == 0 || player.Map == null)
                return;

            foreach (PathMission mission in missions.Values.Where(m => m.Kind == MissionKind.Infrastructure && !m.IsComplete).ToList())
            {
                if (!carriedResources.TryGetValue(mission.Objects[0], out uint carried) || carried == 0u)
                    continue;

                PathSettlerInfrastructureEntry infrastructure = GameTableManager.Instance.PathSettlerInfrastructure.GetEntry(mission.Objects[0]);
                bool atBuilder = player.GetVisible()
                    .OfType<IWorldEntity>()
                    .Any(e => e.CreatureId == infrastructure.Creature2IdDepot && Vector3.Distance(e.Position, player.Position) <= BuilderRange);
                if (!atBuilder)
                    continue;

                carriedResources.Remove(infrastructure.Id);
                if (infrastructure.Spell4IdResource00 != 0u && player.HasSpell(infrastructure.Spell4IdResource00, out Abstract.Spell.ISpell buff))
                    buff.Finish();

                Advance(mission, Math.Min(mission.Progress + carried, mission.Count));
            }
        }
    }
}
