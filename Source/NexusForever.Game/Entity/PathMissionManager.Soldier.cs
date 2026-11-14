using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Entity.Creature;
using NexusForever.Game.Map.Search;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model.PlayerPath;
using NexusForever.Shared;
using NLog;
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
    /// wave event is a boss: one of the wave raised to the Prime rank (Creature2Difficulty 6, health x3.5) with Superior damage ratings at the highest level
    /// of the wave and drawn larger, and a roster boss that does not outrank its wave is raised the same way. In an event won by
    /// beating every wave a raised boss whose wave could not fall in the event time left keeps Prime damage and rank but gets the
    /// health that fits (FitHoldoutBossHealth).
    /// PathSoldierEvent.PathSoldierEventType (client enum, Holdout 0 Defend 1 Timed 2 TimedDefend 3 TowerDefense 4 WhackAMole 5
    /// WhackAMoleTimed 6 StopTheThieves 7 StopTheThievesTimed 8) decides the win: timed events are won by lasting until the
    /// event time runs out, the others by beating every wave before it does (running out of time is FailTimeOut). Both lose on
    /// death or when the soldier goes more than 60 m away. Defend events (types 1 and 3) also spawn the friendly units to guard
    /// when the client table names them (holdoutDefendTargets), and are lost when all of them die.
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
            // wave creatures that stay far from the soldier (stuck on the way, spawned on the far side) and since when
            public Dictionary<uint, double> FarSince { get; } = [];
        }

        private static readonly ILogger holdoutLog = LogManager.GetLogger("Holdout");

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

        // Prime. Creature2Difficulty 5 is Superior (health x0.96), 6 is Prime (health x3.5, damage ratings x2.65 against x1.75)
        private const uint HoldoutBossDifficulty = 6u;
        // model scale of a generic boss, the one visible mark that it is not one of the wave (the same SetScale command the scale spells use)
        private const float HoldoutBossScale = 1.5f;
        // a boss raised to Prime in a kill-everything event: at the pace the regular waves ask for, the whole boss wave should fall within
        // this share of the event time left, otherwise the boss gets the health that fits (Prime x3.5 health could not die in time, M1850)
        private const double HoldoutBossTimeShare = 0.5d;
        // a fitted boss never drops below Superior health (the boss rank before Prime) nor below this many times its strongest regular
        private const uint HoldoutBossFloorDifficulty = 5u;
        private const float HoldoutBossFloorRegular = 1.5f;
        // a boss raised to Prime hits like a Superior (the boss rank before Prime) or its own rank if that hits harder: Prime damage ratings
        // (x2.65) on top of Prime health killed level-appropriate soldiers in the boss wave (M314 passed 5 runs as Superior, died in wave 4 every run as Prime)
        private const uint HoldoutBossDamageDifficulty = 5u;
        // pool creatures of this Creature2Difficulty and above are elites and stay out of the waves
        private const uint HoldoutPoolMaxDifficulty = 4u;

        private const float HoldoutPoolRange   = 150f;
        private const float HoldoutLeaveRange  = 60f;
        private const float HoldoutSpawnMin    = 14f;
        private const float HoldoutSpawnMax    = 18f;
        private const double HoldoutQuickWave  = 3d;
        private const float HoldoutPointMin    = 8f;
        private const float HoldoutPointMax    = 40f;
        private const double HoldoutArriveTime = 2d;
        private const float HoldoutDefendRange = 3f;
        // a wave creature this far from the soldier for this long is brought next to the soldier: a creature stuck on the way
        // (cave walls, slopes, no path) otherwise keeps the wave alive until the event clock runs out
        private const float HoldoutStragglerRange  = 30f;
        private const double HoldoutStragglerTime  = 10d;
        private const float HoldoutStragglerBring  = 6f;

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
            IUnitEntity[] nearby = player.Map.Search(center, HoldoutPoolRange, check)
                .Where(u => u is not IPlayer && u.CreatureId != 0u && u.IsAlive && u.CanAttack(player) && u.SummonerGuid == null
                    && GameTableManager.Instance.Creature2.GetEntry(u.CreatureId)?.PathMissionIdSoldier == 0u
                    && u.Level <= player.Level + 3u)
                .ToArray();
            // elites (Superior rank and up, 1.66x damage and more) are for groups: left out of a lone soldier's waves while
            // anything else lives nearby (M314 drew its Prime-rank Site 14 guard four times in a row and killed the bot)
            IUnitEntity[] regulars = nearby
                .Where(u => (GameTableManager.Instance.Creature2.GetEntry(u.CreatureId)?.Creature2DifficultyId ?? 0u) < HoldoutPoolMaxDifficulty)
                .ToArray();
            uint[] pool = (regulars.Length > 0 ? regulars : nearby)
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

            holdoutLog.Info($"M{mission.Entry.Id} event {soldierEvent.Id} type {soldierEvent.PathSoldierEventType} started by {player.Name} (level {player.Level}, "
                + $"health {player.Health}/{player.MaxHealth}) at {center}: {waves} waves, {holdout.Pool.Length} creature(s) in the pool [{string.Join(",", holdout.Pool)}]"
                + $"{(hasRoster ? " (roster)" : "")}, {holdout.Bosses.Length} boss(es), {spawnPoints.Length} spawn point(s), {defendIds.Length} defender(s), "
                + $"event time {soldierEvent.MaxEventTime} ms");

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

            BringHoldoutStragglers();

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

        /// <summary>
        /// A live wave creature that has stayed more than <see cref="HoldoutStragglerRange"/> from the soldier for
        /// <see cref="HoldoutStragglerTime"/> is set down next to the soldier and sent at them again.
        /// </summary>
        private void BringHoldoutStragglers()
        {
            Holdout current = holdout;
            foreach (uint guid in current.Spawned)
            {
                IUnitEntity unit = player.Map.GetEntity<IUnitEntity>(guid);
                if (unit == null || !unit.IsAlive)
                {
                    current.FarSince.Remove(guid);
                    continue;
                }

                float distance = Vector3.Distance(unit.Position, player.Position);
                if (distance <= HoldoutStragglerRange)
                {
                    current.FarSince.Remove(guid);
                    continue;
                }

                if (!current.FarSince.TryGetValue(guid, out double since))
                {
                    current.FarSince[guid] = current.Elapsed;
                    continue;
                }

                if (current.Elapsed - since < HoldoutStragglerTime)
                    continue;

                current.FarSince.Remove(guid);
                Vector3 direction = Vector3.Normalize(new Vector3(unit.Position.X - player.Position.X, 0f, unit.Position.Z - player.Position.Z));
                if (float.IsNaN(direction.X))
                    direction = Vector3.UnitX;
                Vector3 position = player.Position + direction * HoldoutStragglerBring;

                holdoutLog.Info($"M{current.Mission.Entry.Id} wave {current.WaveIndex + 1}: creature {unit.CreatureId} guid {guid} stayed {distance:0} m away "
                    + $"for {HoldoutStragglerTime:0} s at {unit.Position}, brought to {position}");

                IPlayer soldier = player;
                unit.RelocateOnMap(position, vector =>
                {
                    unit.MovementManager.SetPosition(vector, false);
                    unit.MovementManager.BroadcastNetworkEntityCommands();
                    unit.ThreatManager.UpdateThreat(soldier, 10);
                });
            }
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
            {
                holdoutLog.Warn($"M{holdout.Mission.Entry.Id} wave {holdout.WaveIndex + 1}: no creatures in the pool, nothing spawned");
                return;
            }

            holdout.WaveSpawnedAt = holdout.Elapsed;
            holdoutLog.Info($"M{holdout.Mission.Entry.Id} wave {holdout.WaveIndex + 1}/{holdout.Waves}{(boss ? " (boss)" : "")} at {holdout.Elapsed:0.0} s, "
                + $"soldier health {player.Health}/{player.MaxHealth}");

            var creatureInfoManager = LegacyServiceProvider.Provider.GetService<ICreatureInfoManager>();
            var entityFactory       = LegacyServiceProvider.Provider.GetService<IEntityFactory>();
            // a solo fight: two then three enemies a wave, and the boss wave is the boss with two companions (waves of 2 to 5
            // piled up on a lone soldier before the boss came; the bot died at 4758/9380 health entering the boss wave of M314)
            int count = boss ? 3 : Math.Min(2 + holdout.WaveIndex, 3);
            Holdout current = holdout;

            // with the mission's own creatures the waves climb the ranks: wave n draws from the weakest n/Waves of the roster,
            // and the boss wave brings the mission's boss creatures in front of the rest
            uint[] bosses = boss ? current.Bosses : [];
            int reach = current.HasRoster
                ? Math.Clamp((int)Math.Ceiling(current.Pool.Length * (current.WaveIndex + 1) / (double)current.Waves), 1, current.Pool.Length)
                : current.Pool.Length;
            count = Math.Max(count, bosses.Length);

            // the boss wave is built up front so the boss can be set against the rest of it: levels are rolled now (not at spawn)
            uint[] spawnIds = new uint[count];
            ICreatureInfo[] infos = new ICreatureInfo[count];
            for (int i = 0; i < count; i++)
            {
                spawnIds[i] = i < bosses.Length ? bosses[i] : current.Pool[Random.Shared.Next(reach)];
                ICreatureInfo info = creatureInfoManager.GetCreatureInfo(spawnIds[i]);
                if (info != null && boss)
                    info = new CreatureInfoOverride().SetCreatureInfoOverride(info).SetLevelOverride(info.GetLevel());
                infos[i] = info;
            }

            int genericBoss = boss ? PromoteHoldoutBosses(current, bosses.Length, infos) : -1;

            for (int i = 0; i < count; i++)
            {
                uint spawnId = spawnIds[i];
                ICreatureInfo creatureInfo = infos[i];
                if (creatureInfo == null)
                    continue;

                bool largeBoss = i == genericBoss;

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
                ICreatureInfo spawnedInfo = creatureInfo;
                entity.AddToMap(player.Map, position, (map, guid, vector) =>
                {
                    current.Spawned.Add(guid);
                    if (map.GetEntity<IUnitEntity>(guid) is not IUnitEntity unit || player.Map != map)
                        return;

                    if (largeBoss)
                        unit.MovementManager.SetScale(HoldoutBossScale);

                    holdoutLog.Info($"M{current.Mission.Entry.Id} wave {current.WaveIndex + 1}: spawned creature {spawnId} guid {guid} level {unit.Level} "
                        + $"difficulty {spawnedInfo.DifficultyEntry?.Id ?? 0u} health {unit.MaxHealth} at {vector} ({Vector3.Distance(vector, current.Center):0} m from the point)");

                    IUnitEntity[] defenders = goesForDefenders
                        ? current.Defenders.Select(g => map.GetEntity<IUnitEntity>(g)).Where(d => d?.IsAlive == true).ToArray()
                        : [];
                    unit.ThreatManager.UpdateThreat(defenders.Length > 0 ? defenders[Random.Shared.Next(defenders.Length)] : player, 10);
                });
            }
        }

        /// <summary>
        /// Raise the boss of a boss wave above the rest of it. Returns the index of a generic boss (made here from an ordinary wave creature), else -1.
        /// </summary>
        /// <remarks>
        /// A roster boss whose difficulty is not above every regular creature of the wave is raised to Prime and to the highest level of the wave
        /// (M156's Frost Giant is Superior like the roster creatures it leads). Without a roster boss the first creature of the wave is promoted the same way.
        /// </remarks>
        private int PromoteHoldoutBosses(Holdout current, int rosterBosses, ICreatureInfo[] infos)
        {
            Creature2DifficultyEntry prime = GameTableManager.Instance.Creature2Difficulty.GetEntry(HoldoutBossDifficulty);
            Creature2DifficultyEntry damageRank = GameTableManager.Instance.Creature2Difficulty.GetEntry(HoldoutBossDamageDifficulty);
            if (prime == null || infos.All(i => i == null))
                return -1;

            uint waveLevel = infos.Where(i => i != null).Max(i => i.GetLevel());
            uint regularRank = infos.Skip(rosterBosses).Where(i => i != null).Select(i => i.DifficultyEntry?.Id ?? 0u).DefaultIfEmpty(0u).Max();

            var raised = new List<int>();
            void Raise(int i)
            {
                infos[i] = new CreatureInfoOverride()
                    .SetCreatureInfoOverride(infos[i])
                    .SetDifficultyEntryOverride(WithBossDamage(prime, damageRank, infos[i].DifficultyEntry))
                    .SetLevelOverride(waveLevel);
                raised.Add(i);
            }

            for (int i = 0; i < Math.Min(rosterBosses, infos.Length); i++)
            {
                uint rank = infos[i]?.DifficultyEntry?.Id ?? 0u;
                if (infos[i] == null || rank >= HoldoutBossDifficulty || rank > regularRank)
                    continue;

                holdoutLog.Info($"M{current.Mission.Entry.Id} boss {infos[i].Entry.Id} (difficulty {rank}) does not outrank its wave (difficulty {regularRank}), raised to Prime at level {waveLevel}");
                Raise(i);
            }

            // no boss creature known: the first of the boss wave leads it (timed events with a roster have no boss, their last wave is the roster's strongest)
            int generic = -1;
            if (rosterBosses == 0 && !(current.HasRoster && current.IsTimed) && infos[0] != null && (infos[0].DifficultyEntry?.Id ?? 0u) < HoldoutBossDifficulty)
            {
                holdoutLog.Info($"M{current.Mission.Entry.Id} boss wave has no boss creature, creature {infos[0].Entry.Id} made Prime at level {waveLevel}");
                Raise(0);
                generic = 0;
            }

            // timed events are won by lasting the clock, their boss never has to die
            if (!current.IsTimed && raised.Count > 0)
                FitHoldoutBossHealth(current, rosterBosses, raised, infos);

            return generic;
        }

        /// <summary>
        /// Lower the health of the bosses raised to Prime so the boss wave of a kill-everything holdout can be beaten in the event time left.
        /// </summary>
        /// <remarks>
        /// The pace a soldier is expected to keep is the one the regular waves ask for: a wave of the last regular size, made of creatures like
        /// the boss wave's regulars, beaten within one wave timer (PathSoldierEvent.MaxTimeBetweenWaves). At that pace the whole boss wave
        /// should fall within <see cref="HoldoutBossTimeShare"/> of the event time left (MaxEventTime minus the time spent); a boss that would
        /// not gets the health that fits, never less than Superior health or <see cref="HoldoutBossFloorRegular"/> times the strongest regular,
        /// and keeps Prime damage, rank and size. M1850 (event 330, 32 s waves, 240 s event) reached its boss wave with 111.5 s left and a Prime
        /// boss of 17,950 health, about 126 s of work at that pace, and failed on time.
        /// </remarks>
        private void FitHoldoutBossHealth(Holdout current, int rosterBosses, List<int> raised, ICreatureInfo[] infos)
        {
            int[] regulars = Enumerable.Range(0, infos.Length)
                .Where(i => i >= rosterBosses && infos[i] != null && !raised.Contains(i))
                .ToArray();
            if (regulars.Length == 0)
            {
                holdoutLog.Info($"M{current.Mission.Entry.Id} boss wave has no regular creature to set the pace, boss health left at Prime");
                return;
            }

            float[] regularHealth = regulars.Select(i => HoldoutCreatureHealth(infos[i], infos[i].DifficultyEntry)).ToArray();
            int regularWaveSize = Math.Min(2 + Math.Max(current.Waves - 2, 0), 3);
            double waveTime = Math.Max(current.Event.MaxTimeBetweenWaves, 10000u) / 1000d;
            double pace = regularWaveSize * regularHealth.Average() / waveTime;
            double timeLeft = Math.Max(current.Event.MaxEventTime / 1000d - current.Elapsed, 0d);

            // the regulars and any roster boss that kept its own rank take their share of the time first
            double otherHealth = regularHealth.Sum() + Enumerable.Range(0, Math.Min(rosterBosses, infos.Length))
                .Where(i => infos[i] != null && !raised.Contains(i))
                .Sum(i => (double)HoldoutCreatureHealth(infos[i], infos[i].DifficultyEntry));
            double share = (pace * timeLeft * HoldoutBossTimeShare - otherHealth) / raised.Count;

            Creature2DifficultyEntry superior = GameTableManager.Instance.Creature2Difficulty.GetEntry(HoldoutBossFloorDifficulty);
            foreach (int i in raised)
            {
                if (infos[i] is not ICreatureInfoOverride boss || boss.DifficultyEntry?.UnitPropertyMultiplier == null)
                    continue;

                // a fixed health property replaces the rank multiplier altogether, there is nothing to scale
                if (boss.GetPropertyOverrides().Any(p => p.Property == Property.BaseHealth))
                {
                    holdoutLog.Info($"M{current.Mission.Entry.Id} boss {boss.Entry.Id} has a fixed health property, health not fitted to the time left");
                    continue;
                }

                float unranked = HoldoutCreatureHealth(boss, null);
                if (unranked <= 0f)
                    continue;

                float primeHealth = unranked * boss.DifficultyEntry.UnitPropertyMultiplier[(uint)Property.BaseHealth];
                float floor = Math.Max(
                    superior != null ? unranked * superior.UnitPropertyMultiplier[(uint)Property.BaseHealth] : 0f,
                    HoldoutBossFloorRegular * regularHealth.Max());
                float health = (float)Math.Clamp(share, Math.Min(floor, primeHealth), primeHealth);

                string numbers = $"{timeLeft:0.0} s left, pace {pace:0} health/s from {regulars.Length} regular(s) and {waveTime:0} s waves, "
                    + $"boss share {Math.Max(share, 0d):0}, floor {floor:0}, Prime {primeHealth:0}";
                if (health >= primeHealth)
                {
                    holdoutLog.Info($"M{current.Mission.Entry.Id} boss {boss.Entry.Id} keeps Prime health {primeHealth:0} ({numbers})");
                    continue;
                }

                boss.SetDifficultyEntryOverride(WithHealthMultiplier(boss.DifficultyEntry, health / unranked));
                holdoutLog.Info($"M{current.Mission.Entry.Id} boss {boss.Entry.Id} health fitted to the time left: {health:0} ({numbers}), "
                    + $"boss wave about {(otherHealth + health * raised.Count) / pace:0} s at that pace");
            }
        }

        /// <summary>
        /// Max health a creature spawns with, the way <see cref="NonPlayerEntity"/> works it out: the level's base health times the archetype,
        /// rank (<paramref name="difficulty"/>, none for the health without a rank) and tier multipliers, unless a fixed health property replaces it.
        /// </summary>
        private static float HoldoutCreatureHealth(ICreatureInfo info, Creature2DifficultyEntry difficulty)
        {
            ICreatureInfoProperty fixedHealth = info.GetPropertyOverrides().FirstOrDefault(p => p.Property == Property.BaseHealth);
            if (fixedHealth != null)
                return fixedHealth.Value;

            CreatureLevelEntry level = GameTableManager.Instance.CreatureLevel.GetEntry(info.GetLevel());
            if (level == null)
                return 0f;

            const uint index = (uint)Property.BaseHealth;
            float health = level.UnitPropertyValue[index];
            health *= info.ArcheTypeEntry?.UnitPropertyMultiplier[index] ?? 1f;
            health *= difficulty?.UnitPropertyMultiplier[index] ?? 1f;
            health *= info.TierEntry?.UnitPropertyMultiplier[index] ?? 1f;
            return health;
        }

        /// <summary>
        /// A copy of <paramref name="prime"/> (same id, so the same rank everywhere) whose damage ratings (assault and support, what
        /// <see cref="NexusForever.Game.Combat.DamageCalculator"/> scales spell damage by) are those of <paramref name="damageRank"/>,
        /// or of the creature's own rank (<paramref name="own"/>) where that is higher. Health and every other multiplier stay Prime.
        /// </summary>
        private static Creature2DifficultyEntry WithBossDamage(Creature2DifficultyEntry prime, Creature2DifficultyEntry damageRank, Creature2DifficultyEntry own)
        {
            if (damageRank?.UnitPropertyMultiplier == null)
                return prime;

            Creature2DifficultyEntry entry = WithHealthMultiplier(prime, prime.UnitPropertyMultiplier[(uint)Property.BaseHealth]);
            foreach (Property rating in new[] { Property.AssaultRating, Property.SupportRating })
            {
                uint index = (uint)rating;
                float ownValue = own?.UnitPropertyMultiplier is { } o && index < o.Length ? o[index] : 0f;
                entry.UnitPropertyMultiplier[index] = Math.Min(prime.UnitPropertyMultiplier[index], Math.Max(damageRank.UnitPropertyMultiplier[index], ownValue));
            }
            return entry;
        }

        /// <summary>
        /// A copy of a <see cref="Creature2DifficultyEntry"/> (same id, so the same rank everywhere) with another health multiplier.
        /// </summary>
        private static Creature2DifficultyEntry WithHealthMultiplier(Creature2DifficultyEntry entry, float multiplier)
        {
            float[] multipliers = (float[])entry.UnitPropertyMultiplier.Clone();
            multipliers[(uint)Property.BaseHealth] = multiplier;
            return new Creature2DifficultyEntry
            {
                Id                                 = entry.Id,
                LocalizedTextIdTitle               = entry.LocalizedTextIdTitle,
                PathScientistScanMultiplier        = entry.PathScientistScanMultiplier,
                ClusterContributionValueDifficulty = entry.ClusterContributionValueDifficulty,
                RankValue                          = entry.RankValue,
                GroupValue                         = entry.GroupValue,
                UnitPropertyMultiplier             = multipliers
            };
        }

        private void EndHoldout(PlayerPathSoldierResult result)
        {
            Holdout ended = holdout;
            holdout = null;

            string alive = string.Join(", ", ended.Spawned
                .Select(guid => player.Map?.GetEntity<IUnitEntity>(guid))
                .Where(unit => unit?.IsAlive == true)
                .Select(unit => $"{unit.CreatureId} guid {unit.Guid} health {unit.Health}/{unit.MaxHealth} at {Vector3.Distance(unit.Position, player.Position):0} m"));
            holdoutLog.Info($"M{ended.Mission.Entry.Id} ended {result} after {ended.Elapsed:0.0} s in wave {ended.WaveIndex + 1}/{ended.Waves}, "
                + $"soldier health {player.Health}/{player.MaxHealth}, alive wave creatures: [{alive}]");

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
