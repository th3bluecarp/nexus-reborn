using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Housing;
using NexusForever.Game.Abstract.Map.Lock;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Shared;

namespace NexusForever.Game.Spell.Effect.Handler
{
    [SpellEffectHandler(SpellEffectType.Teleport)]
    public class SpellEffectTeleportHandler : ISpellEffectApplyHandler<ISpellEffectTeleportData>
    {
        #region Dependency Injection

        private readonly IGameTableManager gameTableManager;
        private readonly IGlobalResidenceManager globalResidenceManager;
        private readonly IMapLockManager mapLockManager;

        public SpellEffectTeleportHandler(
            IGameTableManager gameTableManager,
            IGlobalResidenceManager globalResidenceManager,
            IMapLockManager mapLockManager)
        {
            this.gameTableManager       = gameTableManager;
            this.globalResidenceManager = globalResidenceManager;
            this.mapLockManager         = mapLockManager;
        }

        #endregion

        /// <summary>
        /// Handle <see cref="ISpell"/> effect apply on <see cref="IUnitEntity"/> target.
        /// </summary>
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectTeleportData data)
        {
            if (target is not IPlayer player)
                return SpellEffectExecutionResult.Ok;

            // a teleport without a location is Recall - Transmat, it goes to the player's bind point
            if (data.WorldLocationId == 0u)
            {
                if (player.TryGetBindPoint(out ushort bindWorldId, out Vector3 bindPosition) && player.CanTeleport())
                    player.TeleportTo(bindWorldId, bindPosition.X, bindPosition.Y, bindPosition.Z);
                return SpellEffectExecutionResult.Ok;
            }

            WorldLocation2Entry locationEntry = gameTableManager.WorldLocation2.GetEntry(data.WorldLocationId);
            if (locationEntry == null)
                return SpellEffectExecutionResult.Ok;

            // Handle Housing Teleport
            if (locationEntry.WorldId == 1229)
            {
                IResidence residence = globalResidenceManager.GetResidenceByOwner(player.Name);
                if (residence == null)
                    residence = globalResidenceManager.CreateResidence(player);

                IResidenceEntrance entrance = globalResidenceManager.GetResidenceEntrance(residence.PropertyInfoId);
                if (player.CanTeleport())
                {
                    IMapLock mapLock = mapLockManager.GetResidenceLock(residence.Parent ?? residence);

                    player.Rotation = entrance.Rotation.ToEuler();
                    player.TeleportTo(entrance.Entry, entrance.Position, mapLock);
                    return SpellEffectExecutionResult.Ok;
                }
            }

            if (player.CanTeleport())
            {
                player.Rotation = new Quaternion(locationEntry.Facing0, locationEntry.Facing1, locationEntry.Facing2, locationEntry.Facing3).ToEuler();
                player.TeleportTo((ushort)locationEntry.WorldId, locationEntry.Position0, locationEntry.Position1, locationEntry.Position2);
            }

            return SpellEffectExecutionResult.Ok;
        }
    }
}
