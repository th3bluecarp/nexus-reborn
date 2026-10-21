using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Proc;
using NexusForever.Game.Combat.CrowdControl;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;

namespace NexusForever.Game.Abstract.Entity
{
    /// <summary>
    /// An <see cref="IUnitEntity"/> is an extension to <see cref="IWorldEntity"/> which can cast spells, be targed by spells and participate in combat.
    /// </summary>
    public interface IUnitEntity : IWorldEntity
    {
        float HitRadius { get; }

        /// <summary>
        /// Guid of the <see cref="IWorldEntity"/> currently targeted.
        /// </summary>
        uint? TargetGuid { get; }

        /// <summary>
        /// Determines whether or not this <see cref="IUnitEntity"/> is alive.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Determines whether or not this <see cref="IUnitEntity"/> is in combat.
        /// </summary>
        bool InCombat { get; }

        public IThreatManager ThreatManager { get; }
        public IProcManager ProcManager { get; }
        public ICrowdControlManager CrowdControlManager { get; }

        /// <summary>
        /// Add a <see cref="Property"/> modifier given a Spell4Id and <see cref="ISpellPropertyModifier"/> instance.
        /// </summary>
        void AddSpellModifierProperty(ISpellPropertyModifier modifier, uint spell4Id);

        /// <summary>
        /// Remove a <see cref="Property"/> modifier by a Spell that is currently affecting this <see cref="IUnitEntity"/>.
        /// </summary>
        void RemoveSpellProperty(Property property, uint spell4Id);

        /// Checks if this <see cref="IUnitEntity"/> is currently casting a spell.
        /// </summary>
        /// <returns></returns>
        bool IsCasting();

        /// <summary>
        /// Return <see cref="ISpell"/> with the supplied casting id.
        /// </summary>
        ISpell GetSpell(uint castingId);

        /// <summary>
        /// Return <see cref="ISpell"/> with the supplied spell id.
        /// </summary>
        ISpell GetSpellBySpellId(uint spellId);

        /// <summary>
        /// Return <see cref="ISpell"/> with the supplied base spell id.
        /// </summary>
        ISpell GetSpellByBaseSpellId(uint baseSpellId);

        /// <summary>
        /// Return a collection of <see cref="ISpell"/> that are part of the supplied spell group id.
        /// </summary>
        IEnumerable<ISpell> GetSpellsByGroupId(uint spellGroupId);

        /// <summary>
        /// Return a collection of <see cref="ISpell"/> that are applying the supplied <see cref="SpellEffectType"/> to entity.
        /// </summary>
        IEnumerable<ISpell> GetSpellsByEffect(SpellEffectType type);

        /// <summary>
        /// Check if this <see cref="IUnitEntity"/> has a spell active with the provided <see cref="Spell4Entry"/> Id
        /// </summary>
        bool HasSpell(uint spell4Id, out ISpell spell, bool isCasting = false);

        /// <summary>
        /// Check if this <see cref="IUnitEntity"/> has a spell active with the provided <see cref="CastMethod"/>
        /// </summary>
        bool HasSpell(CastMethod castMethod, out ISpell spell);

        /// <summary>
        /// Check if this <see cref="IUnitEntity"/> has a spell active with the provided <see cref="Func"/> predicate.
        /// </summary>
        bool HasSpell(Func<ISpell, bool> predicate, out ISpell spell);

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell id and <see cref="ISpellParameters"/>.
        /// </summary>
        void CastSpell<T>(T spell4Id, ISpellParameters parameters) where T : Enum;

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell id and <see cref="ISpellParameters"/>.
        /// </summary>
        void CastSpell(uint spell4Id, ISpellParameters parameters);

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell base id, tier and <see cref="ISpellParameters"/>.
        /// </summary>
        void CastSpell(uint spell4BaseId, byte tier, ISpellParameters parameters);

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied <see cref="ISpellParameters"/>.
        /// </summary>
        void CastSpell(ISpellParameters parameters);

        /// <summary>
        /// Cancel any <see cref="ISpell"/>'s that are interrupted by movement.
        /// </summary>
        void CancelSpellsOnMove();

        /// <summary>
        /// Cancel an <see cref="ISpell"/> based on its casting id.
        /// </summary>
        /// <param name="castingId">Casting ID of the spell to cancel</param>
        void CancelSpellCast(uint castingId);

        /// <summary>
        /// Determine if this <see cref="IUnitEntity"/> can attack supplied <see cref="IUnitEntity"/>.
        /// </summary>
        bool CanAttack(IUnitEntity target);

        /// <summary>
        /// Returns whether or not this <see cref="IUnitEntity"/> is an attackable target for supplied <see cref="IUnitEntity"/>.
        /// </summary>
        bool IsValidAttackTarget(IUnitEntity attacker);

        /// <summary>
        /// Deal damage to this <see cref="IUnitEntity"/> from the supplied <see cref="IUnitEntity"/>.
        /// </summary>
        void TakeDamage(IUnitEntity attacker, IDamageDescription damageDescription);

        /// <summary>
        /// Add an absorb pool from a spell effect, damage of a type in the mask is taken from it before shield and health.
        /// </summary>
        /// <remarks>
        /// The mask has one bit per <see cref="DamageType"/> (bit 0 physical, bit 1 tech, bit 2 magic), a mask of 0 absorbs everything.
        /// </remarks>
        void AddAbsorb(uint effectId, uint amount, uint damageTypeMask);

        /// <summary>
        /// Remove the absorb pool added by supplied spell effect.
        /// </summary>
        void RemoveAbsorb(uint effectId);

        /// <summary>
        /// Take as much of supplied damage as the absorb pools allow and return the amount absorbed.
        /// </summary>
        uint ConsumeAbsorb(DamageType damageType, uint damage);

        /// <summary>
        /// Add a damage or healing multiplier from a PersonalDmgHealMod effect, <paramref name="otherGuid"/> is the unit it applies against (0 for any unit).
        /// </summary>
        /// <remarks>
        /// Categories 3 to 5 scale physical, tech and magic damage this unit deals, 6 to 8 the same damage types this unit takes,
        /// 12 healing and 13 shield healing this unit receives.
        /// </remarks>
        void AddPersonalModifier(uint effectId, uint casterGuid, uint category, uint otherGuid, float multiplier);

        /// <summary>
        /// Remove the multiplier added by supplied spell effect and caster.
        /// </summary>
        void RemovePersonalModifier(uint effectId, uint casterGuid);

        /// <summary>
        /// Product of the multipliers of supplied category that apply against supplied unit.
        /// </summary>
        float GetPersonalModifier(uint category, uint otherGuid);

        /// <summary>
        /// ModifySpell and ModifySpellEffect effects active on this unit, they change the unit's own spells.
        /// </summary>
        IEnumerable<GameTable.Model.Spell4EffectsEntry> SpellModifiers { get; }

        /// <summary>
        /// Add a ModifySpell or ModifySpellEffect effect, until removed.
        /// </summary>
        void AddSpellModifier(uint effectId, uint casterGuid, GameTable.Model.Spell4EffectsEntry entry);

        /// <summary>
        /// Remove the modifier added by supplied spell effect and caster.
        /// </summary>
        void RemoveSpellModifier(uint effectId, uint casterGuid);

        /// <summary>
        /// Make this unit immune to effects of supplied type from other units, until removed.
        /// </summary>
        void AddEffectImmunity(uint effectId, uint casterGuid, SpellEffectType type);

        /// <summary>
        /// Remove the immunity added by supplied spell effect and caster.
        /// </summary>
        void RemoveEffectImmunity(uint effectId, uint casterGuid);

        /// <summary>
        /// Returns if this unit is immune to effects of supplied type from other units.
        /// </summary>
        bool IsImmuneToEffect(SpellEffectType type);

        /// <summary>
        /// Add a DelayDeath trigger: when damage would kill this unit, <paramref name="spell4Id"/> is cast and death is held off for <paramref name="delayMs"/>.
        /// </summary>
        void AddDeathTrigger(uint effectId, uint casterGuid, uint entryId, uint mode, uint spell4Id, uint delayMs, uint prerequisiteId, uint cooldownMs);

        /// <summary>
        /// Remove the DelayDeath trigger added by supplied spell effect and caster.
        /// </summary>
        void RemoveDeathTrigger(uint effectId, uint casterGuid);

        /// <summary>
        /// Modify the health of this <see cref="IUnitEntity"/> by the supplied amount.
        /// </summary>
        /// <remarks>
        /// If the <see cref="DamageType"/> is <see cref="DamageType.Heal"/> amount is added to current health otherwise subtracted.
        /// </remarks>
        void ModifyHealth(uint amount, DamageType? type, IUnitEntity source);

        /// <summary>
        /// Remove this unit from the map without killing it, world database spawns come back after the respawn delay.
        /// </summary>
        void Despawn();

        /// <summary>
        /// Return active <see cref="ISpell"/>s cast by this unit that have the supplied unit as a target.
        /// </summary>
        IEnumerable<ISpell> GetSpellsAffecting(IUnitEntity target);

        /// <summary>
        /// Returns if the unit is stealthed by at least one active stealth effect.
        /// </summary>
        bool IsStealthed { get; }

        /// <summary>
        /// Add a stealth source from the spell with the supplied casting id.
        /// </summary>
        void AddStealth(uint castingId);

        /// <summary>
        /// Remove the stealth source from the spell with the supplied casting id.
        /// </summary>
        void RemoveStealth(uint castingId);

        /// <summary>
        /// End every stealth effect on the unit, for example when it attacks.
        /// </summary>
        void BreakStealth();

        /// <summary>
        /// Returns if the unit takes no damage, from at least one active invulnerability effect.
        /// </summary>
        bool IsInvulnerable { get; }

        /// <summary>
        /// Returns if the unit is busy (SetBusy effect), busy objects cannot be activated.
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Changes every time the busy state is set, so a delayed clear can tell whether it still owns the state.
        /// </summary>
        uint BusyToken { get; }

        /// <summary>
        /// Creature AI does not act before this time (UTC), set by NpcExecutionDelay spell effects.
        /// </summary>
        DateTime ExecutionDelayUntil { get; set; }

        /// <summary>
        /// Set or clear the busy state.
        /// </summary>
        void SetBusy(bool busy);

        /// <summary>
        /// Add or remove an invulnerability source from the spell with the supplied casting id.
        /// </summary>
        void SetInvulnerable(uint castingId, bool invulnerable);

        /// <summary>
        /// Stance used by the creature AI when this unit is a combat pet.
        /// </summary>
        NexusForever.Game.Static.Pet.PetStance PetStance { get; set; }

        /// <summary>
        /// Set target to supplied target guid.
        /// </summary>
        /// <remarks>
        /// A null target will clear the current target.
        /// </remarks>
        void SetTarget(uint? target, uint threat = 0u);

        /// <summary>
        /// Set target to supplied <see cref="IUnitEntity"/>.
        /// </summary>
        /// <remarks>
        /// A null target will clear the current target.
        /// </remarks>
        void SetTarget(IWorldEntity target, uint threat = 0u);

        /// <summary>
        /// Invoked when a new <see cref="IHostileEntity"/> is added to the threat list.
        /// </summary>
        void OnThreatAddTarget(IHostileEntity hostile);

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is removed from the threat list.
        /// </summary>
        void OnThreatRemoveTarget(IHostileEntity hostile);

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is update on the threat list.
        /// </summary>
        void OnThreatChange(IHostileEntity hostile);
    }
}
