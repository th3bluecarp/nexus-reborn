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
    /// Scales damage or healing between the target and the caster, for example Analyze Weakness (target takes 20% more from the Stalker)
    /// or Nerve Induction (target deals 20% less to the Medic).
    /// </summary>
    /// <remarks>
    /// DataBits00 is the category: 3 to 5 physical, tech and magic damage the target deals, 6 to 8 the same damage types the target takes,
    /// 12 healing and 13 shield healing the target receives. DataBits01 1 limits it to damage or healing with the caster, 2 applies it
    /// against everyone. DataBits02 is the multiplier (float). Category 9, a flat reduction, is only used by a test spell and is ignored.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.PersonalDmgHealMod)]
    public class SpellEffectPersonalDmgHealModHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (data.DataBits00 is not ((>= 3u and <= 8u) or 12u or 13u))
                return SpellEffectExecutionResult.Ok;

            float multiplier = BitConverter.UInt32BitsToSingle(data.DataBits02);
            if (float.IsNaN(multiplier) || multiplier < 0f)
                return SpellEffectExecutionResult.Ok;

            IUnitEntity caster = executionContext.Spell.Caster;
            target.AddPersonalModifier(info.EffectId, caster.Guid, data.DataBits00, data.DataBits01 == 2u ? 0u : caster.Guid, multiplier);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            target.RemovePersonalModifier(info.EffectId, spell.Caster.Guid);
        }
    }
}
