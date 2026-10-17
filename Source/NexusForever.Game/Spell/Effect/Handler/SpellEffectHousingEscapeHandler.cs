using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Housing;
using NexusForever.Game.Abstract.Map.Lock;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.Shared;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Housing Escape (41323): puts a player stuck on their plot back in front of their house, the residence entrance.
    /// </summary>
    [SpellEffectHandler(SpellEffectType.HousingEscape)]
    public class SpellEffectHousingEscapeHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        #region Dependency Injection

        private readonly IGlobalResidenceManager globalResidenceManager;
        private readonly IMapLockManager mapLockManager;

        public SpellEffectHousingEscapeHandler(
            IGlobalResidenceManager globalResidenceManager,
            IMapLockManager mapLockManager)
        {
            this.globalResidenceManager = globalResidenceManager;
            this.mapLockManager         = mapLockManager;
        }

        #endregion

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is not IPlayer player || !player.CanTeleport())
                return SpellEffectExecutionResult.PreventEffect;

            IResidence residence = globalResidenceManager.GetResidenceByOwner(player.Name);
            if (residence == null)
                return SpellEffectExecutionResult.PreventEffect;

            IResidenceEntrance entrance = globalResidenceManager.GetResidenceEntrance(residence.PropertyInfoId);
            if (entrance == null)
                return SpellEffectExecutionResult.PreventEffect;

            IMapLock mapLock = mapLockManager.GetResidenceLock(residence.Parent ?? residence);

            player.Rotation = entrance.Rotation.ToEuler();
            player.TeleportTo(entrance.Entry, entrance.Position, mapLock);
            return SpellEffectExecutionResult.Ok;
        }
    }
}
