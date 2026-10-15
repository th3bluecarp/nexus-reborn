using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Target;

namespace NexusForever.Game.Abstract.Combat
{
    public interface IDamageCalculator
    {
        /// <summary>
        /// Returns the calculated damage and updates the referenced <see cref="ISpellTargetEffectInfo"/> appropriately.
        /// </summary>
        /// <remarks>
        /// TODO: This should probably return an instance of a Class which describes all the damage done to both entities. Attackers can have reflected damage from this, etc.
        /// </remarks>
        void CalculateDamage(ISpellExecutionContext executionContext, IUnitEntity victim, ISpellTargetEffectInfo info);

        /// <summary>
        /// Returns the calculated heal amount for a Heal or HealShields effect and updates the referenced <see cref="ISpellTargetEffectInfo"/> appropriately.
        /// </summary>
        uint CalculateHeal(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info);
    }
}
