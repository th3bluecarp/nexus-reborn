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
    /// Vendor discounts: DataBits00 is a float multiplier on purchase prices, DataBits01 one on sell prices
    /// (Small Vendor Price Discount 0.95 and 1.05, Large 0.85 and 1.15).
    /// </summary>
    [SpellEffectHandler(SpellEffectType.VendorPriceModifier)]
    public class SpellEffectVendorPriceModifierHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is not IPlayer player)
                return SpellEffectExecutionResult.Ok;

            (float buy, float sell) = GetMultipliers(data);
            player.VendorBuyMultiplier  *= buy;
            player.VendorSellMultiplier *= sell;
            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is not IPlayer player)
                return;

            (float buy, float sell) = GetMultipliers(data);
            player.VendorBuyMultiplier  /= buy;
            player.VendorSellMultiplier /= sell;
        }

        private static (float Buy, float Sell) GetMultipliers(ISpellEffectDefaultData data)
        {
            float buy  = BitConverter.UInt32BitsToSingle(data.DataBits00);
            float sell = BitConverter.UInt32BitsToSingle(data.DataBits01);
            return (buy > 0f ? buy : 1f, sell > 0f ? sell : 1f);
        }
    }
}
