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
    /// Casts a sub spell for a linear area attack (Subjugating Blades, spore bombs and similar).
    /// </summary>
    /// <remarks>
    /// DataBits00 is the Spell4 id, DataBits01 an angle and DataBits02 a distance (floats) where retail placed the sub spell.
    /// Positioned casts are not supported, the sub spell is cast at the effect target instead.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ProxyLinearAE)]
    public class SpellEffectProxyLinearAEHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (data.DataBits00 == 0u)
                return SpellEffectExecutionResult.PreventEffect;

            executionContext.Spell.Caster.CastSpell(data.DataBits00, new SpellParameters
            {
                ParentSpellInfo        = executionContext.Spell.Parameters.SpellInfo,
                RootSpellInfo          = executionContext.Spell.Parameters.RootSpellInfo,
                PrimaryTargetId        = target.Guid,
                UserInitiatedSpellCast = executionContext.Spell.Parameters.UserInitiatedSpellCast,
                IsProxy                = true
            });

            return SpellEffectExecutionResult.Ok;
        }
    }
}
