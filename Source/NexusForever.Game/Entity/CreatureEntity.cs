using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Movement;
using NexusForever.Game.Abstract.Entity.Stat;
using NexusForever.Game.Abstract.Loot.Distribute;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// An <see cref="ICreatureEntity"/> is an extension to <see cref="IUnitEntity"/> which contains logic specific to non player controlled combat entities.
    /// </summary>
    public abstract class CreatureEntity : UnitEntity, ICreatureEntity
    {
        #region Dependency Injection

        private readonly ILootDistributor lootDistributor;

        public CreatureEntity(
            IMovementManager movementManager,
            IEntitySummonFactory entitySummonFactory,
            IStatUpdateManager statUpdateManager,
            ISpellFactory spellFactory,
            ILootDistributor lootDistributor)
            : base(movementManager, entitySummonFactory, statUpdateManager, spellFactory)
        {
            this.lootDistributor = lootDistributor;
        }

        #endregion

        protected override void GenerateRewards(IUnitEntity killer)
        {
            base.GenerateRewards(killer);

            lootDistributor.Distribute(this);
        }

        /// <summary>
        /// A creature's range check (proximity aggro, script range events) also needs the unit near its own height. Distances
        /// here are horizontal (MathsExtensions.GetDistance leaves Y out), so a Darkspur Bruiser standing 35 m under a ledge was
        /// "8.6 m" from a player fighting on it, joined that fight and climbed through the air (Darkspur Payback, 2026-11-24).
        /// The limit is the check's own range, at least 15 m (SEMANTICS: creatures notice their own level).
        /// </summary>
        protected override bool IsInRange(IGridEntity target)
        {
            if (!base.IsInRange(target))
                return false;

            float heightLimit = MathF.Max(RangeCheck ?? 0f, 15f);
            return MathF.Abs(Position.Y - target.Position.Y) <= heightLimit;
        }

        /// <summary>
        /// Set target to supplied <see cref="IUnitEntity"/>.
        /// </summary>
        /// <remarks>
        /// A null target will clear the current target.
        /// </remarks>
        public override void SetTarget(IWorldEntity target, uint threat = 0)
        {
            base.SetTarget(target, threat);

            if (target is IPlayer player)
            {
                // plays aggro sound at client, maybe more??
                player.Session.EnqueueMessageEncrypted(new ServerEntityAggroSwitch
                {
                    UnitId   = Guid,
                    TargetId = TargetGuid.Value
                });
            }
        }

        /// <summary>
        /// Invoked when <see cref="ICreatureEntity"/> is targeted by another <see cref="IUnitEntity"/>.
        /// </summary>
        public override void OnTargeted(IUnitEntity source)
        {
            base.OnTargeted(source);

            // client only processes threat list message if the source matches the current target
            if (InCombat && source is IPlayer player)
                ThreatManager.SendThreatList(player.Session);
        }

        /// <summary>
        /// Invoked when a new <see cref="IHostileEntity"/> is added to the threat list.
        /// </summary>
        public override void OnThreatAddTarget(IHostileEntity hostile)
        {
            ThreatManager.BroadcastThreatList();
            base.OnThreatAddTarget(hostile);
        }

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is removed from the threat list.
        /// </summary>
        public override void OnThreatRemoveTarget(IHostileEntity hostile)
        {
            ThreatManager.BroadcastThreatList();
            base.OnThreatRemoveTarget(hostile);
        }

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is update on the threat list.
        /// </summary>
        public override void OnThreatChange(IHostileEntity hostile)
        {
            ThreatManager.BroadcastThreatList();
            base.OnThreatChange(hostile);
        }
    }
}
