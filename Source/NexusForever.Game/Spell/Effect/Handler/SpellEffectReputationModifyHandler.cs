using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Reputation;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Disguises and faction tricks (Hidden in Plain Sight, Hidden in Hay, Friendly To Garr): while the effect lasts the target
    /// player's standing with faction DataBits00 counts as DataBits01 (a float reputation amount, 32000 is Beloved), so the
    /// faction's creatures do not attack.
    /// </summary>
    /// <remarks>
    /// Server side only: the client is not told, so nameplates keep their real colour. Effects without a duration last until relog.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ReputationModify)]
    public class SpellEffectReputationModifyHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is not IPlayer player || data.DataBits00 == 0u)
                return SpellEffectExecutionResult.Ok;

            float amount = BitConverter.UInt32BitsToSingle(data.DataBits01);
            if (float.IsNaN(amount))
                return SpellEffectExecutionResult.Ok;

            player.SetReputationOverride((Faction)data.DataBits00, amount);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is IPlayer player)
                player.SetReputationOverride((Faction)data.DataBits00, null);
        }
    }
}
