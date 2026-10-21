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
    /// While the effect lasts, damage that would kill the target casts a spell instead: Last Stand, To the Pain, Unbreakable and
    /// Homeward Bound cheat death, Final Salvo, From the Grave and Cheat Death fire as the target dies.
    /// </summary>
    /// <remarks>
    /// DataBits00 mode (0 and 1 survive, 2 dies), DataBits01 the spell cast, DataBits02 how long death is held off in ms,
    /// DataBits03 a prerequisite that must be met, usually "not under the lockout spell" that makes it "once every N minutes",
    /// DataBits04 a cooldown in ms (To the Pain 300000).
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.DelayDeath)]
    public class SpellEffectDelayDeathHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (!target.IsAlive)
                return SpellEffectExecutionResult.PreventEffect;

            target.AddDeathTrigger(info.EffectId, executionContext.Spell.Caster.Guid, info.Entry.Id, data.DataBits00, data.DataBits01,
                data.DataBits02, data.DataBits03, data.DataBits04);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            target.RemoveDeathTrigger(info.EffectId, spell.Caster.Guid);
        }
    }
}
