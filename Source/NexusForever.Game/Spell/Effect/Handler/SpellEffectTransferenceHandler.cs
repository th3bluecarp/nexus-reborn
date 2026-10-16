using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.Shared;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Drains a vital from the target and gives part of it to the caster (Stalker Bloodthirst and Nano Dart, Warrior Sapping Strikes).
    /// </summary>
    /// <remarks>
    /// DataBits00 is the <see cref="Vital"/> drained, DataBits01 the vital given, DataBits02 and DataBits03 the damage multiplier
    /// and base (read by the damage calculator), DataBits05 the share of the damage given back (float). Only health is handled.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.Transference)]
    public class SpellEffectTransferenceHandler : ISpellEffectApplyHandler<ISpellEffectDamageData>
    {
        #region Dependency Injection

        private readonly IFactory<IDamageCalculator> damageCalculatorFactory;

        public SpellEffectTransferenceHandler(
            IFactory<IDamageCalculator> damageCalculatorFactory)
        {
            this.damageCalculatorFactory = damageCalculatorFactory;
        }

        #endregion

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDamageData data)
        {
            IUnitEntity caster = executionContext.Spell.Caster;
            if ((Vital)info.Entry.DataBits00 != Vital.Health)
                return SpellEffectExecutionResult.PreventEffect;

            if (!target.CanAttack(caster) || target.IsInvulnerable)
                return SpellEffectExecutionResult.PreventEffect;

            IDamageCalculator damageCalculator = damageCalculatorFactory.Resolve();
            damageCalculator.CalculateDamage(executionContext, target, info);
            if (info.Damage == null)
                return SpellEffectExecutionResult.Ok;

            target.TakeDamage(caster, info.Damage);

            if ((Vital)info.Entry.DataBits01 == Vital.Health && caster.IsAlive)
            {
                float ratio = BitConverter.UInt32BitsToSingle(info.Entry.DataBits05);
                if (!float.IsFinite(ratio) || ratio <= 0f)
                    ratio = 1f;

                uint heal = Math.Min((uint)(info.Damage.AdjustedDamage * ratio), caster.MaxHealth - caster.Health);
                if (heal > 0u)
                    caster.ModifyHealth(heal, DamageType.Heal, caster);
            }

            return SpellEffectExecutionResult.Ok;
        }
    }
}
