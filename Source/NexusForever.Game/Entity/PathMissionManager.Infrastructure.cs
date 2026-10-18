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

        private readonly Dictionary<uint, uint> carriedResources = [];

        private static uint[] ResourceCreatures(PathSettlerInfrastructureEntry infrastructure)
        {
            return new[] { infrastructure.Creature2IdResource00, infrastructure.Creature2IdResource01, infrastructure.Creature2IdResource02 }
                .Where(c => c != 0u)
                .ToArray();
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
            return match.Success ? match.Groups[1].Value : null;
        }

        private static bool IsResource(PathSettlerInfrastructureEntry infrastructure, uint creatureId)
        {
            uint[] resources = ResourceCreatures(infrastructure);
            if (resources.Length > 0)
                return resources.Contains(creatureId);

            string resource = ObjectiveResource(infrastructure);
            Creature2Entry creature = GameTableManager.Instance.Creature2.GetEntry(creatureId);
            string name = creature == null ? null : GameTableManager.Instance.TextEnglish?.GetEntry(creature.LocalizedTextIdName);
            if (resource == null || name == null)
                return false;

            name = Regex.Replace(name, @"\{p:[^}]*\}", "");
            // "Eldan Relics" in the text, "Eldan Relic" and "Eldan Relics" as creatures
            return string.Equals(name, resource, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name + "s", resource, StringComparison.OrdinalIgnoreCase);
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
