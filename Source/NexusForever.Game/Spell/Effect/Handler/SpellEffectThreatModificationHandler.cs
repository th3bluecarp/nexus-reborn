using NexusForever.Game.Abstract.Combat;
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
    /// Changes the caster's place on the target's threat list. DataBits00 is the mode.
    /// </summary>
    /// <remarks>
    /// 1 lowers the caster's threat by DataBits01 percent (float, Forget and Detaunt), 2 drops the caster from the list
    /// (Drop Threat), 5 adds DataBits02 threat (taunts such as Atomic Attraction), 6 wipes the whole list (Threat Wipe).
    /// Modes 0 and 4 are not understood and are ignored.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ThreatModification)]
    public class SpellEffectThreatModificationHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            IUnitEntity caster = executionContext.Spell.Caster;
            if (caster == null || target == caster || target is IPlayer)
                return SpellEffectExecutionResult.PreventEffect;

            switch (data.DataBits00)
            {
                case 1u:
                {
                    IHostileEntity hostile = target.ThreatManager.GetHostile(caster.Guid);
                    if (hostile == null)
                        break;

                    float percent = Math.Clamp(BitConverter.UInt32BitsToSingle(data.DataBits01), 0f, 100f);
                    if (percent >= 100f)
                        target.ThreatManager.RemoveHostile(caster.Guid);
                    else
                        target.ThreatManager.UpdateThreat(caster, -(int)(hostile.Threat * percent / 100f));
                    break;
                }
                case 2u:
                    target.ThreatManager.RemoveHostile(caster.Guid);
                    break;
                case 5u:
                    if (target.CanAttack(caster))
                        target.ThreatManager.UpdateThreat(caster, (int)Math.Min(data.DataBits02, int.MaxValue / 2u));
                    break;
                case 6u:
                    target.ThreatManager.ClearThreatList();
                    break;
                default:
                    return SpellEffectExecutionResult.PreventEffect;
            }

            return SpellEffectExecutionResult.Ok;
        }
    }
}
