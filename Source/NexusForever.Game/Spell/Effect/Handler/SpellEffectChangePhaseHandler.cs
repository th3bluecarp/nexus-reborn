using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Moves a player into other world phases (Use Portal, Ritual of Departed Ancestors, spirit realms).
    /// </summary>
    /// <remarks>
    /// DataBits00 is the phase mask the player perceives and DataBits01 the mask that perceives the player. Login sends 1 and 1,
    /// a timed effect returns the player to them when it ends.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.ChangePhase)]
    public class SpellEffectChangePhaseHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is not IPlayer player || data.DataBits00 == 0u)
                return SpellEffectExecutionResult.PreventEffect;

            player.Session.EnqueueMessageEncrypted(new ServerPhaseVisibilityWorldLocation
            {
                PhasesIPerceive      = data.DataBits00,
                PhasesThatPerceiveMe = data.DataBits01 != 0u ? data.DataBits01 : data.DataBits00
            });

            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            if (target is not IPlayer player || info.Entry.DurationTime == 0u)
                return;

            player.Session.EnqueueMessageEncrypted(new ServerPhaseVisibilityWorldLocation
            {
                PhasesIPerceive      = 1u,
                PhasesThatPerceiveMe = 1u
            });
        }
    }
}
