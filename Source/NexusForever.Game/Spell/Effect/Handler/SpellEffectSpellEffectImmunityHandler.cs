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
    /// Makes the target immune to one effect type from other units while it lasts: Freedom and Self CC Immunity (CCStateSet),
    /// boss immunities to knockbacks (ForcedMove), interrupts (SpellCounter) and damage.
    /// </summary>
    /// <remarks>
    /// DataBits00 is the <see cref="SpellEffectType"/>. DataBits01 (a spell id on one test spell) and DataBits03/04 are not used.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.SpellEffectImmunity)]
    public class SpellEffectSpellEffectImmunityHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (data.DataBits00 == 0u)
                return SpellEffectExecutionResult.Ok;

            target.AddEffectImmunity(info.EffectId, executionContext.Spell.Caster.Guid, (SpellEffectType)data.DataBits00);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            target.RemoveEffectImmunity(info.EffectId, spell.Caster.Guid);
        }
    }
}
