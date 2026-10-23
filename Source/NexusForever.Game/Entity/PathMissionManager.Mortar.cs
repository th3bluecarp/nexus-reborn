using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Map.Search;
using NexusForever.Game.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Shared;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Settler improvements that bring a mortar team: while the improvement is built the team stands at the depot and shells
    /// hostile creatures near it.
    /// </summary>
    /// <remarks>
    /// Only the parts are in the client data: the "Exile Mortar Strike Team" improvement (group 10, Camp Icefury), the two
    /// "Exile Mortar Team" creatures, the shell spell (27289, a missile at 40 m/s) and the explosion (27299, enemies within 5 m of
    /// its caster take half their max health and are knocked down, the caster despawns after 15 s). The explosion is cast by the
    /// invisible unit 18742 standing at the impact point, as the retail target painter (27283) summoned it there. How often the
    /// team fired, how far and where it stood were in the lost server scripts: here a shell every 10 s at a hostile creature
    /// within 50 m of the depot, enemies fighting the settler first, while the settler is within 120 m. Kills give the settler credit.
    /// </remarks>
    public partial class PathMissionManager
    {
        /// <param name="CrewOffsets">Crew spots as X/Z offsets from the depot display point, picked by hand to stay clear of
        /// the props around each depot (at Icefury the depot sits 4 m west of the point and a Vitalium Crystal 5 m east).</param>
        private record MortarTeam(uint[] Creatures, uint ShellSpell, uint ExplosionSpell, uint ImpactCreature, Vector2[] CrewOffsets);

        private static readonly Dictionary<uint, MortarTeam> mortarTeams = new()
        {
            [10] = new MortarTeam([18810u, 18813u], 27289u, 27299u, 18742u, [new Vector2(-3f, -7f), new Vector2(3f, -7f)])
        };

        private const double MortarInterval    = 10d;
        private const float MortarTargetRange  = 50f;
        private const float MortarActiveRange  = 120f;
        private const float MortarShellSpeed   = 40f;

        private class ActiveMortarTeam
        {
            public List<uint> Crew { get; } = [];
            public bool Spawning { get; set; }
            public double NextShell { get; set; } = 3d;
            public List<(Vector3 Position, double At)> Incoming { get; } = [];
        }

        private readonly Dictionary<uint, ActiveMortarTeam> activeMortarTeams = [];
        private double mortarClock;

        private void UpdateMortarTeams(double lastTick)
        {
            if (player.Map == null || mortarTeams.Count == 0)
                return;

            mortarClock += lastTick;
            foreach ((uint groupId, MortarTeam team) in mortarTeams)
            {
                PathSettlerImprovementGroupEntry group = GameTableManager.Instance.PathSettlerImprovementGroup.GetEntry(groupId);
                WorldLocation2Entry location = group == null ? null : GameTableManager.Instance.WorldLocation2.GetEntry(group.WorldLocation2IdDisplayPoint);
                if (location == null)
                    continue;

                var depot = new Vector3(location.Position0, location.Position1, location.Position2);
                bool active = GetBuilt(groupId) != null
                    && player.IsAlive
                    && location.WorldId == player.Map.Entry.Id
                    && Vector3.Distance(player.Position, depot) <= MortarActiveRange;

                activeMortarTeams.TryGetValue(groupId, out ActiveMortarTeam state);
                if (!active)
                {
                    DismissMortarTeam(groupId);
                    continue;
                }

                if (state == null)
                {
                    state = new ActiveMortarTeam { NextShell = mortarClock + 3d };
                    activeMortarTeams[groupId] = state;
                    SpawnMortarCrew(team, state, depot);
                }

                for (int i = state.Incoming.Count - 1; i >= 0; i--)
                {
                    if (state.Incoming[i].At > mortarClock)
                        continue;

                    Explode(team, state.Incoming[i].Position);
                    state.Incoming.RemoveAt(i);
                }

                if (mortarClock < state.NextShell)
                    continue;

                state.NextShell = mortarClock + MortarInterval;
                FireShell(team, state, depot);
            }
        }

        private void SpawnMortarCrew(MortarTeam team, ActiveMortarTeam state, Vector3 depot)
        {
            var creatureInfoManager = LegacyServiceProvider.Provider.GetService<ICreatureInfoManager>();
            var entityFactory       = LegacyServiceProvider.Provider.GetService<IEntityFactory>();

            for (int i = 0; i < team.Creatures.Length; i++)
            {
                ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(team.Creatures[i]);
                if (creatureInfo == null)
                    continue;

                Vector2 offset = team.CrewOffsets[i % team.CrewOffsets.Length];
                float x = depot.X + offset.X;
                float z = depot.Z + offset.Y;
                // the camp floor is level with the depot point (props within 10 m sit at -724.2 to -725.0), terrain height could be under it
                var position = new Vector3(x, depot.Y, z);
                // face the depot
                float angle = MathF.Atan2(-offset.X, -offset.Y);

                IWorldEntity entity = entityFactory.CreateWorldEntity(creatureInfo.Entry.CreationTypeEnum);
                entity.Initialise(creatureInfo);
                if (entity.Level == 0u)
                    entity.Level = Math.Max(player.Level, 1u);
                entity.Rotation = new Vector3(angle, 0f, 0f);
                entity.AddToMap(player.Map, position, (map, guid, vector) => state.Crew.Add(guid));
            }
        }

        private void FireShell(MortarTeam team, ActiveMortarTeam state, Vector3 depot)
        {
            var check = new SearchCheckRange<IUnitEntity>();
            check.Initialise(depot, MortarTargetRange);
            List<IUnitEntity> targets = player.Map.Search(depot, MortarTargetRange, check)
                .Where(u => u is not IPlayer && u.CreatureId != 0u && u.IsAlive && u.CanAttack(player) && player.CanAttack(u))
                .ToList();
            if (targets.Count == 0)
                return;

            // enemies already fighting the settler first
            List<IUnitEntity> fighting = targets.Where(u => u.ThreatManager.Any(h => h.HatedUnitId == player.Guid)).ToList();
            List<IUnitEntity> pool = fighting.Count > 0 ? fighting : targets;
            IUnitEntity target = pool[Random.Shared.Next(pool.Count)];

            IUnitEntity gunner = state.Crew
                .Select(guid => player.Map.GetEntity<IUnitEntity>(guid))
                .Where(u => u?.IsAlive == true)
                .OrderBy(_ => Random.Shared.Next())
                .FirstOrDefault();

            float distance = Vector3.Distance(gunner?.Position ?? depot, target.Position);
            gunner?.CastSpell(team.ShellSpell, new SpellParameters
            {
                PrimaryTargetId        = target.Guid,
                UserInitiatedSpellCast = false
            });

            state.Incoming.Add((target.Position, mortarClock + distance / MortarShellSpeed + 0.5d));
        }

        private void Explode(MortarTeam team, Vector3 position)
        {
            var creatureInfoManager = LegacyServiceProvider.Provider.GetService<ICreatureInfoManager>();
            var entityFactory       = LegacyServiceProvider.Provider.GetService<IEntityFactory>();

            ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(team.ImpactCreature);
            if (creatureInfo == null)
                return;

            IWorldEntity entity = entityFactory.CreateWorldEntity(creatureInfo.Entry.CreationTypeEnum);
            entity.Initialise(creatureInfo);
            // the settler gets the kills, as for a pet
            entity.SummonerGuid = player.Guid;
            entity.AddToMap(player.Map, position, (map, guid, vector) =>
            {
                if (map.GetEntity<IUnitEntity>(guid) is not IUnitEntity impact)
                    return;

                impact.CastSpell(team.ExplosionSpell, new SpellParameters { UserInitiatedSpellCast = false });
                impact.DespawnAfter(TimeSpan.FromSeconds(16d));
            });
        }

        private void DismissMortarTeam(uint groupId)
        {
            if (!activeMortarTeams.Remove(groupId, out ActiveMortarTeam state))
                return;

            foreach (uint guid in state.Crew)
                player.Map?.GetEntity<IWorldEntity>(guid)?.RemoveFromMap();
        }

        private void DismissMortarTeams()
        {
            foreach (uint groupId in activeMortarTeams.Keys.ToList())
                DismissMortarTeam(groupId);
        }
    }
}
