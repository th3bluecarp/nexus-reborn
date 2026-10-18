using Microsoft.Extensions.DependencyInjection;
using System.Numerics;
using System.Text.RegularExpressions;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.PlayerPath;
using NexusForever.Shared;
using Path = NexusForever.Game.Static.PlayerPath.Path;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Scientist scan missions (mission type 2, and type 14 checklists that need several different units) and the scanbot.
    /// </summary>
    /// <remarks>
    /// Retail decided on the server which creatures belong to a scan mission and told the client with
    /// ServerPathScientistAddCreatureInfoToCreature. That mapping is lost, it is rebuilt here from the mission texts as creature
    /// names (<see cref="ScanRules"/>, zone by zone). Every group of names has a number of scans to make, each scan of a
    /// different unit fills one slot, the mission progress is the bit mask of filled slots.
    /// The client sends 0x180 to summon the scanbot, 0xF0 to dismiss it and 0x15F when the path action button scans.
    /// </remarks>
    public partial class PathMissionManager
    {
        private record ScanRule(string[][] Groups, uint ScansPerGroup);

        /// <summary>
        /// Scan targets by path mission id, creature names (plural tag removed) that count for each group of the mission.
        /// </summary>
        private static readonly Dictionary<uint, ScanRule> ScanRules = new()
        {
            // Northern Wilds
            [42u]  = new([["Dominion Turret", "Inactive Dominion Turret"], ["Dominion Secure Database", "Dominion Secure Database Hologram"]], 1u), // ANALYSIS: Turning the Tide
            [160u] = new([["Vitalium Crystal"]], 3u),                                                     // CHEMISTRY: Vitalium Crystals
            [648u] = new([["Skeech Diviner", "Skeech Scratcher", "Skeech Shaman", "Cleaved Skeech Shaman"]], 3u), // BIOLOGY: Skeech Physiology
            // Algoroc (Grimstone Girrok 264, Gronyx 826 and Stumpkins 827 have no spawns anywhere in Algoroc, no rule)
            [201u]  = new([["Irradiated Icetail Leaper", "Irradiated Icetail Pouncer", "Irradiated Icetail Slayer", "Irradiated Icetail Stalker", "Irradiated Snoglug"]], 3u), // BIOLOGY: Irradiated Wildlife
            [218u]  = new([["Large Loftite Formation"]], 3u),       // CHEMISTRY: Loftite Crystals
            [266u]  = new([["Fiera Flower"]], 3u),                  // BOTANY: Fiera Flowers
            [1978u] = new([["Augmented XAS Scientist"]], 3u),       // BIOLOGY: Augmented Scientists
            [2139u] = new([["Eldan Repair Construct"]], 3u),        // ANALYSIS: Eldan Constructs
            [262u]  = new([["Darkspur Cartel Terminal"]], 5u),      // ANALYSIS: Darkspur Cartel Terminals (checklist of 5)
            [701u]  = new([["Eldan Relic", "Eldan Relics"]], 8u),   // ARCHAEOLOGY: Excavated Artifacts (checklist of 8)
            [2132u] = new([["DREDplex Data Terminal"]], 10u),       // ANALYSIS: DREDplex Weapon Designs (checklist of 10)
            [2133u] = new([["Deadstar Data Terminal", "Deadstar Footlocker", "Deadstar Clothesline"]], 3u), // ANALYSIS: Deadeye and the Deadstar (checklist of 3)
            // Celestion
            [1714u] = new([["Hoogle Guardian Totem", "Transmorphing Flight Hoogle Totem"]], 3u), // ANALYSIS: Hoogle Totems
            [1719u] = new([["Draken Garden Gnome"], ["Draken Backscratcher"], ["Draken Chamberpot"]], 1u), // ANALYSIS: Draken Implements
            [1724u] = new([["Weave Portal"]], 3u),                  // BOTANY: Weave Portals (checklist of 3)
            [1725u] = new([["Moodie Mojo"]], 3u),                   // ARCHAEOLOGY: Moodie Mojo
            [1828u] = new([["Augmented Celestial Razortusk", "Augmented Nightfang Creeper", "Augmented Nightfang Weaver", "Augmented Nightfang Widow", "Augmented Buzzbing", "Augmented Nightfang Flayer"]], 3u), // BIOLOGY: Augmented Creatures
            [1830u] = new([["Amorous Rowsdower"]], 10u),            // CHEMISTRY: Lessons in Love (checklist of 10)
            [1840u] = new([["Rowsdower's Corpse", "Sunhorn Roamer's Corpse", "Ravenous Corpse"]], 3u), // BIOLOGY: That Special Glow
            [1841u] = new([["Mordesh Vitality Extractor"]], 3u),    // DIAGNOSTICS: Mordesh Machinery
            [1845u] = new([["Water of Life"]], 5u),                 // CHEMISTRY: Water of Life (checklist of 5)
            [1847u] = new([["Ravenous Ripper", "Ravenous Ghoul", "Ravenous Horror", "Ravenous Fiend", "Ravenous Reaver", "Ravenous Malignant", "Ravenous Mordesh"]], 3u), // BIOLOGY: Ravenous Ruminations
            [1854u] = new([["Communication Controls", "Communication Dish"]], 4u), // ANALYSIS: Eldan Communications (checklist of 4)
            [1859u] = new([["Shield Generator", "Bio-Containment Shield"]], 3u), // DIAGNOSTICS: Switching Safeguards (checklist of 3)
        };

        /// <summary>
        /// A scan reaches the player's target up to this distance, without a target the nearest scan target within <see cref="ScanSearchRange"/>.
        /// </summary>
        private const float ScanTargetRange = 30f;
        private const float ScanSearchRange = 15f;

        private uint? scanbotGuid;
        private readonly Dictionary<ushort, HashSet<uint>> scannedUnits = [];

        private static PathMission CreateScanMission(PathMissionEntry entry)
        {
            if (!ScanRules.TryGetValue(entry.Id, out ScanRule rule))
                return null;

            uint slots = Math.Min((uint)rule.Groups.Length * rule.ScansPerGroup, 32u);
            return new PathMission { Entry = entry, Kind = MissionKind.Scan, Objects = [entry.ObjectId], Count = slots };
        }

        private static string CreatureName(IWorldEntity entity)
        {
            if (entity.CreatureId == 0u)
                return null;

            Creature2Entry creature = GameTableManager.Instance.Creature2.GetEntry(entity.CreatureId);
            if (creature == null)
                return null;

            string name = GameTableManager.Instance.TextEnglish?.GetEntry(creature.LocalizedTextIdName);
            return name == null ? null : Regex.Replace(name, @"\{p:[^}]*\}", "");
        }

        /// <summary>
        /// Return the group of the scan rule the entity belongs to, -1 when it is not a target of the mission.
        /// </summary>
        private static int ScanGroup(PathMission mission, string name)
        {
            if (name == null || !ScanRules.TryGetValue(mission.Entry.Id, out ScanRule rule))
                return -1;

            for (int i = 0; i < rule.Groups.Length; i++)
                if (rule.Groups[i].Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                    return i;

            return -1;
        }

        /// <summary>
        /// Return the first free slot of the group, null when the group has all its scans.
        /// </summary>
        private static int? FreeScanSlot(PathMission mission, int group)
        {
            uint perGroup = ScanRules[mission.Entry.Id].ScansPerGroup;
            for (int i = 0; i < perGroup; i++)
            {
                int slot = group * (int)perGroup + i;
                if (slot < 32 && (mission.Progress & (1u << slot)) == 0u)
                    return slot;
            }

            return null;
        }

        private IEnumerable<PathMission> ActiveScanMissions()
        {
            return missions.Values.Where(m => m.Kind == MissionKind.Scan && !m.IsComplete && m.Entry.PathTypeEnum == (uint)player.Path);
        }

        /// <summary>
        /// The scan mission the entity can still count for, with the group it belongs to.
        /// </summary>
        private (PathMission Mission, int Group)? FindScanMission(IWorldEntity entity)
        {
            string name = CreatureName(entity);
            if (name == null)
                return null;

            foreach (PathMission mission in ActiveScanMissions())
            {
                if (scannedUnits.TryGetValue((ushort)mission.Entry.Id, out HashSet<uint> scanned) && scanned.Contains(entity.Guid))
                    continue;

                int group = ScanGroup(mission, name);
                if (group >= 0 && FreeScanSlot(mission, group) != null)
                    return (mission, group);
            }

            return null;
        }

        private void MarkScanTarget(IWorldEntity entity)
        {
            if (player.Path != Path.Scientist)
                return;

            (PathMission Mission, int Group)? target = FindScanMission(entity);
            if (target == null)
                return;

            player.Session.EnqueueMessageEncrypted(new ServerPathScientistAddCreatureInfoToCreature
            {
                UnitId                      = entity.Guid,
                PathScientistCreatureInfoId = target.Value.Mission.Entry.ObjectId
            });
            player.Session.EnqueueMessageEncrypted(new ServerPathScientistUnitScanParameters
            {
                UnitId          = entity.Guid,
                ScanRewardFlags = ScanReward.RewardForScan,
                IsScannable     = true
            });
        }

        public void SummonScanbot(uint profileId)
        {
            if (player.Path != Path.Scientist || player.Map == null)
                return;

            if (scanbotGuid != null && player.Map.GetEntity<IPetEntity>(scanbotGuid.Value) != null)
                return;

            PathScientistScanBotProfileEntry profile = GameTableManager.Instance.PathScientistScanBotProfile.GetEntry(profileId == 0u ? 1u : profileId)
                ?? GameTableManager.Instance.PathScientistScanBotProfile.GetEntry(1u);
            if (profile == null)
                return;

            ICreatureInfo creatureInfo = LegacyServiceProvider.Provider.GetService<ICreatureInfoManager>().GetCreatureInfo(profile.Creature2Id);
            if (creatureInfo == null)
                return;

            var scanbot = LegacyServiceProvider.Provider.GetService<IEntityFactory>().CreateEntity<IPetEntity>();
            scanbot.Initialise(player, creatureInfo, true);
            scanbot.AddToMap(player.Map, player.Position + new Vector3(1.5f, 0f, 1.5f), (map, guid, vector) =>
            {
                scanbotGuid = guid;
                player.Session.EnqueueMessageEncrypted(new ServerPathScientistScanbotState
                {
                    ScanbotUnitId = guid
                });
            });
        }

        public void DismissScanbot()
        {
            if (scanbotGuid == null)
                return;

            player.Map?.GetEntity<IPetEntity>(scanbotGuid.Value)?.RemoveFromMap();
            scanbotGuid = null;

            player.Session.EnqueueMessageEncrypted(new ServerPathScientistScanbotState
            {
                ScanbotUnitId     = 0u,
                ScanbotCooldownMS = 0u
            });
        }

        public void Scan()
        {
            if (player.Path != Path.Scientist || player.Map == null)
                return;

            // the path action scans only with the scanbot out, without one the client asks for it instead (0x180)
            if (scanbotGuid == null)
            {
                SummonScanbot(0u);
                return;
            }

            IWorldEntity target = null;
            if (player.TargetGuid != null)
            {
                IWorldEntity selected = player.GetVisible<IWorldEntity>(player.TargetGuid.Value);
                if (selected != null && Vector3.Distance(selected.Position, player.Position) <= ScanTargetRange && FindScanMission(selected) != null)
                    target = selected;
            }

            target ??= player.GetVisible()
                .OfType<IWorldEntity>()
                .Where(e => e != player && Vector3.Distance(e.Position, player.Position) <= ScanSearchRange)
                .OrderBy(e => Vector3.DistanceSquared(e.Position, player.Position))
                .FirstOrDefault(e => FindScanMission(e) != null);

            if (target == null)
            {
                // a specimen survey takes a sample from the target, or the nearest creature, at its locations
                IWorldEntity specimen = player.TargetGuid != null ? player.GetVisible<IWorldEntity>(player.TargetGuid.Value) : null;
                specimen ??= player.GetVisible()
                    .OfType<IUnitEntity>()
                    .Where(e => e is not IPlayer && e.IsAlive && Vector3.Distance(e.Position, player.Position) <= ScanSearchRange)
                    .OrderBy(e => Vector3.DistanceSquared(e.Position, player.Position))
                    .FirstOrDefault();
                if (specimen != null && Vector3.Distance(specimen.Position, player.Position) <= ScanTargetRange)
                    ScanSpecimen(specimen);
                return;
            }

            (PathMission mission, int group) = FindScanMission(target).Value;
            int slot = FreeScanSlot(mission, group).Value;

            if (!scannedUnits.TryGetValue((ushort)mission.Entry.Id, out HashSet<uint> scanned))
                scannedUnits.Add((ushort)mission.Entry.Id, scanned = []);
            scanned.Add(target.Guid);

            // the scanbot goes to the target
            IPetEntity scanbot = player.Map.GetEntity<IPetEntity>(scanbotGuid.Value);
            scanbot?.MovementManager.Follow(target, 2f);

            player.Session.EnqueueMessageEncrypted(new ServerPathScientistUnitScanParameters
            {
                UnitId          = target.Guid,
                ScanRewardFlags = 0,
                IsScannable     = false
            });

            Advance(mission, mission.Progress | (1u << slot));
        }
    }
}
