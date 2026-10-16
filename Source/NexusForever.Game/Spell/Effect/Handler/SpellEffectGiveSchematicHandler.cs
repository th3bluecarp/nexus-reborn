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
    /// Recipe items: DataBits00 is the TradeskillSchematic2 learned.
    /// </summary>
    /// <remarks>
    /// 1,171 effects; DataBits00 is an existing schematic for 385, the rest name schematics no longer in the table and do nothing.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.GiveSchematic)]
    public class SpellEffectGiveSchematicHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is not IPlayer player)
                return SpellEffectExecutionResult.PreventEffect;

            player.TradeskillManager.LearnSchematic(data.DataBits00);
            return SpellEffectExecutionResult.Ok;
        }
    }
}
