using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Map.Search;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.PlayerPath;
using NexusForever.Shared;
using Path = NexusForever.Game.Static.PlayerPath.Path;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Soldier holdout missions (mission type 0): activate the control point, then hold it against waves of enemies.
    /// </summary>
    /// <remarks>
    /// The control point is the creature whose Creature2.PathMissionIdSoldier is the mission. PathSoldierEvent gives the start
    /// delay, the time between waves and the event length, PathSoldierEventWave the number of waves. The wave creatures were
    /// in the lost server scripts: here each wave brings 2 more enemies than the one before (2 to 5), picked from the hostile
    /// creatures living within 150 m of the control point, spawned 14 to 18 m away and sent at the soldier. The holdout is won
    /// when every wave is beaten or the event time runs out, and lost on death or when the soldier goes more than 60 m away.
    /// </remarks>
    public partial class PathMissionManager
    {
        private class Holdout
        {
            public PathMission Mission { get; init; }
            public PathSoldierEventEntry Event { get; init; }
            public Vector3 Center { get; init; }
            public uint[] Pool { get; init; }
            public int Waves { get; init; }
            public int WaveIndex { get; set; } = -1;
            public double Elapsed { get; set; }
            public double NextWave { get; set; }
            public List<uint> Spawned { get; } = [];
        }

        private const float HoldoutPoolRange   = 150f;
        private const float HoldoutLeaveRange  = 60f;
        private const float HoldoutSpawnMin    = 14f;
        private const float HoldoutSpawnMax    = 18f;
        private const double HoldoutQuickWave  = 3d;

        private Holdout holdout;

        private void StartHoldout(uint creatureId)
        {
            if (player.Path != Path.Soldier || holdout != null || player.Map == null)
                return;

            Creature2Entry creature = GameTableManager.Instance.Creature2.GetEntry(creatureId);
            if (creature == null || creature.PathMissionIdSoldier == 0u)
                return;

            if (!missions.TryGetValue((ushort)creature.PathMissionIdSoldier, out PathMission mission) || mission.Kind != MissionKind.Holdout || mission.IsComplete)
                return;

            PathSoldierEventEntry soldierEvent = GameTableManager.Instance.PathSoldierEvent.GetEntry(mission.Objects[0]);
            if (soldierEvent == null)
                return;

            Vector3 center = player.Position;
            var check = new SearchCheckRange<IUnitEntity>();
            check.Initialise(center, HoldoutPoolRange);
            uint[] pool = player.Map.Search(center, HoldoutPoolRange, check)
                .Where(u => u is not IPlayer && u.CreatureId != 0u && u.IsAlive && u.CanAttack(player) && u.SummonerGuid == null
                    && GameTableManager.Instance.Creature2.GetEntry(u.CreatureId)?.PathMissionIdSoldier == 0u
                    && u.Level <= player.Level + 3u)
                .GroupBy(u => u.CreatureId)
                .OrderByDescending(g => g.Count())
                .Take(4)
                .Select(g => g.Key)
                .ToArray();

            int waves = Math.Max(GameTableManager.Instance.PathSoldierEventWave.Entries
                .Where(w => w.PathSoldierEventId == soldierEvent.Id)
                .Select(w => w.WaveIndex)
                .Distinct()
                .Count(), 3);

            holdout = new Holdout
            {
                Mission  = mission,
                Event    = soldierEvent,
                Center   = center,
                Pool     = pool,
                Waves    = waves,
                NextWave = soldierEvent.InitialSpawnTime / 1000d
            };

            if (mission.State == PathMissionState.Unlocked)
            {
                mission.State   = PathMissionState.Started;
                mission.IsDirty = true;
            }

            player.Session.EnqueueMessageEncrypted(new ServerPathSoldierHoldoutStatus
            {
                PathSoldierEventId = soldierEvent.Id,
                Mode               = PlayerPathSoldierEventMode.InitialDelay,
                DelayTime          = (int)soldierEvent.InitialSpawnTime,
                WaveIndex          = 0
            });
        }

        private void UpdateHoldout(double lastTick)
        {
            if (holdout == null)
                return;

            holdout.Elapsed += lastTick;

            if (player.Map == null || !player.IsAlive)
            {
                EndHoldout(PlayerPathSoldierResult.FailDeath);
                return;
            }

            if (Vector3.Distance(player.Position, holdout.Center) > HoldoutLeaveRange)
            {
                EndHoldout(PlayerPathSoldierResult.FailLeaveArea);
                return;
            }

            bool waveAlive = holdout.Spawned.Any(guid => player.Map.GetEntity<IUnitEntity>(guid)?.IsAlive == true);
            bool lastWave  = holdout.WaveIndex >= holdout.Waves - 1;

            if ((lastWave && !waveAlive) || holdout.Elapsed * 1000d >= holdout.Event.MaxEventTime)
            {
                EndHoldout(PlayerPathSoldierResult.Success);
                return;
            }

            // with nobody left the next wave comes sooner
            if (!lastWave && holdout.WaveIndex >= 0 && !waveAlive)
                holdout.NextWave = Math.Min(holdout.NextWave, holdout.Elapsed + HoldoutQuickWave);

            if (!lastWave && holdout.Elapsed >= holdout.NextWave)
                SpawnHoldoutWave();
        }

        private void SpawnHoldoutWave()
        {
            holdout.WaveIndex++;
            holdout.NextWave = holdout.Elapsed + Math.Max(holdout.Event.MaxTimeBetweenWaves, 10000u) / 1000d;

            bool boss = holdout.WaveIndex == holdout.Waves - 1;
            player.Session.EnqueueMessageEncrypted(new ServerPathSoldierHoldOutNextWave
            {
                PathSoldierEventId = (ushort)holdout.Event.Id,
                WaveIndex          = (uint)holdout.WaveIndex,
                IsBoss             = boss
            });
            player.Session.EnqueueMessageEncrypted(new ServerPathSoldierHoldoutStatus
            {
                PathSoldierEventId = holdout.Event.Id,
                Mode               = PlayerPathSoldierEventMode.Active,
                WaveIndex          = holdout.WaveIndex,
                IsBoss             = boss,
                DelayTime          = (int)holdout.Event.MaxTimeBetweenWaves,
                StartTimeOffset    = (int)(holdout.Elapsed * 1000d)
            });

            if (holdout.Pool.Length == 0)
                return;

            var creatureInfoManager = LegacyServiceProvider.Provider.GetService<ICreatureInfoManager>();
            var entityFactory       = LegacyServiceProvider.Provider.GetService<IEntityFactory>();
            int count = Math.Min(2 + holdout.WaveIndex, 5);
            Holdout current = holdout;
            for (int i = 0; i < count; i++)
            {
                ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(current.Pool[Random.Shared.Next(current.Pool.Length)]);
                if (creatureInfo == null)
                    continue;

                float angle    = (float)(Random.Shared.NextDouble() * Math.PI * 2d);
                float distance = HoldoutSpawnMin + (float)Random.Shared.NextDouble() * (HoldoutSpawnMax - HoldoutSpawnMin);
                Vector3 position = current.Center + new Vector3(MathF.Cos(angle) * distance, 0f, MathF.Sin(angle) * distance);

                IWorldEntity entity = entityFactory.CreateWorldEntity(creatureInfo.Entry.CreationTypeEnum);
                entity.Initialise(creatureInfo);
                entity.Rotation = new Vector3(angle + MathF.PI, 0f, 0f);
                entity.AddToMap(player.Map, position, (map, guid, vector) =>
                {
                    current.Spawned.Add(guid);
                    if (map.GetEntity<IUnitEntity>(guid) is IUnitEntity unit && player.Map == map)
                        unit.ThreatManager.UpdateThreat(player, 10);
                });
            }
        }

        private void EndHoldout(PlayerPathSoldierResult result)
        {
            Holdout ended = holdout;
            holdout = null;

            foreach (uint guid in ended.Spawned)
                player.Map?.GetEntity<IWorldEntity>(guid)?.RemoveFromMap();

            player.Session.EnqueueMessageEncrypted(new ServerPathSoldierHoldoutEnd
            {
                PathSoldierEventId = (ushort)ended.Event.Id,
                Reason             = result
            });

            if (result == PlayerPathSoldierResult.Success)
                Advance(ended.Mission, 1u);
        }

        public void OnEntityVisible(IWorldEntity entity)
        {
            if (entity == null || entity == player)
                return;

            MarkScanTarget(entity);
            MarkSettlerDepot(entity);
        }

        public void OnRemoveFromMap()
        {
            if (holdout != null)
                EndHoldout(PlayerPathSoldierResult.FailLeaveArea);

            // the scanbot does not follow through a teleport, the client is told it is gone
            if (scanbotGuid != null)
                DismissScanbot();
        }
    }
}
