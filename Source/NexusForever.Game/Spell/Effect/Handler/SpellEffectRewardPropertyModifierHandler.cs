using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Reward;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.GameTable;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Boost buffs and consumables (Combat XP increased, Protostar Profit, Champion Buff): DataBits00 is a RewardPropertyType,
    /// DataBits01 its data value (a currency for currency boosts), DataBits02 a float added to the property while the effect lasts.
    /// </summary>
    /// <remarks>
    /// The property is updated for the client's display. XP boosts also raise <see cref="IPlayer.XpBoost"/>, which XpManager pays out;
    /// the other property types are shown but not yet applied to their rewards.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.RewardPropertyModifier)]
    public class SpellEffectRewardPropertyModifierHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is IPlayer player)
                Modify(player, data, 1f);
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is IPlayer player)
                Modify(player, data, -1f);
        }

        private static void Modify(IPlayer player, ISpellEffectDefaultData data, float sign)
        {
            float value = BitConverter.UInt32BitsToSingle(data.DataBits02);
            if (value == 0f || float.IsNaN(value))
                return;

            var type = (RewardPropertyType)data.DataBits00;
            if (GameTableManager.Instance.RewardProperty.GetEntry(data.DataBits00) != null)
                player.Account.RewardPropertyManager.UpdateRewardProperty(type, sign * value, data.DataBits01);

            if (type == RewardPropertyType.XP)
                player.XpBoost = Math.Max(0f, player.XpBoost + sign * value);
        }
    }
}
