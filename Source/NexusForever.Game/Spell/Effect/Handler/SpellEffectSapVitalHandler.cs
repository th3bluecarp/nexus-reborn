using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Changes a vital by a percentage (Level-Up full heal, food, Creeping Death, self-destructs, Destruction).
    /// </summary>
    /// <remarks>
    /// DataBits00 is the <see cref="Vital"/>, DataBits02 the percentage (float) and DataBits03 the mode:
    /// 1 adds the percentage of the maximum (negative drains), 0 removes the percentage of the maximum,
    /// 2 removes the percentage of the current value. Only health is handled.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.SapVital)]
    public class SpellEffectSapVitalHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if ((Vital)data.DataBits00 != Vital.Health || !target.IsAlive)
                return SpellEffectExecutionResult.PreventEffect;

            float percent = BitConverter.UInt32BitsToSingle(data.DataBits02);
            if (!float.IsFinite(percent) || percent == 0f)
                return SpellEffectExecutionResult.PreventEffect;

            double change = data.DataBits03 switch
            {
                1u => target.MaxHealth * percent / 100d,
                0u => -target.MaxHealth * percent / 100d,
                2u => -target.Health * percent / 100d,
                _  => 0d
            };

            IUnitEntity caster = executionContext.Spell.Caster;
            if (change > 0d)
            {
                uint heal = Math.Min((uint)change, target.MaxHealth - target.Health);
                if (heal > 0u)
                    target.ModifyHealth(heal, DamageType.Heal, caster);
            }
            else if (change < 0d)
            {
                if (target.IsInvulnerable)
                    return SpellEffectExecutionResult.PreventEffect;

                target.ModifyHealth(Math.Min((uint)-change, target.Health), DamageType.Physical, caster);
            }

            return SpellEffectExecutionResult.Ok;
        }
    }
}
