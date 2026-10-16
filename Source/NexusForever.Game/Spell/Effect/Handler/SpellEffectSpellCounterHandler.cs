using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Interrupts the target's cast (school interrupts, Head Shot, Suffocate, Exhaust).
    /// </summary>
    /// <remarks>
    /// Uses interrupt armour like crowd control: one point of armour is removed first, the cast is only stopped when none is left.
    /// DataBits01 is a lockout in milliseconds and DataBits03 a school mask, school lockouts are not applied yet.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.SpellCounter)]
    public class SpellEffectSpellCounterHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (!target.HasSpell(s => s.IsCasting, out ISpell spell))
                return SpellEffectExecutionResult.Ok;

            if (target.MaxInterruptArmour == -1)
                return SpellEffectExecutionResult.PreventEffect;

            uint remainder = 1u;
            target.CrowdControlManager.RemoveInterruptArmour(ref remainder);
            if (remainder == 0u)
                return SpellEffectExecutionResult.Ok;

            spell.CancelCast(CastResult.SpellInterrupted);
            return SpellEffectExecutionResult.Ok;
        }
    }
}
