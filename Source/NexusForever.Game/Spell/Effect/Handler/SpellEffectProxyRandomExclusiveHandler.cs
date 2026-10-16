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
    /// Casts one spell picked at random by weight on the target.
    /// </summary>
    /// <remarks>
    /// Up to five (Spell4 id, weight) pairs in DataBits00/01, 02/03, 04/05, 06/07 and 08/09, a zero spell ends the list.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ProxyRandomExclusive)]
    public class SpellEffectProxyRandomExclusiveHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            uint[] bits = [data.DataBits00, data.DataBits01, data.DataBits02, data.DataBits03, data.DataBits04, data.DataBits05, data.DataBits06, data.DataBits07, data.DataBits08, data.DataBits09];

            var choices = new List<(uint Spell4Id, uint Weight)>();
            for (int i = 0; i < bits.Length; i += 2)
            {
                if (bits[i] == 0u)
                    break;
                choices.Add((bits[i], Math.Max(bits[i + 1], 1u)));
            }

            if (choices.Count == 0)
                return SpellEffectExecutionResult.PreventEffect;

            long roll = Random.Shared.NextInt64(choices.Sum(c => (long)c.Weight));
            uint spell4Id = choices[^1].Spell4Id;
            foreach ((uint id, uint weight) in choices)
            {
                if (roll < weight)
                {
                    spell4Id = id;
                    break;
                }
                roll -= weight;
            }

            executionContext.Spell.Caster.CastSpell(spell4Id, new SpellParameters
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
