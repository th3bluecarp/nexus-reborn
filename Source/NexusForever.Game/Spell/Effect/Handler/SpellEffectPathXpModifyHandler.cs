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
    /// Gives the player path xp, DataBits00 times 10.
    /// </summary>
    /// <remarks>
    /// 5 effects with DataBits00 1, 10 or 30, on Explorer objects such as the reveal spell 83956. The scale is invented.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.PathXpModify)]
    public class SpellEffectPathXpModifyHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            IPlayer player = target as IPlayer ?? executionContext.Spell.Caster as IPlayer;
            if (player == null || data.DataBits00 == 0u)
                return SpellEffectExecutionResult.PreventEffect;

            player.PathManager.AddXp(data.DataBits00 * 10u);
            return SpellEffectExecutionResult.Ok;
        }
    }
}
