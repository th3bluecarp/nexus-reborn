using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Spell.Proc;
using NexusForever.Game.Static.Spell;
using NexusForever.Shared;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// DistributedDamage (Stalker Shred and Eviscerate, Esper Telekinetic Field and others) deals damage like the Damage effect.
    /// </summary>
    /// <remarks>
    /// The damage calculator already reads its data. Retail split the total across targets in some way that is not known,
    /// each target takes the full amount here.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.DistributedDamage)]
    public class SpellEffectDistributedDamageHandler : SpellEffectDamageHandler
    {
        public SpellEffectDistributedDamageHandler(
            IFactory<IDamageCalculator> damageCalculatorFactory,
            IFactory<IProcParameters> procParameterFactory)
            : base(damageCalculatorFactory, procParameterFactory)
        {
        }
    }

    /// <summary>
    /// DistanceDependentDamage deals damage like the Damage effect, the distance falloff is not applied yet.
    /// </summary>
    [SpellEffectHandler(SpellEffectType.DistanceDependentDamage)]
    public class SpellEffectDistanceDependentDamageHandler : SpellEffectDamageHandler
    {
        public SpellEffectDistanceDependentDamageHandler(
            IFactory<IDamageCalculator> damageCalculatorFactory,
            IFactory<IProcParameters> procParameterFactory)
            : base(damageCalculatorFactory, procParameterFactory)
        {
        }
    }
}
