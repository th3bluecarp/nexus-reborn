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
    /// Cooldown resets and reductions (Quick Shot alternating, AMP procs, Crisis Wave): DataBits00 the target kind (0 base spell,
    /// 1 spell, 2 spell group with 0 for every spell, 3 cooldown id), DataBits01 the target, DataBits02 the operation and
    /// DataBits03 a float amount: 0 or 1 multiply the remaining time (0 resets), 2 add milliseconds (negative shortens),
    /// 3 set the remaining time in milliseconds (0 resets).
    /// </summary>
    /// <remarks>
    /// Read from the 522 uses in Spell4Effects (see docs/reborn/SEMANTICS.md). Applied once when the effect lands.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ModifySpellCooldown)]
    public class SpellEffectModifySpellCooldownHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is not IPlayer player)
                return SpellEffectExecutionResult.Ok;

            float amount = BitConverter.UInt32BitsToSingle(data.DataBits03);
            if (float.IsNaN(amount))
                return SpellEffectExecutionResult.Ok;

            Func<double, double> change = data.DataBits02 switch
            {
                0u or 1u => remaining => remaining * amount,
                2u       => remaining => remaining + amount / 1000d,
                3u       => _ => amount / 1000d,
                _        => null
            };
            if (change == null)
                return SpellEffectExecutionResult.Ok;

            player.SpellManager.ModifySpellCooldowns(data.DataBits00, data.DataBits01, change);
            return SpellEffectExecutionResult.Ok;
        }
    }
}
