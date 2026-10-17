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
        // active timed scales per unit in apply order, the newest one shows
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, List<(uint EffectId, float Scale)>> active = new();

        /// <summary>
        /// Handle <see cref="ISpell"/> effect apply on <see cref="IUnitEntity"/> target.
        /// </summary>
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            float scale = BitConverter.UInt32BitsToSingle(data.DataBits00);
            if (!float.IsFinite(scale) || scale <= 0f || scale > 20f)
                return SpellEffectExecutionResult.PreventEffect;

            if (info.Entry.DurationTime != 0u)
            {
                List<(uint EffectId, float Scale)> list = active.GetOrAdd(target.Guid, _ => []);
                lock (list)
                    list.Add((info.EffectId, scale));
            }

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

            float scale = 1f;
            if (active.TryGetValue(target.Guid, out List<(uint EffectId, float Scale)> list))
            {
                lock (list)
                {
                    // the same effect applied twice is listed twice, each removal takes one
                    int index = list.FindIndex(e => e.EffectId == info.EffectId);
                    if (index >= 0)
                        list.RemoveAt(index);
                    if (list.Count > 0)
                        scale = list[^1].Scale;
                    else
                        active.TryRemove(target.Guid, out _);
                }
            }

            target.MovementManager.SetScale(scale);
        }
    }
}
