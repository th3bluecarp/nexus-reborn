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
    /// Pauses the casting creature's AI for the effect duration, a recovery window after a big attack.
    /// </summary>
    /// <remarks>
    /// 3,288 effects, 94 percent with no data and a duration of 1 to 4 seconds on creature spells; the meaning is inferred
    /// from the name and those durations. Players are not affected.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.NpcExecutionDelay)]
    public class SpellEffectNpcExecutionDelayHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (executionContext.Spell.Caster is not IUnitEntity caster || caster is IPlayer || info.Entry.DurationTime == 0u)
                return SpellEffectExecutionResult.Ok;

            DateTime until = DateTime.UtcNow.AddMilliseconds(info.Entry.DurationTime);
            if (until > caster.ExecutionDelayUntil)
                caster.ExecutionDelayUntil = until;

            return SpellEffectExecutionResult.Ok;
        }
    }
}
