using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Configuration.Model;
using NexusForever.Game.Prerequisite;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.Game.Static.Reputation;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.PlayerPath;
using NexusForever.Shared.Configuration;
using NexusForever.Shared.Game;
using Path = NexusForever.Game.Static.PlayerPath.Path;
using NetworkMission = NexusForever.Network.World.Message.Model.PlayerPath.Mission;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Path missions for the player's active path: episode per zone, mission unlocks, progress and completion.
    /// </summary>
    /// <remarks>
    /// Supported mission kinds: kill counts (Soldier assassination), activation counts (Soldier demolition and rescue,
    /// Explorer activate), locations to visit (Explorer vista, claim, exploration, Settler scout and mayor) and quests to
    /// complete (Settler sheriff). Scientist scans, Settler hubs and infrastructure, Soldier holdouts and SWAT are not handled.
    /// </remarks>
    public class PathMissionManager : IPathMissionManager
    {
        private enum MissionKind
        {
            Unsupported,
            Kill,
            Activate,
            Locations,
            Quests,
            Cartography,
            ScavengerHunt
        }

        private class PathMission
        {
            public PathMissionEntry Entry { get; init; }
            public MissionKind Kind { get; init; }
            public uint CreatureId { get; init; }
            public uint TargetGroupId { get; init; }
            public uint Count { get; init; }
            public uint[] Objects { get; init; } = [];

            public PathMissionState State { get; set; }
            public uint Progress { get; set; }
            public bool IsNew { get; set; }
            public bool IsDirty { get; set; }

            public bool IsComplete => State == PathMissionState.Complete;
        }

        private const float MinLocationRadius = 10f;

        /// <summary>
        /// Explored percentage that completes a cartography mission, a little below 100 because some map hexes may not be reachable here.
        /// </summary>
        private const uint CartographyCompletePercent = 95u;

        private readonly IPlayer player;
        private readonly Dictionary<ushort, PathMission> missions = [];
        private PathEpisodeEntry currentEpisode;
        private readonly UpdateTimer locationTimer = new(TimeSpan.FromSeconds(1d));

        public PathMissionManager(IPlayer owner, CharacterModel model)
        {
            player = owner;

            foreach (CharacterPathMissionModel missionModel in model.PathMission)
            {
                PathMission mission = CreateMission(missionModel.MissionId);
                if (mission == null)
                    continue;

                mission.State    = (PathMissionState)missionModel.State;
                mission.Progress = missionModel.Progress;
                missions.Add(missionModel.MissionId, mission);
            }
        }

        private static PathMission CreateMission(ushort missionId)
        {
            PathMissionEntry entry = GameTableManager.Instance.PathMission.GetEntry(missionId);
            if (entry == null)
                return null;

            var type = ((Path)entry.PathTypeEnum, entry.PathMissionTypeEnum);
            switch (type)
            {
                case (Path.Soldier, 4u):
                {
                    PathSoldierAssassinateEntry assassinate = GameTableManager.Instance.PathSoldierAssassinate.GetEntry(entry.ObjectId);
                    if (assassinate == null)
                        break;
                    return new PathMission { Entry = entry, Kind = MissionKind.Kill, CreatureId = assassinate.Creature2Id, TargetGroupId = assassinate.TargetGroupId, Count = Math.Max(assassinate.Count, 1u) };
                }
                case (Path.Soldier, 7u):
                {
                    // SWAT: retail had a gadget used on the targets (VirtualItemIdDisplay), here killing them counts
                    PathSoldierSWATEntry swat = GameTableManager.Instance.PathSoldierSWAT.GetEntry(entry.ObjectId);
                    if (swat == null)
                        break;
                    return new PathMission { Entry = entry, Kind = MissionKind.Kill, CreatureId = swat.Creature2Id, TargetGroupId = swat.TargetGroupId, Count = Math.Max(swat.Count, 1u) };
                }
                case (Path.Soldier, 5u):
                case (Path.Soldier, 6u):
                {
                    PathSoldierActivateEntry activate = GameTableManager.Instance.PathSoldierActivate.GetEntry(entry.ObjectId);
                    if (activate == null)
                        break;
                    return new PathMission { Entry = entry, Kind = MissionKind.Activate, CreatureId = activate.Creature2Id, TargetGroupId = activate.TargetGroupId, Count = Math.Max(activate.Count, 1u) };
                }
                case (Path.Explorer, 17u):
                {
                    PathExplorerActivateEntry activate = GameTableManager.Instance.PathExplorerActivate.GetEntry(entry.ObjectId);
                    if (activate == null)
                        break;
                    return new PathMission { Entry = entry, Kind = MissionKind.Activate, CreatureId = activate.Creature2Id, TargetGroupId = activate.TargetGroupId, Count = Math.Max(activate.Count, 1u) };
                }
                case (Path.Explorer, 3u):
                case (Path.Explorer, 12u):
                case (Path.Explorer, 15u):
                case (Path.Explorer, 18u): // tracking: retail followed a creature or trail (PathExplorerPowerMap), here reaching its locations completes it
                case (Path.Settler, 27u):
                {
                    uint[] locations = new[] { entry.WorldLocation2Id00, entry.WorldLocation2Id01, entry.WorldLocation2Id02, entry.WorldLocation2Id03 }
                        .Where(l => l != 0u)
                        .ToArray();
                    if (locations.Length == 0)
                        break;
                    return new PathMission { Entry = entry, Kind = MissionKind.Locations, Objects = locations };
                }
                case (Path.Explorer, 13u):
                {
                    PathExplorerScavengerHuntEntry hunt = GameTableManager.Instance.PathExplorerScavengerHunt.GetEntry(entry.ObjectId);
                    if (hunt == null)
                        break;
                    uint[] clues = new[] { hunt.PathExplorerScavengerClueId00, hunt.PathExplorerScavengerClueId01, hunt.PathExplorerScavengerClueId02,
                            hunt.PathExplorerScavengerClueId03, hunt.PathExplorerScavengerClueId04, hunt.PathExplorerScavengerClueId05, hunt.PathExplorerScavengerClueId06 }
                        .Where(c => c != 0u && GameTableManager.Instance.PathExplorerScavengerClue.GetEntry(c) != null)
                        .ToArray();
                    if (clues.Length == 0)
                        break;
                    return new PathMission { Entry = entry, Kind = MissionKind.ScavengerHunt, Objects = clues };
                }
                case (Path.Explorer, 16u):
                {
                    // survey the episode zone, progress is the explored percentage of its zone map
                    PathEpisodeEntry episode = GameTableManager.Instance.PathEpisode.GetEntry(entry.PathEpisodeId);
                    if (episode == null)
                        break;
                    return new PathMission { Entry = entry, Kind = MissionKind.Cartography, Objects = [episode.WorldZoneId], Count = CartographyCompletePercent };
                }
                case (Path.Settler, 25u):
                {
                    PathSettlerMayorEntry mayor = GameTableManager.Instance.PathSettlerMayor.GetEntry(entry.ObjectId);
                    if (mayor == null)
                        break;
                    uint[] locations = new[] { mayor.WorldLocation2Id00, mayor.WorldLocation2Id01, mayor.WorldLocation2Id02, mayor.WorldLocation2Id03,
                            mayor.WorldLocation2Id04, mayor.WorldLocation2Id05, mayor.WorldLocation2Id06, mayor.WorldLocation2Id07 }
                        .Where(l => l != 0u)
                        .ToArray();
                    if (locations.Length == 0)
                        break;
                    return new PathMission { Entry = entry, Kind = MissionKind.Locations, Objects = locations };
                }
                case (Path.Settler, 26u):
                {
                    PathSettlerSheriffEntry sheriff = GameTableManager.Instance.PathSettlerSheriff.GetEntry(entry.ObjectId);
                    if (sheriff == null)
                        break;
                    uint[] quests = new[] { sheriff.Quest2IdSheriff00, sheriff.Quest2IdSheriff01, sheriff.Quest2IdSheriff02, sheriff.Quest2IdSheriff03,
                            sheriff.Quest2IdSheriff04, sheriff.Quest2IdSheriff05, sheriff.Quest2IdSheriff06, sheriff.Quest2IdSheriff07 }
                        .Where(q => q != 0u)
                        .ToArray();
                    if (quests.Length == 0)
                        break;
                    return new PathMission { Entry = entry, Kind = MissionKind.Quests, Objects = quests };
                }
            }

            return new PathMission { Entry = entry, Kind = MissionKind.Unsupported };
        }

        public void Save(CharacterContext context)
        {
            foreach ((ushort id, PathMission mission) in missions)
            {
                if (!mission.IsDirty)
                    continue;

                var model = new CharacterPathMissionModel
                {
                    Id        = player.CharacterId,
                    MissionId = id,
                    State     = (byte)mission.State,
                    Progress  = mission.Progress
                };

                if (mission.IsNew)
                    context.Add(model);
                else
                {
                    EntityEntry<CharacterPathMissionModel> entity = context.Attach(model);
                    entity.Property(p => p.State).IsModified    = true;
                    entity.Property(p => p.Progress).IsModified = true;
                }

                mission.IsNew   = false;
                mission.IsDirty = false;
            }
        }

        /// <summary>
        /// Invoked when the player enters a new zone, switches to that zone's episode for the active path.
        /// </summary>
        public void OnZoneUpdate(WorldZoneEntry zone)
        {
            if (player.Map == null || zone == null)
                return;

            var zoneIds = new HashSet<uint>();
            for (WorldZoneEntry z = zone; z != null && zoneIds.Add(z.Id); z = z.ParentZoneId != 0u ? GameTableManager.Instance.WorldZone.GetEntry(z.ParentZoneId) : null)
            {
            }

            PathEpisodeEntry episode = GameTableManager.Instance.PathEpisode.Entries
                .FirstOrDefault(e => e.WorldId == player.Map.Entry.Id && e.PathTypeEnum == (uint)player.Path && zoneIds.Contains(e.WorldZoneId));
            if (episode == null || episode == currentEpisode)
                return;

            currentEpisode = episode;
            UnlockEpisodeMissions();
            SendEpisode();
        }

        private bool IsMissionForPlayer(PathMissionEntry entry)
        {
            if (entry.PathTypeEnum != (uint)player.Path)
                return false;

            // 1 Exile, 2 Dominion, anything else both
            if (entry.PathMissionFactionEnum == 1u && player.Faction1 != Faction.Exile)
                return false;
            if (entry.PathMissionFactionEnum == 2u && player.Faction1 != Faction.Dominion)
                return false;

            return true;
        }

        private void UnlockEpisodeMissions()
        {
            foreach (PathMissionEntry entry in GameTableManager.Instance.PathMission.Entries.Where(e => e.PathEpisodeId == currentEpisode.Id))
            {
                if (!IsMissionForPlayer(entry) || missions.ContainsKey((ushort)entry.Id))
                    continue;

                if (entry.PrerequisiteId != 0u && !PrerequisiteManager.Instance.Meets(player, entry.PrerequisiteId))
                    continue;

                PathMission mission = CreateMission((ushort)entry.Id);
                if (mission == null)
                    continue;

                mission.State   = PathMissionState.Unlocked;
                mission.IsNew   = true;
                mission.IsDirty = true;
                missions.Add((ushort)entry.Id, mission);
            }
        }

        private NetworkMission Build(PathMission mission)
        {
            return new NetworkMission
            {
                PathMissionId            = mission.Entry.Id,
                Completed                = mission.IsComplete,
                ObjectiveCompletionFlags = mission.Progress,
                StateFlags               = 0u,
                State                    = mission.State,
                GiverUnitId              = 0u
            };
        }

        private void SendEpisode()
        {
            if (currentEpisode == null)
                return;

            var episodeMissions = missions.Values
                .Where(m => m.Entry.PathEpisodeId == currentEpisode.Id)
                .ToList();

            player.Session.EnqueueMessageEncrypted(new ServerPathSetCurrentEpisode
            {
                PathEpisodeId = (ushort)currentEpisode.Id
            });

            player.Session.EnqueueMessageEncrypted(new ServerPathEpisodeProgress
            {
                EpisodeId = (ushort)currentEpisode.Id,
                Missions  = episodeMissions.Select(Build).ToList()
            });

            player.Session.EnqueueMessageEncrypted(new ServerPathMissionActivate
            {
                Missions = episodeMissions
                    .Where(m => !m.IsComplete)
                    .Select(Build)
                    .ToList()
            });
        }

        public void SendInitialPackets()
        {
            // the zone can be set before or after the initial packets, send the episode either way
            currentEpisode = null;
            OnZoneUpdate(player.Zone);
        }

        public void Update(double lastTick)
        {
            locationTimer.Update(lastTick);
            if (!locationTimer.HasElapsed)
                return;

            locationTimer.Reset();

            if (player.Map == null)
                return;

            foreach (PathMission mission in missions.Values.Where(m => m.Kind == MissionKind.Cartography && !m.IsComplete).ToList())
            {
                uint percent = (uint)player.ZoneMapManager.GetExploredPercent(mission.Objects[0]);
                if (percent > mission.Progress)
                    Advance(mission, percent);
            }

            // scavenger hunt clues without a creature are found by standing at their spot
            foreach (PathMission mission in missions.Values.Where(m => m.Kind == MissionKind.ScavengerHunt && !m.IsComplete).ToList())
            {
                for (int i = 0; i < mission.Objects.Length; i++)
                {
                    uint bit = 1u << i;
                    if ((mission.Progress & bit) != 0u)
                        continue;

                    PathExplorerScavengerClueEntry clue = GameTableManager.Instance.PathExplorerScavengerClue.GetEntry(mission.Objects[i]);
                    if (clue.Creature2Id != 0u || clue.TargetGroupId != 0u)
                        continue;

                    if (IsAtLocation(clue.WorldLocation2IdMiniMap, clue.ActiveRadius))
                        Advance(mission, mission.Progress | bit);
                }
            }

            foreach (PathMission mission in missions.Values.Where(m => m.Kind == MissionKind.Locations && !m.IsComplete).ToList())
            {
                for (int i = 0; i < mission.Objects.Length && i < 32; i++)
                {
                    uint bit = 1u << i;
                    if ((mission.Progress & bit) != 0u)
                        continue;

                    WorldLocation2Entry location = GameTableManager.Instance.WorldLocation2.GetEntry(mission.Objects[i]);
                    if (location == null || location.WorldId != player.Map.Entry.Id)
                        continue;

                    float dx = player.Position.X - location.Position0;
                    float dz = player.Position.Z - location.Position2;
                    float radius = Math.Max(location.Radius, MinLocationRadius);
                    if (dx * dx + dz * dz > radius * radius)
                        continue;

                    if (location.MaxVerticalDistance > 0f && Math.Abs(player.Position.Y - location.Position1) > location.MaxVerticalDistance)
                        continue;

                    Advance(mission, mission.Progress | bit);
                }
            }
        }

        /// <summary>
        /// Invoked when the player gets kill credit for a creature.
        /// </summary>
        public void OnKill(uint creatureId, IEnumerable<uint> targetGroupIds)
        {
            OnCreature(MissionKind.Kill, creatureId, targetGroupIds);
        }

        /// <summary>
        /// Invoked when the player successfully activates a creature.
        /// </summary>
        public void OnActivate(uint creatureId, IEnumerable<uint> targetGroupIds)
        {
            OnCreature(MissionKind.Activate, creatureId, targetGroupIds);
        }

        private bool IsAtLocation(uint worldLocationId, float minRadius)
        {
            WorldLocation2Entry location = GameTableManager.Instance.WorldLocation2.GetEntry(worldLocationId);
            if (location == null || location.WorldId != player.Map.Entry.Id)
                return false;

            float dx = player.Position.X - location.Position0;
            float dz = player.Position.Z - location.Position2;
            float radius = Math.Max(Math.Max(location.Radius, minRadius), MinLocationRadius);
            if (dx * dx + dz * dz > radius * radius)
                return false;

            return location.MaxVerticalDistance <= 0f || Math.Abs(player.Position.Y - location.Position1) <= location.MaxVerticalDistance;
        }

        private void OnCreature(MissionKind kind, uint creatureId, IEnumerable<uint> targetGroupIds)
        {
            var groups = targetGroupIds.ToHashSet();

            // scavenger hunt relics are activated
            if (kind == MissionKind.Activate)
            {
                foreach (PathMission mission in missions.Values.Where(m => m.Kind == MissionKind.ScavengerHunt && !m.IsComplete).ToList())
                {
                    for (int i = 0; i < mission.Objects.Length; i++)
                    {
                        uint bit = 1u << i;
                        if ((mission.Progress & bit) != 0u)
                            continue;

                        PathExplorerScavengerClueEntry clue = GameTableManager.Instance.PathExplorerScavengerClue.GetEntry(mission.Objects[i]);
                        if ((clue.Creature2Id != 0u && clue.Creature2Id == creatureId) || (clue.TargetGroupId != 0u && groups.Contains(clue.TargetGroupId)))
                        {
                            Advance(mission, mission.Progress | bit);
                            break;
                        }
                    }
                }
            }
            foreach (PathMission mission in missions.Values.Where(m => m.Kind == kind && !m.IsComplete).ToList())
            {
                bool match = (mission.CreatureId != 0u && mission.CreatureId == creatureId)
                    || (mission.TargetGroupId != 0u && groups.Contains(mission.TargetGroupId));
                if (match)
                    Advance(mission, mission.Progress + 1u);
            }
        }

        /// <summary>
        /// Invoked when the player completes a quest.
        /// </summary>
        public void OnQuestComplete(uint questId)
        {
            foreach (PathMission mission in missions.Values.Where(m => m.Kind == MissionKind.Quests && !m.IsComplete).ToList())
            {
                int index = Array.IndexOf(mission.Objects, questId);
                if (index >= 0 && index < 32)
                    Advance(mission, mission.Progress | (1u << index));
            }
        }

        private void Advance(PathMission mission, uint progress)
        {
            mission.Progress = progress;
            mission.IsDirty  = true;

            if (mission.State == PathMissionState.Unlocked)
                mission.State = PathMissionState.Started;

            bool complete = mission.Kind switch
            {
                MissionKind.Kill or MissionKind.Activate or MissionKind.Cartography => mission.Progress >= mission.Count,
                MissionKind.Locations or MissionKind.Quests or MissionKind.ScavengerHunt => mission.Progress == (mission.Objects.Length >= 32 ? uint.MaxValue : (1u << mission.Objects.Length) - 1u),
                _ => false
            };

            if (complete)
                mission.State = PathMissionState.Complete;

            player.Session.EnqueueMessageEncrypted(new ServerPathMissionUpdate
            {
                Mission = Build(mission)
            });

            if (complete)
            {
                player.Session.EnqueueMessageEncrypted(new ServerPathMissionAdvanced
                {
                    PathMissionId = (ushort)mission.Entry.Id
                });

                PathMissionXpConfig xpConfig = SharedConfiguration.Instance.Get<RealmConfig>()?.PathMissionXp ?? new PathMissionXpConfig();
                uint xp = xpConfig.Base + xpConfig.PerLevel * player.Level;
                if (xp > 0u)
                    player.PathManager.AddXp(xp);
            }
        }
    }
}
