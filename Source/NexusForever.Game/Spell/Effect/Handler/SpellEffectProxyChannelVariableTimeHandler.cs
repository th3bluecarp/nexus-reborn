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
    /// Fires a sequence of spells while the parent spell is channelled (Subjugating Blades, Wave Lash, creature combos like Hack and Slash).
    /// </summary>
    /// <remarks>
    /// Up to five (Spell4 id, delay in milliseconds before the next one) pairs in DataBits00 to 09. The sequence stops when the
    /// effect duration has passed or the caster or target dies.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ProxyChannelVariableTime)]
    public class SpellEffectProxyChannelVariableTimeHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            IUnitEntity caster = executionContext.Spell.Caster;
            if (caster?.Map == null)
                return SpellEffectExecutionResult.PreventEffect;

            uint[] bits = [data.DataBits00, data.DataBits01, data.DataBits02, data.DataBits03, data.DataBits04, data.DataBits05, data.DataBits06, data.DataBits07, data.DataBits08, data.DataBits09];
            var steps = new List<(uint Spell4Id, uint DelayMs)>();
            for (int i = 0; i < bits.Length; i += 2)
            {
                if (bits[i] == 0u)
                    break;
                steps.Add((bits[i], bits[i + 1]));
            }

            if (steps.Count == 0)
                return SpellEffectExecutionResult.PreventEffect;

            ISpell parent = executionContext.Spell;
            ISpellParameters parentParameters = parent.Parameters;

            void Cast(uint spell4Id)
            {
                caster.CastSpell(spell4Id, new SpellParameters
                {
                    ParentSpellInfo        = parentParameters.SpellInfo,
                    RootSpellInfo          = parentParameters.RootSpellInfo,
                    PrimaryTargetId        = target.Guid,
                    UserInitiatedSpellCast = parentParameters.UserInitiatedSpellCast,
                    IsProxy                = true
                });
            }

            Cast(steps[0].Spell4Id);

            // instant parents finish straight away, so the effect duration bounds the sequence instead
            DateTime end = DateTime.UtcNow.AddMilliseconds(Math.Max(info.Entry.DurationTime, steps.Sum(s => (double)s.DelayMs)) + 250d);
            int next = 1;
            if (next < steps.Count)
            {
                caster.Map.AddTimer(TimeSpan.FromMilliseconds(Math.Max(steps[0].DelayMs, 50u)), () =>
                {
                    if (DateTime.UtcNow > end || !caster.IsAlive || !target.IsAlive || caster.Map == null)
                        return false;

                    Cast(steps[next].Spell4Id);
                    next++;
                    return next < steps.Count;
                });
            }

            return SpellEffectExecutionResult.Ok;
        }
    }
}
