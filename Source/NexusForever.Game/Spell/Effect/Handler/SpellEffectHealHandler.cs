using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.Network.World.Combat;
using NexusForever.Shared;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Restores health on a friendly living target.
    /// </summary>
    [SpellEffectHandler(SpellEffectType.Heal)]
    public class SpellEffectHealHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        #region Dependency Injection

        private readonly IFactory<IDamageCalculator> damageCalculatorFactory;

        public SpellEffectHealHandler(
            IFactory<IDamageCalculator> damageCalculatorFactory)
        {
            this.damageCalculatorFactory = damageCalculatorFactory;
        }

        #endregion

        /// <summary>
        /// Handle <see cref="ISpell"/> effect apply on <see cref="IUnitEntity"/> target.
        /// </summary>
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (!target.IsAlive)
                return SpellEffectExecutionResult.PreventEffect;

            // never heal something the caster could attack
            IUnitEntity caster = executionContext.Spell.Caster;
            if (caster != target && caster.CanAttack(target))
                return SpellEffectExecutionResult.PreventEffect;

            IDamageCalculator damageCalculator = damageCalculatorFactory.Resolve();
            uint amount = damageCalculator.CalculateHeal(executionContext, target, info);

            uint missing = target.MaxHealth - target.Health;
            uint healed  = Math.Min(amount, missing);
            if (healed > 0)
                target.ModifyHealth(healed, DamageType.Heal, caster);

            executionContext.AddCombatLog(new CombatLogHeal
            {
                HealAmount = healed,
                Overheal   = amount - healed,
                EffectType = info.Entry.EffectType,
                CastData   = new CombatLogCastData
                {
                    CasterId     = caster.Guid,
                    TargetId     = target.Guid,
                    SpellId      = executionContext.Spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = info.Damage?.CombatResult ?? CombatResult.Hit
                }
            });

            return SpellEffectExecutionResult.Ok;
        }
    }
}
