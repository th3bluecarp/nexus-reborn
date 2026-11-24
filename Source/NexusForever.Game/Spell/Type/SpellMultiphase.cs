using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Abstract.Spell.Validator;
using NexusForever.Game.Spell.Event;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Spell.Type
{
    public class SpellMultiphase : Spell
    {
        public override CastMethod CastMethod => CastMethod.Multiphase;

        #region Dependency Injection

        private readonly ILogger<SpellMultiphase> log;

        public SpellMultiphase(
            ILogger<SpellMultiphase> log,
            ISpellTargetInfoCollection spellTargetInfoCollection,
            IGlobalSpellManager globalSpellManager,
            ICastResultValidatorManager castResultValidatorManager,
            IDisableManager disableManager)
            : base(log, spellTargetInfoCollection, globalSpellManager, castResultValidatorManager, disableManager)
        {
            this.log = log;
        }

        #endregion

        public override bool Cast()
        {
            if (!base.Cast())
                return false;

            hasFirstPhase = Parameters.SpellInfo.Phases.Any(p => p.OrderIndex == 0u);

            uint spellDelay = 0;
            for (int i = 0; i < Parameters.SpellInfo.Phases.Count; i++)
            {
                int index = i;
                SpellPhaseEntry spellPhase = Parameters.SpellInfo.Phases[i];
                spellDelay += spellPhase.PhaseDelay;
                events.EnqueueEvent(new SpellEvent(spellDelay / 1000d, () =>
                {
                    currentPhase = (byte)spellPhase.OrderIndex;
                    Execute();

                    if (i == Parameters.SpellInfo.Phases.Count - 1)
                    {
                        status = SpellStatus.Finishing;
                        log.LogTrace($"SpellMultiphase {Parameters.SpellInfo.Entry.Id} has finished executing.");
                    }
                }));
            }

            status = SpellStatus.Casting;
            log.LogTrace($"SpellMultiphase {Parameters.SpellInfo.Entry.Id} has started casting.");
            return true;
        }

        protected override bool _IsCasting()
        {
            return base._IsCasting() && (status == SpellStatus.Casting || status == SpellStatus.Executing);
        }

        protected override bool IsTelegraphValid(TelegraphDamageEntry telegraph)
        {
            // Ensure only telegraphs that apply to this Execute phase are evaluated.
            if (currentPhase >= 255)
                return true;

            int phaseMask = 1 << currentPhase;
            return telegraph.PhaseFlags != 1 && (phaseMask & telegraph.PhaseFlags) != 0;
        }

        // whether SpellPhase has a phase with order index 0 for this spell, the phase the flag value 1 stands for
        private bool hasFirstPhase;

        /// <remarks>
        /// PhaseFlags has one bit per phase and bit 0 is the first phase. An effect flagged 1 used to run in every phase:
        /// Crushing Flurry (46692) has four hits flagged 1, 2, 4 and 8 and landed seven, the first hit again with each later one
        /// (soldier run 20261124-102254: one health update for its first tick, two for each of the next three). It now runs in
        /// the first phase only. A spell with no phase 0 keeps the old rule, there a flag of 1 would never match.
        /// </remarks>
        protected override bool CanExecuteEffect(Spell4EffectsEntry spell4EffectsEntry)
        {
            if (currentPhase < 255)
            {
                int phaseMask = 1 << currentPhase;
                bool everyPhase = spell4EffectsEntry.PhaseFlags == uint.MaxValue
                    || (spell4EffectsEntry.PhaseFlags == 1u && !hasFirstPhase);
                if (!everyPhase && (phaseMask & spell4EffectsEntry.PhaseFlags) == 0)
                    return false;
            }

            return base.CanExecuteEffect(spell4EffectsEntry);
        }
    }
}
