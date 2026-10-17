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
    /// Gives a player back charges of one of their abilities. DataBits00 is the Spell4Base id, DataBits01 the number of charges.
    /// </summary>
    /// <remarks>
    /// Read from the data (Warrior Smackdown CD Reset gives 1 charge of 21806, Spellslinger Assassinate T8 Recharge 3 of 23274).
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ModifyAbilityCharges)]
    public class SpellEffectModifyAbilityChargesHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            IPlayer player = target as IPlayer ?? executionContext.Spell.Caster as IPlayer;
            ICharacterSpell spell = player?.SpellManager.GetSpell(data.DataBits00);
            if (spell == null)
                return SpellEffectExecutionResult.Ok;

            spell.AddCharges(Math.Max(data.DataBits01, 1u));
            return SpellEffectExecutionResult.Ok;
        }
    }
}
