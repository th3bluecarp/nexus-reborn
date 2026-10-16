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
    /// Sends the player to their house (Recall - House, the stuck menu house option), creating the residence when needed.
    /// </summary>
    [SpellEffectHandler(SpellEffectType.HousingTeleport)]
    public class SpellEffectHousingTeleportHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        #region Dependency Injection

        private readonly IGlobalResidenceManager globalResidenceManager;
        private readonly IMapLockManager mapLockManager;

        public SpellEffectHousingTeleportHandler(
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

            IResidence residence = globalResidenceManager.GetResidenceByOwner(player.Name)
                ?? globalResidenceManager.CreateResidence(player);

            IResidenceEntrance entrance = globalResidenceManager.GetResidenceEntrance(residence.PropertyInfoId);
            IMapLock mapLock = mapLockManager.GetResidenceLock(residence.Parent ?? residence);

            player.Rotation = entrance.Rotation.ToEuler();
            player.TeleportTo(entrance.Entry, entrance.Position, mapLock);
            return SpellEffectExecutionResult.Ok;
        }
    }
}
