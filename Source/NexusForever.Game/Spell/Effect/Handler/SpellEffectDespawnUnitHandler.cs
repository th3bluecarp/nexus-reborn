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
    /// Removes a creature from the world, used by quest objects that vanish once used and creatures that destroy themselves.
    /// </summary>
    [SpellEffectHandler(SpellEffectType.DespawnUnit)]
    public class SpellEffectDespawnUnitHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        /// <summary>
        /// Handle <see cref="ISpell"/> effect apply on <see cref="IUnitEntity"/> target.
        /// </summary>
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is IPlayer)
                return SpellEffectExecutionResult.PreventEffect;

            target.Despawn();
            return SpellEffectExecutionResult.Ok;
        }
    }
}
