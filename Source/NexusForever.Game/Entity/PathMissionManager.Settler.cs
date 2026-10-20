using System.Numerics;
using System.Text.RegularExpressions;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.PlayerPath;
using NexusForever.Network.World.Message.Static;
using Path = NexusForever.Game.Static.PlayerPath.Path;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Settler expansion missions (mission type 19): gather the hub's resources and build its improvements at the depots.
    /// </summary>
    /// <remarks>
    /// The mission's object is a PathSettlerHub. A hub's depot creature stands at the display point its improvement groups share
    /// and offers all of them (one ServerPathSettlerAddImprovementInfoToUnit per group for the same unit), building a tier costs the hub resources listed by its PathSettlerImprovement and adds the group's
    /// contribution to every expansion mission of the hub, the hub's MissionCount completes it. Built improvements last
    /// DurationPerBundleMs. Retail shared improvements between all settlers of the zone, here they are per player.
    /// Resource nodes give the hub resource whose item name is the node's name (Salvaged Machine Parts), the lost server
    /// scripts did this.
    /// </remarks>
    public partial class PathMissionManager
    {
        private class BuiltImprovement
        {
            public uint Tier { get; init; }
            public DateTime? Expires { get; init; }
        }

        private readonly Dictionary<uint, BuiltImprovement> builtImprovements = [];

        private static Dictionary<string, uint> settlerResourceItems;

        /// <summary>
        /// Item id of every settler hub resource by its item name.
        /// </summary>
        private static Dictionary<string, uint> SettlerResourceItems()
        {
            if (settlerResourceItems != null)
                return settlerResourceItems;

            var items = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            foreach (PathSettlerHubEntry hub in GameTableManager.Instance.PathSettlerHub.Entries)
            {
                foreach (uint itemId in new[] { hub.Item2IdResource00, hub.Item2IdResource01, hub.Item2IdResource02 }.Where(i => i != 0u))
                {
                    Item2Entry item = GameTableManager.Instance.Item.GetEntry(itemId);
                    string name = item == null ? null : GameTableManager.Instance.TextEnglish?.GetEntry(item.LocalizedTextIdName);
                    if (!string.IsNullOrEmpty(name))
                        items.TryAdd(Regex.Replace(name, @"\{p:[^}]*\}", ""), itemId);
                }
            }

            return settlerResourceItems = items;
        }

        private void GatherSettlerResource(uint creatureId)
        {
            if (player.Path != Path.Settler)
                return;

            Creature2Entry creature = GameTableManager.Instance.Creature2.GetEntry(creatureId);
            string name = creature == null ? null : GameTableManager.Instance.TextEnglish?.GetEntry(creature.LocalizedTextIdName);
            if (name == null || !SettlerResourceItems().TryGetValue(Regex.Replace(name, @"\{p:[^}]*\}", ""), out uint itemId))
                return;

            player.Inventory.ItemCreate(InventoryLocation.Inventory, itemId, 1u, ItemUpdateReason.Loot);
        }

        private const float DepotRange = 10f;

        /// <summary>
        /// The improvement groups a depot unit offers: the groups using its creature whose display point is within <see cref="DepotRange"/>.
        /// </summary>
        private List<PathSettlerImprovementGroupEntry> DepotGroups(IWorldEntity entity)
        {
            if (entity.CreatureId == 0u || player.Map == null)
                return [];

            return GameTableManager.Instance.PathSettlerImprovementGroup.Entries
                .Where(g => g.Creature2IdDepot == entity.CreatureId)
                .Select(g => (Group: g, Location: GameTableManager.Instance.WorldLocation2.GetEntry(g.WorldLocation2IdDisplayPoint)))
                .Where(g => g.Location != null && g.Location.WorldId == player.Map.Entry.Id
                    && Vector3.Distance(new Vector3(g.Location.Position0, g.Location.Position1, g.Location.Position2), entity.Position) <= DepotRange)
                .Select(g => g.Group)
                .ToList();
        }

        private static uint[] ImprovementTiers(PathSettlerImprovementGroupEntry group)
        {
            return new[] { group.PathSettlerImprovementIdTier00, group.PathSettlerImprovementIdTier01, group.PathSettlerImprovementIdTier02, group.PathSettlerImprovementIdTier03 }
                .TakeWhile(t => t != 0u)
                .ToArray();
        }

        private BuiltImprovement GetBuilt(uint groupId)
        {
            if (!builtImprovements.TryGetValue(groupId, out BuiltImprovement built))
                return null;

            if (built.Expires != null && built.Expires < DateTime.UtcNow)
            {
                builtImprovements.Remove(groupId);
                return null;
            }

            return built;
        }

        private void SendDepot(IWorldEntity depot, PathSettlerImprovementGroupEntry group)
        {
            BuiltImprovement built = GetBuilt(group.Id);
            var message = new ServerPathSettlerAddImprovementInfoToUnit
            {
                UnitId                        = depot.Guid,
                PathSettlerImprovementGroupId = group.Id,
                RemainingTime                 = built?.Expires == null ? 0u : (uint)Math.Max(0d, (built.Expires.Value - DateTime.UtcNow).TotalMilliseconds)
            };

            if (built != null)
            {
                message.Owners.Add(player.Name);
                message.Improvements.Add(new ServerPathSettlerAddImprovementInfoToUnit.ImprovementInfo
                {
                    Name = player.Name,
                    Tier = built.Tier
                });
            }

            player.Session.EnqueueMessageEncrypted(message);
        }

        private void MarkSettlerDepot(IWorldEntity entity)
        {
            if (player.Path != Path.Settler)
                return;

            foreach (PathSettlerImprovementGroupEntry group in DepotGroups(entity))
                SendDepot(entity, group);
        }

        public bool OpenSettlerDepot(IWorldEntity entity)
        {
            if (entity == null || player.Path != Path.Settler)
                return false;

            List<PathSettlerImprovementGroupEntry> groups = DepotGroups(entity);
            if (groups.Count == 0)
                return false;

            // the window lists the groups the client knows for the unit, so refresh them first
            foreach (PathSettlerImprovementGroupEntry group in groups)
                SendDepot(entity, group);

            player.Session.EnqueueMessageEncrypted(new ServerPathSettlerBuildInvoke
            {
                SettlerHubUnitId               = entity.Guid,
                PathSettlerImprovementGroupIds = groups.Select(g => g.Id).ToList()
            });
            return true;
        }

        public void BuildImprovement(uint improvementGroupId, uint tier)
        {
            if (player.Path != Path.Settler || player.Map == null)
                return;

            PathSettlerImprovementGroupEntry group = GameTableManager.Instance.PathSettlerImprovementGroup.GetEntry(improvementGroupId);
            PathSettlerHubEntry hub = group == null ? null : GameTableManager.Instance.PathSettlerHub.GetEntry(group.PathSettlerHubId);
            if (hub == null)
                return;

            uint[] tiers = ImprovementTiers(group);
            if (tier >= tiers.Length)
                return;

            // a tier is built over the one before it
            BuiltImprovement built = GetBuilt(group.Id);
            if (built != null && built.Tier >= tier + 1u)
                return;

            PathSettlerImprovementEntry improvement = GameTableManager.Instance.PathSettlerImprovement.GetEntry(tiers[tier]);
            if (improvement == null)
                return;

            var costs = new[] { (hub.Item2IdResource00, improvement.CountResource00), (hub.Item2IdResource01, improvement.CountResource01), (hub.Item2IdResource02, improvement.CountResource02) }
                .Where(c => c.Item1 != 0u && c.Item2 != 0u)
                .ToList();
            if (costs.Any(c => !player.Inventory.HasItemCount(c.Item1, c.Item2)))
            {
                player.Session.EnqueueMessageEncrypted(new ServerPathSettlerBuildResult
                {
                    Result                        = 1u,
                    PathSettlerImprovementGroupId = group.Id
                });
                return;
            }

            foreach ((uint itemId, uint count) in costs)
                player.Inventory.ItemDelete(itemId, count, ItemUpdateReason.NoReason);

            builtImprovements[group.Id] = new BuiltImprovement
            {
                Tier    = tier + 1u,
                Expires = group.DurationPerBundleMs == uint.MaxValue ? null : DateTime.UtcNow.AddMilliseconds(Math.Max(group.DurationPerBundleMs, 60000u))
            };

            player.Session.EnqueueMessageEncrypted(new ServerPathSettlerBuildResult
            {
                Result                        = 0u,
                Unknown                       = 1u,
                PathSettlerImprovementGroupId = group.Id
            });

            // the depot shows the structure
            IWorldEntity depot = player.GetVisible()
                .OfType<IWorldEntity>()
                .Where(e => e.CreatureId == group.Creature2IdDepot && DepotGroups(e).Any(g => g.Id == group.Id))
                .FirstOrDefault();
            if (depot != null)
            {
                SendDepot(depot, group);
                if (improvement.Spell4IdDisplay != 0u && depot is IUnitEntity unit)
                    unit.CastSpell(improvement.Spell4IdDisplay, new SpellParameters { UserInitiatedSpellCast = false });
            }

            uint contribution = Math.Max(group.ContributionValue + tier * group.PerTierBonusContributionValue, 1u);
            foreach (PathMission mission in missions.Values.Where(m => m.Kind == MissionKind.Expansion && !m.IsComplete && m.Objects[0] == hub.Id).ToList())
                Advance(mission, Math.Min(mission.Progress + contribution, mission.Count));
        }
    }
}
