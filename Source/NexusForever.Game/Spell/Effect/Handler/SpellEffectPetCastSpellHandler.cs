using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Info;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Makes the caster's pets of a creature cast a spell, used by the Engineer bot ability buttons.
    /// </summary>
    /// <remarks>
    /// DataBits00 is the pet Creature2 id and DataBits01 the Spell4 the pet casts, at the owner's target, else the pet's own target. A spell aimed at a primary target is not cast without one, other spells fall back to the pet itself.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.PetCastSpell)]
    public class SpellEffectPetCastSpellHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        #region Dependency Injection

        private readonly ISpellInfoManager spellInfoManager;

        public SpellEffectPetCastSpellHandler(
            ISpellInfoManager spellInfoManager)
        {
            this.spellInfoManager = spellInfoManager;
        }

        #endregion

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            IUnitEntity caster = executionContext.Spell.Caster;
            if (caster == null || data.DataBits01 == 0u)
                return SpellEffectExecutionResult.PreventEffect;

            bool needsTarget = spellInfoManager.GetSpellInfo(data.DataBits01)?.BaseInfo.TargetMechanics?.TargetType == SpellTargetMechanicType.PrimaryTarget;

            bool cast = false;
            foreach (IUnitEntity pet in caster.SummonFactory.GetSummons(data.DataBits00).OfType<IUnitEntity>())
            {
                if (!pet.IsAlive)
                    continue;

                uint? targetGuid = caster.TargetGuid ?? pet.TargetGuid;
                if (targetGuid == null && needsTarget)
                    continue;

                pet.CastSpell(data.DataBits01, new SpellParameters
                {
                    PrimaryTargetId        = targetGuid ?? pet.Guid,
                    UserInitiatedSpellCast = false
                });
                cast = true;
            }

            return cast ? SpellEffectExecutionResult.Ok : SpellEffectExecutionResult.PreventEffect;
        }
    }
}
