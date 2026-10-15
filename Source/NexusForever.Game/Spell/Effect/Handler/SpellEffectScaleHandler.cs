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
    /// Changes the model scale of the target for the effect duration.
    /// </summary>
    /// <remarks>
    /// DataBits00 is the scale (float). Only server controlled units are scaled, player movement is client controlled.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.Scale)]
    public class SpellEffectScaleHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        /// <summary>
        /// Handle <see cref="ISpell"/> effect apply on <see cref="IUnitEntity"/> target.
        /// </summary>
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            float scale = BitConverter.UInt32BitsToSingle(data.DataBits00);
            if (!float.IsFinite(scale) || scale <= 0f || scale > 20f)
                return SpellEffectExecutionResult.PreventEffect;

            target.MovementManager.SetScale(scale);
            return SpellEffectExecutionResult.Ok;
        }

        /// <summary>
        /// Handle <see cref="ISpell"/> effect remove on <see cref="IUnitEntity"/> target.
        /// </summary>
        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            // effects without a duration are permanent changes, the effect finishing straight away doesn't undo them
            if (info.Entry.DurationTime == 0u)
                return;

            target.MovementManager.SetScale(1f);
        }
    }
}
