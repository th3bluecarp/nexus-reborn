using NexusForever.Game.Abstract.Combat.CrowdControl;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Combat.CrowdControl;
using NexusForever.Game.Static.Entity;

namespace NexusForever.Game.Combat.CrowdControl
{
    public class CrowdControlManager : ICrowdControlManager
    {
        private IUnitEntity owner;

        private readonly Dictionary<CCState, ISpellTargetEffectInfo> ccEffects = [];
        private readonly Dictionary<ISpellTargetEffectInfo, ITemporaryInterruptArmour> temporaryInterruptArmour = [];

        public void Initialise(IUnitEntity entity)
        {
            if (owner != null)
                throw new InvalidOperationException("CrowdControlManager is already initialised.");

            owner = entity;
        }

        /// <summary>
        /// Checks if the specified <see cref="CCState"/> can be applied to the target.
        /// </summary>
        public ICrowdControlApplyResult CanApplyCCEffect(CCState ccState)
        {
            // TODO: not sure if this entirely correct...
            if (GetCCEffect(ccState) != null)
            {
                return new CrowdControlApplyResult
                {
                    Result = CCStateApplyRulesResult.StackingDoesNotStack
                };
            }

            // TODO: handle StackingDoesNotStack, StackingShorterDuration

            // diminishing returns, invented: after a crowd control state ends the unit is immune to that state for twice its
            // duration (at least ImmunityMinimumSeconds), so a boss telegraph that re-applies a stun every few seconds cannot hold a
            // player stunned forever (Algoroc's Boulderback, Loftite Shards; SEMANTICS: crowd control immunity)
            if (immuneUntil.TryGetValue(ccState, out DateTime until) && until > DateTime.UtcNow)
            {
                return new CrowdControlApplyResult
                {
                    Result = CCStateApplyRulesResult.TargetImmune
                };
            }

            return new CrowdControlApplyResult
            {
                Result = CCStateApplyRulesResult.Ok,
            };
        }

        private const double ImmunityMinimumSeconds = 5d;
        private readonly Dictionary<CCState, DateTime> immuneUntil = new();

        /// <summary>
        /// Adds the specified <see cref="CCState"/> to the target.
        /// </summary>
        public void AddCCEffect(CCState state, ISpellTargetEffectInfo effect)
        {
            ccEffects.Add(state, effect);
        }

        /// <summary>
        /// Removes the specified <see cref="CCState"/> from the target.
        /// </summary>
        public void RemoveCCEffect(CCState state)
        {
            if (ccEffects.TryGetValue(state, out ISpellTargetEffectInfo effect))
            {
                double seconds = Math.Max(ImmunityMinimumSeconds, 2d * (effect?.Entry?.DurationTime ?? 0u) / 1000d);
                immuneUntil[state] = DateTime.UtcNow.AddSeconds(seconds);
            }
            ccEffects.Remove(state);
        }

        /// <summary>
        /// Returns the <see cref="ISpellTargetEffectInfo"/> for the specified <see cref="CCState"/>.
        /// </summary>
        public ISpellTargetEffectInfo GetCCEffect(CCState state)
        {
            return ccEffects.TryGetValue(state, out ISpellTargetEffectInfo spell) ? spell : null;
        }

        /// <summary>
        /// Returns all <see cref="CCState"/>'s currently affecting the target.
        /// </summary>
        public IEnumerable<CCState> GetCCStates()
        {
            return ccEffects.Keys;
        }

        /// <summary>
        /// Returns the total amount of temporary interrupt armour on the target.
        /// </summary>
        public uint GetTemporaryInterruptArmour()
        {
            return (uint)temporaryInterruptArmour.Values.Sum(i => i.Amount);
        }

        /// <summary>
        /// Add temporary interrupt armour for the specified <see cref="ISpellTargetEffectInfo"/>.
        /// </summary>
        public void AddTemporaryInterruptArmour(ISpellTargetEffectInfo info, uint amount)
        {
            if (owner.MaxInterruptArmour == -1)
                return;

            temporaryInterruptArmour.Add(info, new TemporaryInterruptArmour(info, amount));

            owner.CalculateProperty(Property.InterruptArmorThreshold);
            owner.InterruptArmour += amount;
        }

        /// <summary>
        /// Remove the temporary interrupt armour granted from the specified <see cref="ISpellTargetEffectInfo"/>.
        /// </summary>
        public void RemoveTemporaryInterruptArmour(ISpellTargetEffectInfo info)
        {
            if (owner.MaxInterruptArmour == -1)
                return;

            if (!temporaryInterruptArmour.TryGetValue(info, out ITemporaryInterruptArmour interruptArmour))
                return;

            temporaryInterruptArmour.Remove(info);

            owner.InterruptArmour -= interruptArmour.Amount;
            owner.CalculateProperty(Property.InterruptArmorThreshold);
        }

        /// <summary>
        /// Removes the specified amount of interrupt armour from the target.
        /// </summary>
        /// <remarks>
        /// This will first remove temporary interrupt armour, then permanent interrupt armour.
        /// </remarks>
        public void RemoveInterruptArmour(ref uint amount)
        {
            if (owner.MaxInterruptArmour == -1)
                return;

            // temporary interrupt armour is generally used by players
            // spells that increase interrupt armour for a short period of time and is removed once the interrupt armour is depleted
            RemoveTemporaryInterruptArmour(ref amount);
            // "permanent" interrupt armour is generally used by bosses
            // spells that increase MAX interrupt armour and is not removed until the effect ends
            RemovePermanentInterruptArmour(ref amount);
        }

        private void RemoveTemporaryInterruptArmour(ref uint amount)
        {
            if (amount == 0)
                return;

            foreach (ITemporaryInterruptArmour interruptArmour in temporaryInterruptArmour.Values)
            {
                uint reduction = (uint)Math.Min(owner.InterruptArmour, Math.Min(interruptArmour.Amount, amount));
                owner.InterruptArmour -= reduction;
                interruptArmour.Amount -= reduction;
                amount -= reduction;

                if (interruptArmour.Amount == 0)
                    interruptArmour.Info.Finish();
            }

            owner.CalculateProperty(Property.InterruptArmorThreshold);
        }

        private void RemovePermanentInterruptArmour(ref uint amount)
        {
            if (amount == 0)
                return;

            uint reduction = (uint)Math.Min(owner.InterruptArmour, amount);
            owner.InterruptArmour -= (int)reduction;
            amount -= reduction;
        }
    }
}
