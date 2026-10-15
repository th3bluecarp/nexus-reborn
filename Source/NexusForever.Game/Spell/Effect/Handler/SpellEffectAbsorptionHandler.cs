using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
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
    /// Gives the target an absorb shield that takes damage before shield and health until it is used up or the effect ends.
    /// </summary>
    /// <remarks>
    /// DataBits00 is the multiplier, DataBits01 the base amount (float) and DataBits04 the damage type mask (7 = physical, tech and magic).
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.Absorption)]
    public class SpellEffectAbsorptionHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        #region Dependency Injection

        private readonly IFactory<IDamageCalculator> damageCalculatorFactory;

        public SpellEffectAbsorptionHandler(
            IFactory<IDamageCalculator> damageCalculatorFactory)
        {
            this.damageCalculatorFactory = damageCalculatorFactory;
        }

        #endregion

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (!target.IsAlive)
                return SpellEffectExecutionResult.PreventEffect;

            IUnitEntity caster = executionContext.Spell.Caster;
            if (caster != target && caster.CanAttack(target))
                return SpellEffectExecutionResult.PreventEffect;

            uint amount = damageCalculatorFactory.Resolve().CalculateEffectAmount(caster, target, info.Entry);
            target.AddAbsorb(info.EffectId, amount, data.DataBits04);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            target.RemoveAbsorb(info.EffectId);
        }
    }
}
