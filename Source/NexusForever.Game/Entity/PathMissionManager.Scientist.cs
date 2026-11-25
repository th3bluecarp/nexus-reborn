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
using ScanRule = NexusForever.Game.Static.PlayerPath.ScientistScanRule;

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
        /// <summary>
        /// Scan targets by path mission id, creature names (plural tag removed) that count for each group of the mission.
        /// </summary>
        private static IReadOnlyDictionary<uint, ScanRule> ScanRules => ScientistMissionRules.Scan;

        /// <summary>
        /// A scan reaches the player's target up to this distance, without a target the nearest scan target within <see cref="ScanSearchRange"/>.
        /// </summary>
        private const float ScanTargetRange = 30f;
        private const float ScanSearchRange = 15f;

        private uint? scanbotGuid;
        private readonly Dictionary<ushort, HashSet<uint>> scannedUnits = [];

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, ScanRule> fieldStudyRules = new();

        /// <summary>
        /// The scan rule of a mission: written for scans and checklists, built from the table for field studies (mission type 20).
        /// </summary>
        /// <remarks>
        /// A field study watches one creature (or target group) perform the behaviors of its checklist; the behavior events were
        /// server side and are lost, so each checklist entry is one scan of a different unit of the creature.
        /// </remarks>
        private static ScanRule RuleFor(uint missionId)
        {
            if (ScanRules.TryGetValue(missionId, out ScanRule rule))
                return rule;

            return fieldStudyRules.GetOrAdd(missionId, id =>
            {
                PathMissionEntry entry = GameTableManager.Instance.PathMission.GetEntry(id);
                if (entry == null || entry.PathMissionTypeEnum != 20u)
                    return null;

                PathScientistFieldStudyEntry study = GameTableManager.Instance.PathScientistFieldStudy.GetEntry(entry.ObjectId);
                if (study == null)
                    return null;

                var creatures = new List<uint>();
                if (study.Creature2Id != 0u)
                    creatures.Add(study.Creature2Id);
                TargetGroupEntry group = study.TargetGroupId != 0u ? GameTableManager.Instance.TargetGroup.GetEntry(study.TargetGroupId) : null;
                if (group != null && group.Type == 1u)
                    creatures.AddRange(group.DataEntries.Where(d => d != 0u));

                string[] names = creatures
                    .Select(c => GameTableManager.Instance.Creature2.GetEntry(c))
                    .Where(c => c != null)
                    .Select(c => GameTableManager.Instance.TextEnglish?.GetEntry(c.LocalizedTextIdName))
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Select(n => Regex.Replace(n, @"\{p:[^}]*\}", ""))
                    .Distinct()
                    .ToArray();
                if (names.Length == 0)
                    return null;

                uint behaviors = (uint)new[] { study.LocalizedTextIdChecklist00, study.LocalizedTextIdChecklist01, study.LocalizedTextIdChecklist02, study.LocalizedTextIdChecklist03,
                    study.LocalizedTextIdChecklist04, study.LocalizedTextIdChecklist05, study.LocalizedTextIdChecklist06, study.LocalizedTextIdChecklist07 }
                    .Count(t => t != 0u);
                return new ScanRule([names], Math.Max(behaviors, 1u));
            });
        }

        private static PathMission CreateScanMission(PathMissionEntry entry)
        {
            ScanRule rule = RuleFor(entry.Id);
            if (rule == null)
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
            ScanRule rule = RuleFor(mission.Entry.Id);
            if (name == null || rule == null)
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
            uint perGroup = RuleFor(mission.Entry.Id).ScansPerGroup;
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

        /// <summary>
        /// The progress value the client expects in ServerPathMissionUpdate (and the episode lists) for the mission.
        /// </summary>
        /// <remarks>
        /// The server keeps scan progress as a bit mask of filled slots. For a type 2 scan mission the client reads the value
        /// as a percentage: GetNumNeeded is a constant 100 and GetNumCompleted the raw value (WildStar64 0x14056d330 and
        /// 0x14056d119). The mission's scan component (0x14056b600, created by 0x140572b10 from PathScientistCreatureInfo
        /// ObjectId) fires PlayerPathScientistScanData when the value goes up, and the FloatText addon shows the
        /// "Scan Mission: name X%" progress floater on PlayerPathMissionUpdate. Type 14 checklists count set bits against
        /// PathScientistCreatureInfo.ChecklistCount on the client, so their mask is sent unchanged.
        /// </remarks>
        private static uint NetworkProgress(PathMission mission)
        {
            if (mission.Kind != MissionKind.Scan || mission.Entry.PathMissionTypeEnum != 2u || mission.Count == 0u)
                return mission.Progress;

            if (mission.IsComplete)
                return 100u;

            uint filled = (uint)BitOperations.PopCount(mission.Progress);
            return Math.Min(filled * 100u / mission.Count, 99u);
        }

        private void MarkScanTarget(IWorldEntity entity)
        {
            if (player.Path != Path.Scientist)
                return;

            (PathMission Mission, int Group)? target = FindScanMission(entity);
            if (target == null)
                return;

            // the 0x8B2 id is a PathScientistCreatureInfo (client 0x1404842a0 looks it up with 0x14021fc40), true for scan
            // missions (type 2) and checklists (type 14); a field study (type 20) object is a PathScientistFieldStudy, sent with 0x8B5
            if (target.Value.Mission.Entry.PathMissionTypeEnum == 20u)
                player.Session.EnqueueMessageEncrypted(new ServerPathScientistAddFieldStudyToCreature
                {
                    UnitId                    = entity.Guid,
                    PathScientistFieldStudyId = target.Value.Mission.Entry.ObjectId
                });
            else
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

            // the guid can belong to another unit by now (the scanbot despawned with its owner's map change and the guid was
            // reused by a creature): the cast to a pet threw and the exception dropped the session
            if (player.Map?.GetEntity<IWorldEntity>(scanbotGuid.Value) is IPetEntity scanbot)
                scanbot.RemoveFromMap();
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
                // a selected unit that was already scanned gives nothing again, clear its scannable marker so the client stops offering it
                if (player.TargetGuid != null && scannedUnits.Values.Any(s => s.Contains(player.TargetGuid.Value)))
                    player.Session.EnqueueMessageEncrypted(new ServerPathScientistUnitScanParameters
                    {
                        UnitId          = player.TargetGuid.Value,
                        ScanRewardFlags = 0,
                        IsScannable     = false
                    });

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

            // the scanbot goes to the target; its guid can belong to a creature by now (a map change despawned the bot and the
            // guid was reused): the cast to a pet threw and three BIOLOGY scans of Algoroc gave nothing
            if (player.Map.GetEntity<IWorldEntity>(scanbotGuid.Value) is IPetEntity scanbot)
                scanbot.MovementManager.Follow(target, 2f);

            player.Session.EnqueueMessageEncrypted(new ServerPathScientistUnitScanParameters
            {
                UnitId          = target.Guid,
                ScanRewardFlags = 0,
                IsScannable     = false
            });

            Advance(mission, mission.Progress | (1u << slot));
            // a scan is a scan: a unit named by a scan mission standing at a specimen survey location samples it too
            // (Augmented Life's second location is inside the Augmented XAS Scientists of BIOLOGY: Augmented Scientists)
            ScanSpecimen(target);
        }
    }
}
