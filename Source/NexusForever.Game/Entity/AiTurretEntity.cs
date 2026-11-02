using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Movement;
using NexusForever.Game.Abstract.Entity.Stat;
using NexusForever.Game.Abstract.Loot.Distribute;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Entity;
using NexusForever.Network.World.Entity;
using NexusForever.Network.World.Entity.Model;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// An AI turret is a creature with health, threat, spells, death and loot like any other NPC; it only differs in the
    /// entity model the client receives. It used to be a bare world object, so the arkship Sim-Turrets (73494) could not be
    /// damaged or killed (2026-11-01, found by the quest bot and confirmed in the real client).
    /// </summary>
    public class AiTurretEntity : NonPlayerEntity, IAiTurretEntity
    {
        public override EntityType Type => EntityType.AiTurret;

        #region Dependency Injection

        public AiTurretEntity(
            IMovementManager movementManager,
            IEntitySummonFactory entitySummonFactory,
            IStatUpdateManager<IUnitEntity> statUpdateManager,
            ISpellFactory spellFactory,
            ILootDistributor lootDistributor)
            : base(movementManager, entitySummonFactory, statUpdateManager, spellFactory, lootDistributor)
        {
        }

        #endregion

        protected override IEntityModel BuildEntityModel()
        {
            return new AiTurretEntityModel
            {
                CreatureId        = CreatureId,
                QuestChecklistIdx = QuestChecklistIdx
            };
        }
    }
}
