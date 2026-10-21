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
    /// AMPs and talents that change other abilities' cast time, cooldown or cost while active (Path Expert, Swift Gadgetry, Efficiency, Impulse).
    /// </summary>
    /// <remarks>
    /// The changes are in Spell4Modification, see <see cref="SpellModification"/>.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ModifySpell)]
    public class SpellEffectModifySpellHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            target.AddSpellModifier(info.EffectId, executionContext.Spell.Caster.Guid, info.Entry);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            target.RemoveSpellModifier(info.EffectId, spell.Caster.Guid);
        }
    }

    /// <summary>
    /// AMPs and talents that change other abilities' effects while active (Fire spell damage, Atomic Rush, Building Pressure).
    /// </summary>
    /// <remarks>
    /// The changes are in Spell4EffectModification, see <see cref="SpellModification"/>. Damage and healing multipliers are applied,
    /// other parameters (duration and the unknown ones) are not yet.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ModifySpellEffect)]
    public class SpellEffectModifySpellEffectHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            target.AddSpellModifier(info.EffectId, executionContext.Spell.Caster.Guid, info.Entry);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            target.RemoveSpellModifier(info.EffectId, spell.Caster.Guid);
        }
    }
}
