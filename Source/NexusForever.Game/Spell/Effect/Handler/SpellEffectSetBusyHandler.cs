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
    /// Marks an activatable object busy (DataBits00 1) or free again (DataBits00 0), see docs/reborn/SEMANTICS.md.
    /// </summary>
    /// <remarks>
    /// 352 of the 508 spells with this effect are creature activate spells (Free Hostage, Clearing Weeds, Plant Explosive
    /// Charge) targeting the object. 62 set busy and clear it again after a delay, an object cooldown. The 275 that set busy
    /// without a clear or a DespawnUnit relied on server scripts (they usually carry a RavelSignal), so busy also clears
    /// itself after <see cref="FallbackClear"/> to keep those objects usable.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.SetBusy)]
    public class SpellEffectSetBusyHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        private static readonly TimeSpan FallbackClear = TimeSpan.FromSeconds(60d);

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is IPlayer)
                return SpellEffectExecutionResult.PreventEffect;

            bool busy = data.DataBits00 != 0u;
            target.SetBusy(busy);

            if (busy && target.Map != null)
            {
                uint token = target.BusyToken;
                target.Map.AddTimer(FallbackClear, () =>
                {
                    // only clear the busy state this effect set, a later set or clear owns it otherwise
                    if (target.IsBusy && target.BusyToken == token)
                        target.SetBusy(false);
                    return false;
                });
            }

            return SpellEffectExecutionResult.Ok;
        }
    }
}
