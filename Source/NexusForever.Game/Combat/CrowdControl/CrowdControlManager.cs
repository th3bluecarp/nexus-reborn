using NexusForever.Game.Abstract.Combat.CrowdControl;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Combat.CrowdControl;
using NexusForever.Game.Static.Entity;
using NexusForever.Network.World.Message.Model.Entity;
using NLog;

namespace NexusForever.Game.Combat.CrowdControl
{
    public class CrowdControlManager : ICrowdControlManager
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        private IUnitEntity owner;

        private readonly Dictionary<CCState, ISpellTargetEffectInfo> ccEffects = [];
        // when each state must be gone at the latest: its effect's duration plus a grace second, or never for an open-ended state
        private readonly Dictionary<CCState, DateTime> expiresAt = [];
        private const double ExpiryGraceSeconds = 1d;
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

            TimeSpan? duration = effect?.GetDuration();
            expiresAt[state] = duration.HasValue && duration.Value > TimeSpan.Zero
                ? DateTime.UtcNow + duration.Value + TimeSpan.FromSeconds(ExpiryGraceSeconds)
                : DateTime.MaxValue;
        }

        /// <summary>
        /// Drops every <see cref="CCState"/> whose effect outlived its duration or its spell.
        /// </summary>
        /// <remarks>
        /// An effect's duration is ticked by its spell, and a spell is ticked by its caster: when the caster died or despawned
        /// before the effect ended (a creature killed during its own 20 s stun in Algoroc), the stun on the player never ended,
        /// every cast answered CCStun for the rest of the session and the player died over and over (2026-11-02).
        /// </remarks>
        public void Update(double lastTick)
        {
            if (ccEffects.Count == 0)
                return;

            foreach ((CCState state, ISpellTargetEffectInfo effect) in ccEffects.ToList())
            {
                bool spellGone = effect?.Target?.Collection?.Spell?.IsFinished ?? true;
                bool expired   = expiresAt.TryGetValue(state, out DateTime at) && DateTime.UtcNow > at;
                if (!spellGone && !expired)
                    continue;

                log.Warn($"Crowd control {state} on unit {owner.Guid} from spell {effect?.Target?.Collection?.Spell?.Spell4Id} outlived its {(spellGone ? "spell" : "duration")}, removing it.");
                ReleaseCCEffect(state, effect);
            }
        }

        /// <summary>
        /// Removes every <see cref="CCState"/> from the target, with the client told of each removal.
        /// </summary>
        public void RemoveAllCCEffects(string reason)
        {
            if (ccEffects.Count == 0)
                return;

            foreach ((CCState state, ISpellTargetEffectInfo effect) in ccEffects.ToList())
            {
                log.Trace($"Crowd control {state} on unit {owner.Guid} removed: {reason}.");
                ReleaseCCEffect(state, effect);
            }
        }

        private void ReleaseCCEffect(CCState state, ISpellTargetEffectInfo effect)
        {
            // the effect's own finish runs the crowd control remove handler, which removes the state here and tells the client
            if (effect != null && !effect.IsFinalised)
                effect.Finish();

            if (!ccEffects.ContainsKey(state))
                return;

            // the effect was already finalised without reaching us: drop the state and tell the client ourselves
            RemoveCCEffect(state);
            owner.EnqueueToVisible(new ServerEntityCCStateRemove
            {
                UnitId              = owner.Guid,
                CCType              = state,
                SpellCastUniqueId   = effect?.Target?.Collection?.Spell?.CastingId ?? 0u,
                SpellEffectUniqueId = effect?.EffectId ?? 0u,
                Removed             = true
            }, true);
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
            expiresAt.Remove(state);
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
