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
    /// Removes buffs or debuffs of a dispellable spell class from the target.
    /// </summary>
    /// <remarks>
    /// DataBits00 is the most spells removed (0 or 0xFFFFFFFF for all), DataBits03 the <see cref="SpellClass"/> removed
    /// (BuffDispellable or DebuffDispellable). Auras live on their caster, so the target and every unit it can see are searched.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.SpellDispel)]
    public class SpellEffectSpellDispelHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        /// <summary>
        /// Handle <see cref="ISpell"/> effect apply on <see cref="IUnitEntity"/> target.
        /// </summary>
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            var spellClass = (SpellClass)data.DataBits03;
            if (spellClass is not (SpellClass.BuffDispellable or SpellClass.DebuffDispellable))
                return SpellEffectExecutionResult.PreventEffect;

            uint max = data.DataBits00 is 0u or uint.MaxValue ? uint.MaxValue : data.DataBits00;

            var sources = new List<IUnitEntity> { target };
            sources.AddRange(target.GetVisible().OfType<IUnitEntity>().Where(u => u != target));

            var toRemove = sources
                .SelectMany(u => u.GetSpellsAffecting(target))
                .Where(s => s != executionContext.Spell && s.Parameters.SpellInfo.BaseInfo.Entry.SpellClass == spellClass)
                // finishing a spell ends it on every target, so area auras hitting others are left alone
                .Where(s => sources.Count(u => u != target && s.GetTarget(u) != null) == 0)
                .Take((int)Math.Min(max, int.MaxValue))
                .ToList();

            foreach (ISpell spell in toRemove)
                spell.Finish();

            return SpellEffectExecutionResult.Ok;
        }
    }
}
