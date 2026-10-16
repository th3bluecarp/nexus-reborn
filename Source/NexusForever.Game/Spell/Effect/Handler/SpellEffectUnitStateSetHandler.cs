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
    /// Sets a unit state for the effect duration, DataBits00 is the state.
    /// </summary>
    /// <remarks>
    /// Only state 6 is handled: invulnerability (Invulnerability, Energy Shield, Primal Energy Shield). The other states
    /// (1 used by Block, 7 to 9 and 16 to 18 by immunities, 22 by barriers) are not understood well enough to apply.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.UnitStateSet)]
    public class SpellEffectUnitStateSetHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        private const uint InvulnerableState = 6u;

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (data.DataBits00 != InvulnerableState)
                return SpellEffectExecutionResult.PreventEffect;

            target.SetInvulnerable(executionContext.Spell.CastingId, true);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (data.DataBits00 != InvulnerableState || info.Entry.DurationTime == 0u)
                return;

            target.SetInvulnerable(spell.CastingId, false);
        }
    }
}
