using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.GameTable;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Spawns a vehicle at the target and, when DataBits02 is set, seats the target player as its pilot.
    /// </summary>
    /// <remarks>
    /// DataBits00 is the Creature2 id and DataBits01 the UnitVehicle id. Ride spells (hoverbike, warpig, battle bot)
    /// have DataBits02 = 1, placed vehicles (turrets, airships, war mechs) have 0 and are boarded by clicking them.
    /// The vehicle goes away when its last passenger leaves or when a timed effect ends.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.SummonVehicle)]
    public class SpellEffectSummonVehicleHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        #region Dependency Injection

        private readonly IEntityFactory entityFactory;
        private readonly ICreatureInfoManager creatureInfoManager;

        public SpellEffectSummonVehicleHandler(
            IEntityFactory entityFactory,
            ICreatureInfoManager creatureInfoManager)
        {
            this.entityFactory       = entityFactory;
            this.creatureInfoManager = creatureInfoManager;
        }

        #endregion

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target?.Map == null)
                return SpellEffectExecutionResult.PreventEffect;

            ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(data.DataBits00);
            if (creatureInfo == null)
                return SpellEffectExecutionResult.PreventEffect;

            if (GameTableManager.Instance.UnitVehicle.GetEntry(data.DataBits01 != 0u ? data.DataBits01 : creatureInfo.Entry.UnitVehicleId) == null)
                return SpellEffectExecutionResult.PreventEffect;

            bool board = data.DataBits02 != 0u && target is IPlayer;
            if (board && !((IPlayer)target).CanMount())
                return SpellEffectExecutionResult.PreventEffect;

            var vehicle = entityFactory.CreateEntity<IVehicleEntity>();
            vehicle.Initialise(creatureInfo, data.DataBits01, executionContext.Spell.Parameters.SpellInfo.Entry.Id);
            if (board)
                vehicle.EnqueuePassengerAdd((IPlayer)target, VehicleSeatType.Pilot, 0);

            vehicle.AddToMap(target.Map, target.Position);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (info.Entry.DurationTime == 0u)
                return;

            if (target is IPlayer player)
                player.Dismount();
        }
    }
}
