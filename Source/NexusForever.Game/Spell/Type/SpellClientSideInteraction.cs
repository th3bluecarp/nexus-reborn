using Microsoft.Extensions.Logging;
using NexusForever.Network.World.Message.Model.Spell;
using NexusForever.Game.Abstract;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Abstract.Spell.Validator;
using NexusForever.Game.Spell.Event;
using NexusForever.Game.Static.Spell;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.Game.Spell.Type
{
    public class SpellClientSideInteraction : Spell
    {
        public override CastMethod CastMethod => CastMethod.ClientSideInteraction;

        #region Dependency Injection

        private readonly ILogger<SpellClientSideInteraction> log;
        private bool completesByTimer;
        private bool interactionReady;
        private bool interactionCompleted;

        public SpellClientSideInteraction(
            ILogger<SpellClientSideInteraction> log,
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

            // Who completes the cast (the split is upstream's, in place since 2021):
            //  - a cast that is not a true client-side interaction (no interaction data, or a table cast method other than
            //    client-side) completes by itself at the end of its cast time. The client's answer to such a cast only marks
            //    it ready (SucceedClientInteraction): running it on the answer as well executed one activation twice (two
            //    settler resources per use, double quest credit in Helping Hand).
            //  - a true client-side interaction has no timer: the client's answer completes it, or its fail answer cancels it.
            // The cast time rule is unchanged: a creature's activate cast time when it has one, else the spell's own.
            double castTime = Parameters.CastTimeOverride > 0 ? Parameters.CastTimeOverride / 1000d : Parameters.SpellInfo.Entry.CastTime / 1000d;
            completesByTimer = Parameters.ClientSideInteraction == null
                || (CastMethod)Parameters.SpellInfo.BaseInfo.Entry.CastMethod != CastMethod.ClientSideInteraction;
            if (completesByTimer)
                events.EnqueueEvent(new SpellEvent(castTime, CompleteInteraction));

            status = SpellStatus.Casting;
            log.LogTrace($"Spell {Parameters.SpellInfo.Entry.Id} has started casting.");
            return true;
        }

        protected override bool _IsCasting()
        {
            return base._IsCasting() && status == SpellStatus.Casting;
        }

        private void SendSpellStartClientInteraction()
        {
            // Shoule we actually emit client interaction events to everyone? - Logs suggest that we only see this packet firing when the client interacts with -something- and is likely only sent to them
            if (Caster is IPlayer player)
            {
                player.Session.EnqueueMessageEncrypted(new ServerSpellConfirmCast
                {
                    ClientSpellCastUniqueId = Parameters.ClientSideInteraction.ClientUniqueId,
                    CastingId = CastingId,
                    CasterId = GetPrimaryTargetId()
                });
            }
        }

        /// <summary>
        /// Used when a <see cref="CSI.ClientSideInteraction"/> succeeds
        /// </summary>
        public void SucceedClientInteraction()
        {
            if (!completesByTimer)
            {
                // true client-side interaction: the answer is the completion
                CompleteInteraction();
                return;
            }

            // timer-completed cast: the answer only marks the client ready, the cast-time timer runs it once
            if (interactionReady || interactionCompleted)
                return;
            interactionReady = true;
            Parameters.ClientSideInteraction?.TriggerReady();
        }

        /// <summary>
        /// Execute an interaction once: at the end of its cast time, or on the client's answer for a true client-side interaction.
        /// </summary>
        private void CompleteInteraction()
        {
            if (interactionCompleted)
                return;
            interactionCompleted = true;

            bool hasActivateEffect = Parameters.SpellInfo.Effects.Any(x => x.EffectType == SpellEffectType.Activate);
            IGridEntity primaryTarget = Parameters.PrimaryTargetId == 0u
                ? Caster
                : Caster.GetVisible<IGridEntity>(Parameters.PrimaryTargetId);
            Execute();

            if (!hasActivateEffect || primaryTarget is not IUnitEntity)
                Parameters.ClientSideInteraction?.HandleSuccess(this);
        }

        /// <summary>
        /// Used when a <see cref="CSI.ClientSideInteraction"/> fails
        /// </summary>
        public void FailClientInteraction()
        {
            Parameters.ClientSideInteraction?.TriggerFail();

            CancelCast(CastResult.ClientSideInteractionFail);
        }

        protected override void OnStatusChange(SpellStatus previousStatus, SpellStatus status)
        {
            switch (status)
            {
                case SpellStatus.Casting:
                    if (Parameters.ClientSideInteraction?.Entry != null || Parameters.ClientSideInteraction == null)
                        SendSpellStart();
                    else
                        SendSpellStartClientInteraction();
                    break;
            }
        }

        protected override uint GetPrimaryTargetId()
        {
            return Parameters.ClientSideInteraction?.Entry != null ? Caster.Guid : Parameters.PrimaryTargetId;
        }
    }
}
