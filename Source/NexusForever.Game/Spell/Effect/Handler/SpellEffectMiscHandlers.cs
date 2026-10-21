using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Puts DataBits01 (at least 1) of item DataBits00 into the target player's bags.
    /// </summary>
    [SpellEffectHandler(SpellEffectType.GiveItemToPlayer)]
    public class SpellEffectGiveItemToPlayerHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is not IPlayer player || data.DataBits00 == 0u)
                return SpellEffectExecutionResult.Ok;

            player.Inventory.ItemCreate(InventoryLocation.Inventory, data.DataBits00, Math.Max(data.DataBits01, 1u));
            return SpellEffectExecutionResult.Ok;
        }
    }

    /// <summary>
    /// Takes the target off its mount or vehicle (Dismount, Security Laser, Squirg Repellent).
    /// </summary>
    [SpellEffectHandler(SpellEffectType.Disembark)]
    public class SpellEffectDisembarkHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is IPlayer player)
            {
                foreach (ISpell spell in player.GetSpellsByEffect(SpellEffectType.SummonMount).ToList())
                    spell.Finish();
                player.Dismount();
            }

            return SpellEffectExecutionResult.Ok;
        }
    }

    /// <summary>
    /// Kills the target outright (Generic Kill Self Immediately, Shredded).
    /// </summary>
    [SpellEffectHandler(SpellEffectType.Kill)]
    public class SpellEffectKillHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target.IsAlive && target.Health > 0u)
                target.ModifyHealth(target.Health, DamageType.Physical, executionContext.Spell.Caster);

            return SpellEffectExecutionResult.Ok;
        }
    }

    /// <summary>
    /// Advances achievement DataBits00 by DataBits01 (at least 1) for the target player, or the caster when the target is not a player.
    /// </summary>
    [SpellEffectHandler(SpellEffectType.AchievementAdvance)]
    public class SpellEffectAchievementAdvanceHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            IPlayer player = target as IPlayer ?? executionContext.Spell.Caster as IPlayer;
            if (player == null || data.DataBits00 == 0u)
                return SpellEffectExecutionResult.Ok;

            player.AchievementManager.AdvanceAchievement(player, (ushort)data.DataBits00, data.DataBits01);
            return SpellEffectExecutionResult.Ok;
        }
    }
}
