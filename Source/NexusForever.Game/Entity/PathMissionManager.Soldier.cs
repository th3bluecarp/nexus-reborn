using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Entity.Creature;
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
    /// in the lost server scripts. A few holdouts still have their own creatures in the client table (see holdoutRosters, the
    /// Northern Wilds ones: Fierce Yeti Icefang and the Frost Giant for M156), and those are used. For the rest each wave brings
    /// 2 more enemies than the one before (2 to 5), picked from the hostile
    /// creatures living within 150 m of the control point, spawned 14 to 18 m away and sent at the soldier. The last wave of a
    /// wave event is a boss: one of the wave raised to the Prime rank (Creature2Difficulty 5) and a level up.
    /// PathSoldierEvent.PathSoldierEventType (client enum, Holdout 0 Defend 1 Timed 2 TimedDefend 3 TowerDefense 4 WhackAMole 5
    /// WhackAMoleTimed 6 StopTheThieves 7 StopTheThievesTimed 8) decides the win: timed events are won by lasting until the
    /// event time runs out, the others by beating every wave before it does (running out of time is FailTimeOut). Both lose on
    /// death or when the soldier goes more than 60 m away.
    /// </remarks>
    public partial class PathMissionManager
    {
        private class Holdout
        {
            public PathMission Mission { get; init; }
            public PathSoldierEventEntry Event { get; init; }
            public Vector3 Center { get; init; }
            public uint[] Pool { get; init; }
            public Vector3[] SpawnPoints { get; init; }
            public int Waves { get; init; }
            public int WaveIndex { get; set; } = -1;
            public double Elapsed { get; set; }
            public double NextWave { get; set; }
            public List<uint> Spawned { get; } = [];
            // spawns join the map on a later tick, a wave is not beaten before they had time to arrive
            public double WaveSpawnedAt { get; set; } = double.MinValue;
            public bool IsTimed => Event.PathSoldierEventType is 2u or 3u or 6u or 8u;
            // the mission's own creatures from the client table, Pool is then sorted from the weakest rank up
            public bool HasRoster { get; init; }
            public uint[] Bosses { get; init; } = [];
            public bool IsDefend => Event.PathSoldierEventType is 1u or 3u;
            // defend events: the friendly units the soldier guards, the event is lost when all of them die
            public uint[] DefendIds { get; init; } = [];
            public List<uint> Defenders { get; } = [];
            public float MaxDefendHealth { get; set; }
            public int DefendersPlaced { get; set; }
            public bool DefendersReady => Defenders.Count > 0 && DefendersPlaced == DefendIds.Length;
        }

        // Creature2 rows made for one holdout, named in Description like "Northern Wilds Holdout - M156 - Bruiser [NW-hCBC]" or
        // "Northern Wilds Holdout Boss - M156 - Tank" (the Frost Giant). Rows marked [DELETE], [STUB], [PROTOTYPE] or (DEPR)
        // are old drafts and skipped. Only a few holdouts were named this way, the rest use the nearby creatures.
        private static readonly Lazy<Dictionary<uint, (uint[] Regulars, uint[] Bosses)>> holdoutRosters = new(() =>
        {
            var regex = new System.Text.RegularExpressions.Regex(@"Holdout( Boss)? - M(\d+) - ");
            return GameTableManager.Instance.Creature2.Entries
                .Where(e => e.Description != null && !e.Description.StartsWith("[") && !e.Description.StartsWith("("))
                .Select(e => (Entry: e, Match: regex.Match(e.Description)))
                .Where(x => x.Match.Success)
                .GroupBy(x => uint.Parse(x.Match.Groups[2].Value))
                .ToDictionary(g => g.Key, g => (
                    g.Where(x => !x.Match.Groups[1].Success).OrderBy(x => x.Entry.Creature2DifficultyId).Select(x => x.Entry.Id).ToArray(),
                    g.Where(x => x.Match.Groups[1].Success).Select(x => x.Entry.Id).ToArray()));
        });

        // the units a defend holdout guards, friendly (faction 1086) Creature2 rows named after the mission, like
        // "M33 - Injured Survivor 1 - Northern Wilds - Skeech Holdout" or "M1721 - Sunhorn Fawn - Celestion - Tract 1 (Holdout Defend)".
        // Only used when the event is a defend type, which keeps out the Farside flares (M1570, M1572) named the same way.
        private const uint HoldoutDefendFaction = 1086u;

        private static readonly Lazy<Dictionary<uint, uint[]>> holdoutDefendTargets = new(() =>
        {
            var regex = new System.Text.RegularExpressions.Regex(@"^M(\d+) - .*Holdout");
            return GameTableManager.Instance.Creature2.Entries
                .Where(e => e.Description != null && (uint)e.FactionId == HoldoutDefendFaction && e.PathMissionIdSoldier == 0u)
                .Select(e => (Entry: e, Match: regex.Match(e.Description)))
                .Where(x => x.Match.Success)
                .GroupBy(x => uint.Parse(x.Match.Groups[1].Value))
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Entry.Id).Select(x => x.Entry.Id).ToArray());
        });

        private const uint HoldoutBossDifficulty = 5u;

        private const float HoldoutPoolRange   = 150f;
        private const float HoldoutLeaveRange  = 60f;
        private const float HoldoutSpawnMin    = 14f;
        private const float HoldoutSpawnMax    = 18f;
        private const double HoldoutQuickWave  = 3d;
        private const float HoldoutPointMin    = 8f;
        private const float HoldoutPointMax    = 40f;
        private const double HoldoutArriveTime = 2d;
        private const float HoldoutDefendRange = 3f;

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

            // waves come from where creatures already stand: a ring around the player put them inside the rock in caves
            // (Coldburrow) and at the wrong height on slopes
            Vector3[] spawnPoints = player.Map.Search(center, HoldoutPointMax, check)
                .Where(u => u is not IPlayer && u.SummonerGuid == null)
                .Select(u => u.Position)
                .Where(p => Vector3.Distance(p, center) >= HoldoutPointMin && Vector3.Distance(p, center) <= HoldoutPointMax)
                .ToArray();

            int waves = Math.Max(GameTableManager.Instance.PathSoldierEventWave.Entries
                .Where(w => w.PathSoldierEventId == soldierEvent.Id)
                .Select(w => w.WaveIndex)
                .Distinct()
                .Count(), 3);

            bool hasRoster = holdoutRosters.Value.TryGetValue(mission.Entry.Id, out (uint[] Regulars, uint[] Bosses) roster)
                && roster.Regulars.Length > 0;

            bool isDefend = soldierEvent.PathSoldierEventType is 1u or 3u;
            uint[] defendIds = isDefend && holdoutDefendTargets.Value.TryGetValue(mission.Entry.Id, out uint[] targets) ? targets : [];

            holdout = new Holdout
            {
                Mission  = mission,
                Event    = soldierEvent,
                Center   = center,
                Pool     = hasRoster ? roster.Regulars : pool,
                HasRoster = hasRoster,
                Bosses   = hasRoster ? roster.Bosses : [],
                DefendIds = defendIds,
                SpawnPoints = spawnPoints,
                Waves    = waves,
                NextWave = soldierEvent.InitialSpawnTime / 1000d
            };

            if (mission.State == PathMissionState.Unlocked)
            {
                mission.State   = PathMissionState.Started;
                mission.IsDirty = true;
            }

            player.Session.EnqueueMessageEncrypted(HoldoutStatus(PlayerPathSoldierEventMode.InitialDelay, (int)soldierEvent.InitialSpawnTime));

            SpawnHoldoutDefenders();
        }

        /// <summary>
        /// Status for the client's holdout tracker, with the defend units so it can show their health.
        /// </summary>
        private ServerPathSoldierHoldoutStatus HoldoutStatus(PlayerPathSoldierEventMode mode, int delayTime, bool isBoss = false)
        {
            var status = new ServerPathSoldierHoldoutStatus
            {
                PathSoldierEventId = holdout.Event.Id,
                Mode               = mode,
                WaveIndex          = Math.Max(holdout.WaveIndex, 0),
                IsBoss             = isBoss,
                DelayTime          = delayTime,
                StartTimeOffset    = mode == PlayerPathSoldierEventMode.InitialDelay ? 0 : (int)(holdout.Elapsed * 1000d)
            };

            if (holdout.DefendersReady)
            {
                status.Units = holdout.Defenders
                    .Select(guid => new TowerDefenseUnit { UnitId = guid, Type = TowerDefenseUnitType.Defend })
                    .ToList();
                status.MaxDefendHealth = holdout.MaxDefendHealth;
            }

            return status;
        }

        /// <summary>
        /// Puts the units a defend holdout guards around the control point. The client is told about them once all are in.
        /// </summary>
        private void SpawnHoldoutDefenders()
        {
            if (holdout.DefendIds.Length == 0)
                return;

            var creatureInfoManager = LegacyServiceProvider.Provider.GetService<ICreatureInfoManager>();
            var entityFactory       = LegacyServiceProvider.Provider.GetService<IEntityFactory>();
            Holdout current = holdout;

            for (int i = 0; i < current.DefendIds.Length; i++)
            {
                ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(current.DefendIds[i]);
                if (creatureInfo == null)
                {
                    current.DefendersPlaced++;
                    continue;
                }

                // close to the soldier, where the ground is known to be: further out they ended up inside the rock in caves
                float angle = MathF.PI * 2f * i / current.DefendIds.Length;
                Vector3 position = current.Center + new Vector3(MathF.Cos(angle) * HoldoutDefendRange, 0f, MathF.Sin(angle) * HoldoutDefendRange);

                IWorldEntity entity = entityFactory.CreateWorldEntity(creatureInfo.Entry.CreationTypeEnum);
                entity.Initialise(creatureInfo);
                entity.Rotation = new Vector3(angle + MathF.PI, 0f, 0f);
                entity.AddToMap(player.Map, position, (map, guid, vector) =>
                {
                    current.Defenders.Add(guid);
                    current.DefendersPlaced++;
                    if (holdout != current || !current.DefendersReady)
                        return;

                    current.MaxDefendHealth = current.Defenders.Sum(g => (float)(map.GetEntity<IUnitEntity>(g)?.MaxHealth ?? 0u));
                    player.Session.EnqueueMessageEncrypted(current.WaveIndex < 0
                        ? HoldoutStatus(PlayerPathSoldierEventMode.InitialDelay, Math.Max((int)(current.NextWave * 1000d - current.Elapsed * 1000d), 0))
                        : HoldoutStatus(PlayerPathSoldierEventMode.Active, (int)(current.NextWave * 1000d - current.Elapsed * 1000d)));
                });
            }
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

            if (holdout.DefendersReady && holdout.Defenders.All(guid => player.Map.GetEntity<IUnitEntity>(guid)?.IsAlive != true))
            {
                EndHoldout(PlayerPathSoldierResult.FailDefenceDeath);
                return;
            }

            bool waveAlive = holdout.Elapsed - holdout.WaveSpawnedAt < HoldoutArriveTime || holdout.Spawned.Any(guid => player.Map.GetEntity<IUnitEntity>(guid)?.IsAlive == true);
            bool lastWave  = holdout.WaveIndex >= holdout.Waves - 1;

            if (holdout.Elapsed * 1000d >= holdout.Event.MaxEventTime)
            {
                EndHoldout(holdout.IsTimed ? PlayerPathSoldierResult.Success : PlayerPathSoldierResult.FailTimeOut);
                return;
            }

            if (!holdout.IsTimed && lastWave && !waveAlive)
            {
                EndHoldout(PlayerPathSoldierResult.Success);
                return;
            }

            // with nobody left the next wave comes sooner, the client's wave timer is told so it does not keep counting the
            // full gap (timed events keep their schedule, they are about lasting the clock)
            if (!holdout.IsTimed && !lastWave && holdout.WaveIndex >= 0 && !waveAlive
                && holdout.NextWave > holdout.Elapsed + HoldoutQuickWave)
            {
                holdout.NextWave = holdout.Elapsed + HoldoutQuickWave;
                player.Session.EnqueueMessageEncrypted(HoldoutStatus(PlayerPathSoldierEventMode.Active, (int)(HoldoutQuickWave * 1000d)));
            }

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
            player.Session.EnqueueMessageEncrypted(HoldoutStatus(PlayerPathSoldierEventMode.Active, (int)holdout.Event.MaxTimeBetweenWaves, boss));

            if (holdout.Pool.Length == 0)
                return;

            holdout.WaveSpawnedAt = holdout.Elapsed;

            var creatureInfoManager = LegacyServiceProvider.Provider.GetService<ICreatureInfoManager>();
            var entityFactory       = LegacyServiceProvider.Provider.GetService<IEntityFactory>();
            int count = Math.Min(2 + holdout.WaveIndex, 5);
            Holdout current = holdout;

            // with the mission's own creatures the waves climb the ranks: wave n draws from the weakest n/Waves of the roster,
            // and the boss wave brings the mission's boss creatures in front of the rest
            uint[] bosses = boss ? current.Bosses : [];
            int reach = current.HasRoster
                ? Math.Clamp((int)Math.Ceiling(current.Pool.Length * (current.WaveIndex + 1) / (double)current.Waves), 1, current.Pool.Length)
                : current.Pool.Length;
            count = Math.Max(count, bosses.Length);

            for (int i = 0; i < count; i++)
            {
                uint spawnId = i < bosses.Length ? bosses[i] : current.Pool[Random.Shared.Next(reach)];
                ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(spawnId);
                if (creatureInfo == null)
                    continue;

                // no boss creature known: the first of the boss wave leads it, Prime rank (more health and damage) and a level
                // above the rest (timed events with a roster have no boss, their last wave is the roster's strongest)
                if (boss && i == 0 && current.Bosses.Length == 0 && !(current.HasRoster && current.IsTimed)
                    && (creatureInfo.DifficultyEntry == null || creatureInfo.DifficultyEntry.Id < HoldoutBossDifficulty))
                {
                    Creature2DifficultyEntry bossDifficulty = GameTableManager.Instance.Creature2Difficulty.GetEntry(HoldoutBossDifficulty);
                    if (bossDifficulty != null)
                        creatureInfo = new CreatureInfoOverride()
                            .SetCreatureInfoOverride(creatureInfo)
                            .SetDifficultyEntryOverride(bossDifficulty)
                            .SetLevelOverride(creatureInfo.GetLevel() + 1u);
                }

                float angle;
                Vector3 position;
                if (current.SpawnPoints.Length > 0)
                {
                    // a little spread so a wave does not stack on one spot
                    position = current.SpawnPoints[Random.Shared.Next(current.SpawnPoints.Length)]
                        + new Vector3((float)Random.Shared.NextDouble() * 2f - 1f, 0f, (float)Random.Shared.NextDouble() * 2f - 1f);
                    angle = MathF.Atan2(position.Z - current.Center.Z, position.X - current.Center.X);
                }
                else
                {
                    angle = (float)(Random.Shared.NextDouble() * Math.PI * 2d);
                    float distance = HoldoutSpawnMin + (float)Random.Shared.NextDouble() * (HoldoutSpawnMax - HoldoutSpawnMin);
                    position = current.Center + new Vector3(MathF.Cos(angle) * distance, 0f, MathF.Sin(angle) * distance);
                }

                IWorldEntity entity = entityFactory.CreateWorldEntity(creatureInfo.Entry.CreationTypeEnum);
                entity.Initialise(creatureInfo);
                entity.Rotation = new Vector3(angle + MathF.PI, 0f, 0f);
                // in a defend event every other attacker goes for the defended units, the soldier pulls them off by fighting them
                bool goesForDefenders = i % 2 == 1;
                entity.AddToMap(player.Map, position, (map, guid, vector) =>
                {
                    current.Spawned.Add(guid);
                    if (map.GetEntity<IUnitEntity>(guid) is not IUnitEntity unit || player.Map != map)
                        return;

                    IUnitEntity[] defenders = goesForDefenders
                        ? current.Defenders.Select(g => map.GetEntity<IUnitEntity>(g)).Where(d => d?.IsAlive == true).ToArray()
                        : [];
                    unit.ThreatManager.UpdateThreat(defenders.Length > 0 ? defenders[Random.Shared.Next(defenders.Length)] : player, 10);
                });
            }
        }

        private void EndHoldout(PlayerPathSoldierResult result)
        {
            Holdout ended = holdout;
            holdout = null;

            foreach (uint guid in ended.Spawned.Concat(ended.Defenders))
                player.Map?.GetEntity<IWorldEntity>(guid)?.RemoveFromMap();

            player.Session.EnqueueMessageEncrypted(new ServerPathSoldierHoldoutEnd
            {
                PathSoldierEventId = (ushort)ended.Event.Id,
                Reason             = result
            });

            if (result == PlayerPathSoldierResult.Success)
                Advance(ended.Mission, 1u);
            else if (ended.Mission.State == PathMissionState.Started)
            {
                // a lost holdout can be tried again. Left Started it crashes the client at the next login
                ended.Mission.State   = PathMissionState.Unlocked;
                ended.Mission.IsDirty = true;
            }
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
            DismissMortarTeams();

            if (holdout != null)
                EndHoldout(PlayerPathSoldierResult.FailLeaveArea);

            // the scanbot does not follow through a teleport, the client is told it is gone
            if (scanbotGuid != null)
                DismissScanbot();
        }
    }
}
