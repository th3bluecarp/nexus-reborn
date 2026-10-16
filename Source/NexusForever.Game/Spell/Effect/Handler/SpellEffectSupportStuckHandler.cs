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
    /// The stuck menu free suicide: the player dies so they can resurrect somewhere reachable.
    /// </summary>
    [SpellEffectHandler(SpellEffectType.SupportStuck)]
    public class SpellEffectSupportStuckHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is not IPlayer || !target.IsAlive)
                return SpellEffectExecutionResult.PreventEffect;

            target.ModifyHealth(target.Health, DamageType.Physical, null);
            return SpellEffectExecutionResult.Ok;
        }
    }
}
