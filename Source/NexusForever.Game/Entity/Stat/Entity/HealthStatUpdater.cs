using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Entity.Stat;
using NexusForever.Game.Static.Spell;
using NexusForever.Shared.Game;

namespace NexusForever.Game.Entity.Stat.Entity
{
    public class HealthStatUpdater : IStatUpdater<IUnitEntity>
    {
        private readonly UpdateTimer updateTimer = new(TimeSpan.FromSeconds(0.5f));

        private IUnitEntity entity;

        public void Initialise(IUnitEntity entity)
        {
            this.entity = entity;
        }

        /// <summary>
        /// Invoked each world tick with the delta since the previous tick occurred.
        /// </summary>
        public void Update(double lastTick)
        {
            updateTimer.Update(lastTick);
            if (!updateTimer.HasElapsed)
                return;

            updateTimer.Reset();

            // Holdout attackers can briefly leave combat when their generic AI leashes. Preserve the soldier's damage until
            // the event ends; the holdout's straggler handling will send the attacker back in.
            if (entity.Health < entity.MaxHealth
                && entity.CreatureInfo is not ICreatureInfoOverride { PreserveHealthOnCombatReset: true })
                entity.ModifyHealth((uint)(entity.MaxHealth / 100f), DamageType.Heal, null);
        }

        /// <summary>
        /// Invoked when the combat state changes.
        /// </summary>
        public void OnCombatStateUpdate(bool inCombat)
        {
            updateTimer.Reset(!inCombat);
        }
    }
}
