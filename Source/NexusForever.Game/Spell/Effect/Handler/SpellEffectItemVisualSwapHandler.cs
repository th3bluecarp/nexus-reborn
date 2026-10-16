using System.Collections.Concurrent;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Shows a different item model in a slot for the effect duration (carried quest objects, weapon and armour visuals).
    /// </summary>
    /// <remarks>
    /// DataBits00 is the <see cref="ItemSlot"/> and DataBits01 the item display id. The previous visual comes back when a timed effect ends.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ItemVisualSwap)]
    public class SpellEffectItemVisualSwapHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        private class Swap
        {
            public ItemVisual Original { get; init; }
            public int Count { get; set; }
        }

        // the visual from before the first active swap of a slot, so overlapping swaps restore the real item at the end
        private static readonly ConcurrentDictionary<(uint Guid, ItemSlot Slot), Swap> swaps = new();

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (data.DataBits01 == 0u || data.DataBits01 > ushort.MaxValue)
                return SpellEffectExecutionResult.PreventEffect;

            var slot = (ItemSlot)data.DataBits00;
            if (info.Entry.DurationTime != 0u)
            {
                Swap swap = swaps.GetOrAdd((target.Guid, slot), _ =>
                {
                    IItemVisual current = target.GetVisuals().FirstOrDefault(v => v.Slot == slot);
                    return new Swap
                    {
                        Original = current == null ? null : new ItemVisual
                        {
                            Slot        = current.Slot,
                            DisplayId   = current.DisplayId,
                            ColourSetId = current.ColourSetId,
                            DyeData     = current.DyeData
                        }
                    };
                });
                swap.Count++;
            }

            target.AddVisual(slot, (ushort)data.DataBits01);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            // effects without a duration are permanent changes, the effect finishing straight away doesn't undo them
            if (info.Entry.DurationTime == 0u)
                return;

            var key = (target.Guid, (ItemSlot)data.DataBits00);
            if (!swaps.TryGetValue(key, out Swap swap) || --swap.Count > 0)
                return;

            swaps.TryRemove(key, out _);
            if (swap.Original?.DisplayId != null)
                target.AddVisual(swap.Original);
            else
                target.RemoveVisual((ItemSlot)data.DataBits00);
        }
    }
}
