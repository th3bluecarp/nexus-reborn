using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Entity.Movement.Spline;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.NorthernWilds
{
    /// <summary>
    /// Freeing a Trapped Survivor (the wreckage, 11070) in Northern Wilds: a Crash Survivor climbs out, runs off and is gone a few seconds later.
    /// </summary>
    /// <remarks>
    /// Stand in for the lost retail script. The Crash Survivor creatures 11069, 11175, 11176 and 11177 are never placed in the
    /// world data, which fits them being spawned by that script. Where they ran to is unknown, here 15 m away from the player.
    /// </remarks>
    [ScriptFilterCreatureId(11070)]
    public class TrappedSurvivorEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private static readonly uint[] CrashSurvivors = [11069u, 11175u, 11176u, 11177u];

        private IWorldEntity owner;

        #region Dependency Injection

        private readonly IEntityFactory entityFactory;
        private readonly ICreatureInfoManager creatureInfoManager;

        public TrappedSurvivorEntityScript(
            IEntityFactory entityFactory,
            ICreatureInfoManager creatureInfoManager)
        {
            this.entityFactory       = entityFactory;
            this.creatureInfoManager = creatureInfoManager;
        }

        #endregion

        public void OnLoad(IWorldEntity owner)
        {
            this.owner = owner;
        }

        public void OnActivateSuccess(IPlayer activator)
        {
            if (owner?.Map == null)
                return;

            ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(CrashSurvivors[Random.Shared.Next(CrashSurvivors.Length)]);
            if (creatureInfo == null)
                return;

            IWorldEntity survivor = entityFactory.CreateWorldEntity(EntityType.NonPlayer);
            survivor.Initialise(creatureInfo);
            survivor.Rotation = owner.Rotation;

            Vector3 start = owner.Position;
            Vector3 away  = start - activator.Position;
            away.Y = 0f;
            away = away.LengthSquared() > 0.01f ? Vector3.Normalize(away) : Vector3.UnitX;
            Vector3 end = start + away * 15f;

            var map = owner.Map;
            survivor.AddToMap(map, start, (_, _, _) =>
            {
                survivor.MovementManager.LaunchSpline([start, end], SplineType.Linear, SplineMode.OneShot, 7f);

                // gone once it has run off
                bool removed = false;
                map.AddTimer(TimeSpan.FromSeconds(4d), () =>
                {
                    if (!removed && survivor.Map != null)
                        survivor.RemoveFromMap();
                    removed = true;
                    return false;
                });
            });
        }
    }
}
