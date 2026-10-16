using System.Numerics;
using SpellInit = NexusForever.Network.World.Message.Model.Shared.SpellInit;
using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Abstract.Map.Instance;
using NexusForever.Game.Configuration.Model;
using NexusForever.Shared.Configuration;
using NexusForever.Shared.Game;
using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Movement;
using NexusForever.Game.Abstract.Entity.Stat;
using NexusForever.Game.Abstract.Group;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Info;
using NexusForever.Game.Abstract.Spell.Proc;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Combat;
using NexusForever.Game.Combat.CrowdControl;
using NexusForever.Game.Static;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.PublicEvent;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Combat;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.Shared;
using NexusForever.Network.World.Message.Static;
using NexusForever.Script;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Collection;
using NexusForever.Shared;

namespace NexusForever.Game.Entity
{
    public abstract class UnitEntity : WorldEntity, IUnitEntity
    {
        private static readonly NLog.ILogger combatLog = NLog.LogManager.GetLogger("Combat");

        public float HitRadius { get; protected set; } = 1f;

        /// <summary>
        /// Guid of the <see cref="IUnitEntity"/> currently targeted.
        /// </summary>
        public uint? TargetGuid { get; private set; }

        /// <summary>
        /// Determines whether or not this <see cref="IUnitEntity"/> is alive.
        /// </summary>
        public bool IsAlive => Health > 0u && deathState == null;

        protected EntityDeathState? DeathState
        {
            get => deathState;
            set
            {
                deathState = value;

                if (deathState is null or EntityDeathState.JustDied)
                {
                    EnqueueToVisible(new ServerEntityDeathState
                    {
                        UnitId    = Guid,
                        Dead      = !IsAlive,
                        Reason    = 0, // client does nothing with this value
                        RezHealth = IsAlive ? Health : 0u
                    }, true);
                }
            }
        }

        private EntityDeathState? deathState;

        /// <summary>
        /// Determines whether or not this <see cref="IUnitEntity"/> is in combat.
        /// </summary>
        public bool InCombat
        {
            get => inCombat;
            private set
            {
                if (inCombat == value)
                    return;

                inCombat = value;

                EnqueueToVisible(new ServerUnitEnteredCombat
                {
                    UnitId   = Guid,
                    InCombat = value
                }, true);
            }
        }

        private bool inCombat;

        public IThreatManager ThreatManager { get; private set; }
        public IProcManager ProcManager { get; private set; }
        public ICrowdControlManager CrowdControlManager { get; private set; }

        private IStatUpdateManager statUpdateManager;

        private readonly List<ISpell> pendingSpells = [];
        private readonly Dictionary<uint, ISpell> spells = [];

        private readonly Dictionary<Property, Dictionary</*spell4Id*/uint, ISpellPropertyModifier>> spellProperties = [];

        #region Dependency Injection

        private readonly ISpellFactory spellFactory;

        public UnitEntity(IMovementManager movementManager,
            IEntitySummonFactory entitySummonFactory,
            IStatUpdateManager statUpdateManager,
            ISpellFactory spellFactory)
            : base(movementManager, entitySummonFactory)
        {
            this.statUpdateManager = statUpdateManager;
            this.spellFactory      = spellFactory;

            // TODO: find a better way to initialise unit managers...
            ThreatManager = new ThreatManager(this);
            ProcManager   = LegacyServiceProvider.Provider.GetService<IProcManager>();
            ProcManager.Initialise(this);
            CrowdControlManager = LegacyServiceProvider.Provider.GetService<ICrowdControlManager>();
            CrowdControlManager.Initialise(this);

            InitialiseHitRadius();
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();

            foreach (ISpell spell in spells.Values)
                spell.Dispose();
        }

        private void InitialiseHitRadius()
        {
            if (CreatureInfo?.ModelEntry != null)
                HitRadius = CreatureInfo.ModelEntry.HitRadius * CreatureInfo.Entry.ModelScale;
        }

        public override void Update(double lastTick)
        {
            base.Update(lastTick);

            // new spells can be added during updates
            // we need a pending queue to prevent issues with iterating over the spell dictionary
            foreach (ISpell spell in pendingSpells)
                spells.Add(spell.CastingId, spell);

            pendingSpells.Clear();

            foreach (ISpell spell in spells.Values)
            {
                spell.Update(lastTick);
                spell.LateUpdate(lastTick);
                if (spell.IsFinished)
                    spells.Remove(spell.CastingId);
            }

            statUpdateManager.Update(lastTick);

            if (respawnTimer != null)
            {
                respawnTimer.Update(lastTick);
                if (respawnTimer.HasElapsed)
                {
                    respawnTimer = null;
                    Respawn();
                }
            }

            ProcManager.Update(lastTick);
        }

        public override ServerEntityCreate BuildCreatePacket(bool initialCommands)
        {
            ServerEntityCreate entityCreate = base.BuildCreatePacket(initialCommands);

            foreach (ISpell spell in spells.Values)
            {
                SpellInit spellInit = spell.BuildSpellInit();
                entityCreate.SpellInitData.Add(spellInit);
            }

            // TODO
            // entityCreate.CurrentSpellUniqueId

            return entityCreate;
        }

        /// <summary>
        /// Initialise <see cref="IScriptCollection"/> for <see cref="IUnitEntity"/>.
        /// </summary>
        protected override void InitialiseScriptCollection(List<string> names)
        {
            scriptCollection = ScriptManager.Instance.InitialiseOwnedCollection<IUnitEntity>(this);
            ScriptManager.Instance.InitialiseEntityScripts<IUnitEntity>(scriptCollection, this, names);
        }

        /// <summary>
        /// Remove tracked <see cref="IGridEntity"/> that is no longer in vision range.
        /// </summary>
        protected override void RemoveVisible(IGridEntity entity)
        {
            if (entity.Guid == TargetGuid)
                SetTarget((IWorldEntity)null);

            ThreatManager.RemoveHostile(entity.Guid);

            base.RemoveVisible(entity);
        }

        /// <summary>
        /// Add a <see cref="Property"/> modifier given a Spell4Id and <see cref="ISpellPropertyModifier"/> instance.
        /// </summary>
        public void AddSpellModifierProperty(ISpellPropertyModifier spellModifier, uint spell4Id)
        {
            if (spellProperties.TryGetValue(spellModifier.Property, out Dictionary<uint, ISpellPropertyModifier> spellDict))
            {
                if (spellDict.ContainsKey(spell4Id))
                    spellDict[spell4Id] = spellModifier;
                else
                    spellDict.Add(spell4Id, spellModifier);
            }
            else
            {
                spellProperties.Add(spellModifier.Property, new Dictionary<uint, ISpellPropertyModifier>
                {
                    { spell4Id, spellModifier }
                });
            }

            CalculateProperty(spellModifier.Property);
        }

        /// <summary>
        /// Remove a <see cref="Property"/> modifier by a Spell that is currently affecting this <see cref="IUnitEntity"/>.
        /// </summary>
        public void RemoveSpellProperty(Property property, uint spell4Id)
        {
            if (spellProperties.TryGetValue(property, out Dictionary<uint, ISpellPropertyModifier> spellDict))
                spellDict.Remove(spell4Id);

            CalculateProperty(property);
        }

        /// <summary>
        /// Return all <see cref="IPropertyModifier"/> for this <see cref="IUnitEntity"/>'s <see cref="Property"/>
        /// </summary>
        private IEnumerable<ISpellPropertyModifier> GetSpellPropertyModifiers(Property property)
        {
            return spellProperties.ContainsKey(property) ? spellProperties[property].Values : Enumerable.Empty<ISpellPropertyModifier>();
        }

        protected override void CalculatePropertyValue(IPropertyValue propertyValue)
        {
            base.CalculatePropertyValue(propertyValue);

            // Run through spell adjustments first because they could adjust base properties
            // dataBits01 appears to be some form of Priority or Math Operator
            foreach (ISpellPropertyModifier spellModifier in GetSpellPropertyModifiers(propertyValue.Property)
                .OrderByDescending(s => s.Priority))
            {
                foreach (IPropertyModifier alteration in spellModifier.Alterations)
                {
                    // TODO: Add checks to ensure we're not modifying FlatValue and Percentage in the same effect?
                    switch (alteration.ModType)
                    {
                        case ModType.FlatValue:
                        case ModType.LevelScale:
                            propertyValue.Value += alteration.GetValue(Level);
                            break;
                        case ModType.Percentage:
                            propertyValue.Value *= alteration.GetValue();
                            break;
                    }
                }
            }

            if (propertyValue.Property == Property.InterruptArmorThreshold)
                propertyValue.Value += CrowdControlManager.GetTemporaryInterruptArmour();
        }

        /// Checks if this <see cref="IUnitEntity"/> is currently casting a spell.
        /// </summary>
        /// <returns></returns>
        public bool IsCasting()
        {
            foreach (ISpell spell in spells.Values)
                if (spell.IsCasting)
                    return true;

            return false;
        }

        /// <summary>
        /// Return <see cref="ISpell"/> with the supplied casting id.
        /// </summary>
        public ISpell GetSpell(uint castingId)
        {
            return spells.TryGetValue(castingId, out ISpell spell) ? spell : null;
        }

        /// <summary>
        /// Return <see cref="ISpell"/> with the supplied spell id.
        /// </summary>
        public ISpell GetSpellBySpellId(uint spellId)
        {
            return spells.Values.SingleOrDefault(s => s.Spell4Id == spellId);
        }

        /// <summary>
        /// Return <see cref="ISpell"/> with the supplied base spell id.
        /// </summary>
        public ISpell GetSpellByBaseSpellId(uint baseSpellId)
        {
            return spells.Values.SingleOrDefault(s => s.Parameters.SpellInfo.BaseInfo.Entry.Id == baseSpellId);
        }

        /// <summary>
        /// Return a collection of <see cref="ISpell"/> that are part of the supplied spell group id.
        /// </summary>
        public IEnumerable<ISpell> GetSpellsByGroupId(uint spellGroupId)
        {
            foreach (ISpell spell in spells.Values)
                if (spell.Parameters.SpellInfo.SpellGroups.Contains(spellGroupId))
                    yield return spell;
        }

        private readonly HashSet<uint> stealthSources = [];
        private readonly HashSet<uint> invulnerableSources = [];

        /// <summary>
        /// Returns if the unit is busy (SetBusy effect), busy objects cannot be activated.
        /// </summary>
        public bool IsBusy { get; private set; }

        /// <summary>
        /// Changes every time the busy state is set, so a delayed clear can tell whether it still owns the state.
        /// </summary>
        public uint BusyToken { get; private set; }

        /// <summary>
        /// Set or clear the busy state.
        /// </summary>
        public void SetBusy(bool busy)
        {
            IsBusy = busy;
            BusyToken++;
        }

        /// <summary>
        /// Returns if the unit takes no damage, from at least one active invulnerability effect.
        /// </summary>
        public bool IsInvulnerable => invulnerableSources.Count > 0;

        /// <summary>
        /// Add or remove an invulnerability source from the spell with the supplied casting id.
        /// </summary>
        public void SetInvulnerable(uint castingId, bool invulnerable)
        {
            if (invulnerable)
                invulnerableSources.Add(castingId);
            else
                invulnerableSources.Remove(castingId);
        }

        /// <summary>
        /// Stance used by the creature AI when this unit is a combat pet.
        /// </summary>
        public NexusForever.Game.Static.Pet.PetStance PetStance { get; set; } = NexusForever.Game.Static.Pet.PetStance.Assist;

        /// <summary>
        /// Returns if the unit is stealthed by at least one active stealth effect.
        /// </summary>
        public bool IsStealthed => stealthSources.Count > 0;

        /// <summary>
        /// Add a stealth source from the spell with the supplied casting id.
        /// </summary>
        public void AddStealth(uint castingId)
        {
            bool wasStealthed = IsStealthed;
            stealthSources.Add(castingId);
            if (!wasStealthed)
                OnStealthChange(false);
        }

        /// <summary>
        /// Remove the stealth source from the spell with the supplied casting id.
        /// </summary>
        public void RemoveStealth(uint castingId)
        {
            if (stealthSources.Remove(castingId) && !IsStealthed)
                OnStealthChange(true);
        }

        /// <summary>
        /// End every stealth effect on the unit, for example when it attacks.
        /// </summary>
        public void BreakStealth()
        {
            if (!IsStealthed)
                return;

            foreach (ISpell spell in GetSpellsByEffect(SpellEffectType.Stealth).ToList())
                spell.Finish();

            // stealth from spells cast by someone else (or already finished) still has to go
            if (IsStealthed)
            {
                stealthSources.Clear();
                OnStealthChange(true);
            }
        }

        private void OnStealthChange(bool exiting)
        {
            if (exiting)
                CreateFlags &= ~EntityCreateFlag.IsStealthed;
            else
                CreateFlags |= EntityCreateFlag.IsStealthed;

            EnqueueToVisible(new ServerCombatLog
            {
                CombatLog = new CombatLogStealth
                {
                    UnitId   = Guid,
                    BExiting = exiting
                }
            }, true);
        }

        /// <summary>
        /// Return active <see cref="ISpell"/>s cast by this unit that have the supplied unit as a target.
        /// </summary>
        public IEnumerable<ISpell> GetSpellsAffecting(IUnitEntity target)
        {
            foreach (ISpell spell in spells.Values)
                if (!spell.IsCasting && !spell.IsFinished && spell.GetTarget(target) != null)
                    yield return spell;
        }

        /// <summary>
        /// Return a collection of <see cref="ISpell"/> that are applying the supplied <see cref="SpellEffectType"/> to entity.
        /// </summary>
        public IEnumerable<ISpell> GetSpellsByEffect(SpellEffectType type)
        {
            foreach (ISpell spell in spells.Values)
            {
                ISpellTargetInfo target = spell.GetTarget(this);
                if (target == null)
                    continue;

                if (target.GetEffectsByType(type).Any())
                    yield return spell;
            }
        }

        /// <summary>
        /// Check if this <see cref="IUnitEntity"/> has a spell active with the provided <see cref="Spell4Entry"/> Id
        /// </summary>
        public bool HasSpell(uint spell4Id, out ISpell spell, bool isCasting = false)
        {
            spell = spells.Values.FirstOrDefault(i => i.IsCasting == isCasting && !i.IsFinished && i.Spell4Id == spell4Id);
            return spell != null;
        }

        /// <summary>
        /// Check if this <see cref="IUnitEntity"/> has a spell active with the provided <see cref="CastMethod"/>
        /// </summary>
        public bool HasSpell(CastMethod castMethod, out ISpell spell)
        {
            spell = spells.Values.FirstOrDefault(i => !i.IsCasting && !i.IsFinished && i.CastMethod == castMethod);
            return spell != null;
        }

        /// <summary>
        /// Check if this <see cref="IUnitEntity"/> has a spell active with the provided <see cref="Func"/> predicate.
        /// </summary>
        public bool HasSpell(Func<ISpell, bool> predicate, out ISpell spell)
        {
            spell = spells.Values.FirstOrDefault(predicate);
            return spell != null;
        }

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell id and <see cref="ISpellParameters"/>.
        /// </summary>
        public void CastSpell<T>(T spell4Id, ISpellParameters parameters) where T : Enum
        {
            CastSpell(spell4Id.As<T, uint>(), parameters);
        }

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell id and <see cref="ISpellParameters"/>.
        /// </summary>
        public void CastSpell(uint spell4Id, ISpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(spell4Id);
            if (spell4Entry == null)
                throw new ArgumentOutOfRangeException();

            CastSpell(spell4Entry.Spell4BaseIdBaseSpell, (byte)spell4Entry.TierIndex, parameters);
        }

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell base id, tier and <see cref="ISpellParameters"/>.
        /// </summary>
        public void CastSpell(uint spell4BaseId, byte tier, ISpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            ISpellBaseInfo spellBaseInfo = LegacyServiceProvider.Provider.GetService<ISpellInfoManager>().GetSpellBaseInfo(spell4BaseId);
            if (spellBaseInfo == null)
                throw new ArgumentOutOfRangeException();

            ISpellInfo spellInfo = spellBaseInfo.GetSpellInfo(tier);
            if (spellInfo == null)
                throw new ArgumentOutOfRangeException();

            parameters.SpellInfo = spellInfo;
            CastSpell(parameters);
        }

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied <see cref="ISpellParameters"/>.
        /// </summary>
        public void CastSpell(ISpellParameters parameters)
        {
            if (!IsAlive)
                return;

            if (parameters == null)
                throw new ArgumentNullException();

            if (DisableManager.Instance.IsDisabled(DisableType.BaseSpell, parameters.SpellInfo.BaseInfo.Entry.Id))
            {
                if (this is IPlayer player)
                    player.SendSystemMessage($"Unable to cast base spell {parameters.SpellInfo.BaseInfo.Entry.Id} because it is disabled.");
                return;
            }

            if (DisableManager.Instance.IsDisabled(DisableType.Spell, parameters.SpellInfo.Entry.Id))
            {
                if (this is IPlayer player)
                    player.SendSystemMessage($"Unable to cast spell {parameters.SpellInfo.Entry.Id} because it is disabled.");
                return;
            }

            if (parameters.UserInitiatedSpellCast)
            {
                if (this is IPlayer player)
                    player.Dismount();
            }

            CastMethod castMethod = (CastMethod)parameters.SpellInfo.BaseInfo.Entry.CastMethod;
            if (parameters.ClientSideInteraction != null)
                castMethod = CastMethod.ClientSideInteraction;

            ISpell spell = spellFactory.CreateSpell(castMethod);
            if (spell == null)
                throw new ArgumentNullException();

            spell.Initialise(this, parameters);
            if (!spell.Cast())
                return;

            // Don't store spell if it failed to initialise
            if (spell.IsFailed)
                return;

            pendingSpells.Add(spell);
        }

        /// <summary>
        /// Cancel any <see cref="ISpell"/>'s that are interrupted by movement.
        /// </summary>
        public void CancelSpellsOnMove()
        {
            foreach (ISpell spell in spells.Values)
                if (spell.IsMovingInterrupted() && spell.IsCasting)
                    spell.CancelCast(CastResult.CasterMovement);
        }

        /// <summary>
        /// Cancel an <see cref="ISpell"/> based on its casting id.
        /// </summary>
        /// <param name="castingId">Casting ID of the spell to cancel</param>
        public void CancelSpellCast(uint castingId)
        {
            ISpell spell = spells.Values.SingleOrDefault(s => s.CastingId == castingId);
            spell?.CancelCast(CastResult.SpellCancelled);
        }

        /// <summary>
        /// Returns an active <see cref="ISpell"/> that is affecting this <see cref="IUnitEntity"/>
        /// </summary>
        public ISpell GetActiveSpell(Func<ISpell, bool> func)
        {
            return spells.Values.FirstOrDefault(func);
        }

        /// <summary>
        /// Determine if this <see cref="IUnitEntity"/> can attack supplied <see cref="IUnitEntity"/>.
        /// </summary>
        public virtual bool CanAttack(IUnitEntity target)
        {
            if (!IsAlive)
                return false;

            if (!target.IsValidAttackTarget(this) || !IsValidAttackTarget(target))
                return false;

            return GetDispositionTo(target.Faction1) < Disposition.Friendly;
        }

        /// <summary>
        /// Returns whether or not this <see cref="IUnitEntity"/> is an attackable target for supplied <see cref="IUnitEntity"/>.
        /// </summary>
        public virtual bool IsValidAttackTarget(IUnitEntity attacker)
        {
            // TODO: Expand on this. There's bound to be flags or states that should prevent an entity from being attacked.
            return (this is IPlayer or INonPlayerEntity);
        }

        /// <summary>
        /// Deal damage to this <see cref="IUnitEntity"/> from the supplied <see cref="IUnitEntity"/>.
        /// </summary>
        private readonly List<(uint EffectId, uint Remaining, uint Mask)> absorbs = [];

        /// <summary>
        /// Add an absorb pool from a spell effect, damage of a type in the mask is taken from it before shield and health.
        /// </summary>
        public void AddAbsorb(uint effectId, uint amount, uint damageTypeMask)
        {
            RemoveAbsorb(effectId);
            if (amount > 0u)
                absorbs.Add((effectId, amount, damageTypeMask));
        }

        /// <summary>
        /// Remove the absorb pool added by supplied spell effect.
        /// </summary>
        public void RemoveAbsorb(uint effectId)
        {
            absorbs.RemoveAll(a => a.EffectId == effectId);
        }

        /// <summary>
        /// Take as much of supplied damage as the absorb pools allow, oldest first, and return the amount absorbed.
        /// </summary>
        public uint ConsumeAbsorb(DamageType damageType, uint damage)
        {
            uint bit      = damageType is >= DamageType.Physical and <= DamageType.Magic ? 1u << ((int)damageType - 1) : 0u;
            uint absorbed = 0u;
            for (int i = 0; i < absorbs.Count && damage > 0u; i++)
            {
                (uint effectId, uint remaining, uint mask) = absorbs[i];
                if (mask != 0u && (bit == 0u || (mask & bit) == 0u))
                    continue;

                uint take = Math.Min(remaining, damage);
                absorbed += take;
                damage   -= take;
                absorbs[i] = (effectId, remaining - take, mask);
            }

            absorbs.RemoveAll(a => a.Remaining == 0u);
            return absorbed;
        }

        public void TakeDamage(IUnitEntity attacker, IDamageDescription damageDescription)
        {
            if (!IsAlive || !attacker.IsAlive || attacker == this)
                return;

            // TODO: Calculate Threat properly
            ThreatManager.UpdateThreat(attacker, (int)damageDescription.RawDamage);

            Shield -= damageDescription.ShieldAbsorbAmount;
            ModifyHealth(damageDescription.AdjustedDamage, damageDescription.DamageType, attacker);

            if (attacker is IPlayer || this is IPlayer)
                combatLog.Debug($"{attacker.Guid} (creature {attacker.CreatureId}, level {attacker.Level}) hit {Guid} (creature {CreatureId}, level {Level}) for {damageDescription.AdjustedDamage} (raw {damageDescription.RawDamage}, shield {damageDescription.ShieldAbsorbAmount}, {damageDescription.CombatResult}), health now {Health}/{MaxHealth}.");
        }

        /// <summary>
        /// Modify the health of this <see cref="IUnitEntity"/> by the supplied amount.
        /// </summary>
        /// <remarks>
        /// If the <see cref="DamageType"/> is <see cref="DamageType.Heal"/> amount is added to current health otherwise subtracted.
        /// </remarks>
        public virtual void ModifyHealth(uint amount, DamageType? type, IUnitEntity source)
        {
            long newHealth = Health;
            if (type is DamageType.Heal or null)
                newHealth += amount;
            else
                newHealth -= amount;

            Health = (uint)Math.Clamp(newHealth, 0u, MaxHealth);

            scriptCollection?.Invoke<IUnitScript>(s => s.OnHealthChange(source, amount, type));

            if (Health == 0)
                OnDeath(source);
        }

        protected virtual void OnDeath(IUnitEntity killer)
        {
            DeathState = EntityDeathState.JustDied;

            foreach (ISpell spell in spells.Values)
            {
                if (spell.IsCasting)
                    spell.CancelCast(CastResult.CasterCannotBeDead);
            }

            GenerateRewards(killer);
            ScheduleRespawn();

            ThreatManager.ClearThreatList();
            MovementManager.Finalise();

            scriptCollection?.Invoke<IUnitScript>(s => s.OnDeath());

            deathState = EntityDeathState.Dead;
        }

        private UpdateTimer respawnTimer;

        /// <summary>
        /// Schedule a fresh copy of this creature to replace it at its spawn position, open world database spawns only.
        /// </summary>
        private void ScheduleRespawn()
        {
            TimeSpan? delay = GetRespawnDelay();
            if (delay.HasValue)
                respawnTimer = new UpdateTimer(delay.Value);
        }

        private TimeSpan? GetRespawnDelay()
        {
            if (this is IPlayer || SpawnModel == null || Map is IMapInstance)
                return null;

            RespawnConfig config = SharedConfiguration.Instance.Get<RealmConfig>()?.Respawn ?? new RespawnConfig();
            if (!config.Enable)
                return null;

            return TimeSpan.FromSeconds(config.DelaySeconds);
        }

        private IWorldEntity CreateRespawnEntity()
        {
            IWorldEntity entity = LegacyServiceProvider.Provider.GetService<IEntityFactory>().CreateWorldEntity((EntityType)SpawnModel.Type);
            entity.Initialise(CreatureInfo, SpawnModel);
            return entity;
        }

        private void Respawn()
        {
            IBaseMap map = Map;
            if (map == null || PendingRemoval)
                return;

            IWorldEntity entity = CreateRespawnEntity();
            RemoveFromMap();
            map.EnqueueAdd(entity, new Vector3(SpawnModel.X, SpawnModel.Y, SpawnModel.Z));
        }

        /// <summary>
        /// Remove this unit from the map without killing it, world database spawns come back after the respawn delay.
        /// </summary>
        public void Despawn()
        {
            IBaseMap map = Map;
            if (this is IPlayer || map == null || PendingRemoval)
                return;

            // the map owns the respawn from here, the pending corpse respawn (if any) goes with this entity
            TimeSpan? delay = GetRespawnDelay();
            if (delay.HasValue)
                map.EnqueueAdd(CreateRespawnEntity(), new Vector3(SpawnModel.X, SpawnModel.Y, SpawnModel.Z), delay.Value);

            respawnTimer = null;
            RemoveFromMap();
        }

        protected virtual void GenerateRewards(IUnitEntity killer)
        {
            foreach (uint targetGroupId in AssetManager.Instance.GetTargetGroupsForCreatureId(CreatureId))
                Map.PublicEventManager.UpdateObjective(PublicEventObjectiveType.KillTargetGroup, targetGroupId, 1);

            // reward public event kill stat to player that landed the killing blow
            if (killer is IPlayer killerPlayer)
                Map.PublicEventManager.UpdateStat(killerPlayer, PublicEventStat.Kills, 1);

            var rewarded = new HashSet<IPlayer>();
            foreach (IHostileEntity hostile in ThreatManager)
            {
                IUnitEntity entity = GetVisible<IUnitEntity>(hostile.HatedUnitId);
                if (entity is IPlayer hostilePlayer)
                    rewarded.Add(hostilePlayer);
                // a pet's kills count for its owner
                else if (entity?.SummonerGuid is uint summonerGuid && GetVisible<IUnitEntity>(summonerGuid) is IPlayer petOwner)
                    rewarded.Add(petOwner);
            }

            // group members close by share the kill (quest credit and XP)
            IGroupManager groupManager = LegacyServiceProvider.Provider.GetService<IGroupManager>();
            foreach (IPlayer player in rewarded.ToList())
            {
                if (player.GroupAssociation == 0ul || groupManager == null)
                    continue;

                IGroup group = groupManager.GetGroup(player.GroupAssociation);
                if (group == null)
                    continue;

                foreach (IGroupMember member in group)
                {
                    IPlayer memberPlayer = PlayerManager.Instance.GetPlayer(member.Identity);
                    if (memberPlayer != null && memberPlayer.Map == Map && memberPlayer.IsAlive
                        && Vector3.Distance(memberPlayer.Position, Position) <= GroupRewardRange)
                        rewarded.Add(memberPlayer);
                }
            }

            foreach (IPlayer player in rewarded)
                RewardKiller(player);
        }

        /// <summary>
        /// Group members within this distance of a kill share its credit.
        /// </summary>
        private const float GroupRewardRange = 100f;

        protected virtual void RewardKiller(IPlayer player)
        {
            player.QuestManager.ObjectiveUpdate(QuestObjectiveType.KillCreature, CreatureId, 1u);
            player.QuestManager.ObjectiveUpdate(QuestObjectiveType.KillCreature2, CreatureId, 1u);
            player.PathMissionManager.OnKill(CreatureId, AssetManager.Instance.GetTargetGroupsForCreatureId(CreatureId));
            player.ChallengeManager.OnKill(AssetManager.Instance.GetTargetGroupsForCreatureId(CreatureId));

            foreach (uint targetGroupId in AssetManager.Instance.GetTargetGroupsForCreatureId(CreatureId))
            {
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.KillTargetGroup, targetGroupId, 1u);
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.KillTargetGroups, targetGroupId, 1u);
                player.QuestManager.RewardPaneObjectiveUpdate(QuestObjectiveType.VirtualCollect, targetGroupId, 1u);
            }

            uint experience = KillExperience.Calculate(this, player);
            if (experience > 0u)
                player.XpManager.GrantXp(experience, ExpReason.KillCreature);

            // TODO: Reward Loot
            // TODO: Handle Achievements
        }

        /// <summary>
        /// Set target to supplied target guid.
        /// </summary>
        /// <remarks>
        /// A null target will clear the current target.
        /// </remarks>
        public void SetTarget(uint? target, uint threat = 0u)
        {
            SetTarget(target != null ? GetVisible<IWorldEntity>(target.Value) : null, threat);
        }

        /// <summary>
        /// Set target to supplied <see cref="IUnitEntity"/>.
        /// </summary>
        /// <remarks>
        /// A null target will clear the current target.
        /// </remarks>
        public virtual void SetTarget(IWorldEntity target, uint threat = 0u)
        {
            // notify current target they are no longer the target
            if (TargetGuid != null)
                GetVisible<IWorldEntity>(TargetGuid.Value)?.OnUntargeted(this);

            target?.OnTargeted(this);

            EnqueueToVisible(new ServerEntityTargetUnit
            {
                UnitId      = Guid,
                NewTargetId = target?.Guid ?? 0u,
                ThreatLevel = threat
            });

            TargetGuid = target?.Guid;
        }

        /// <summary>
        /// Invoked when a new <see cref="IHostileEntity"/> is added to the threat list.
        /// </summary>
        public virtual void OnThreatAddTarget(IHostileEntity hostile)
        {
            UpdateCombatState();
            scriptCollection?.Invoke<IUnitScript>(s => s.OnThreatAddTarget(hostile));
        }

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is removed from the threat list.
        /// </summary>
        public virtual void OnThreatRemoveTarget(IHostileEntity hostile)
        {
            UpdateCombatState();
            scriptCollection?.Invoke<IUnitScript>(s => s.OnThreatRemoveTarget(hostile));
        }

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is update on the threat list.
        /// </summary>
        public virtual void OnThreatChange(IHostileEntity hostile)
        {
            scriptCollection?.Invoke<IUnitScript>(s => s.OnThreatChange(hostile));
        }

        private void UpdateCombatState()
        {
            // ensure conditions for combat state change are met
            if (ThreatManager.IsThreatened == InCombat)
                return;

            bool previousCombatState = InCombat;
            InCombat = ThreatManager.IsThreatened;

            if (!previousCombatState && InCombat)
                scriptCollection?.Invoke<IUnitScript>(s => s.OnEnterCombat());
            else if (previousCombatState && !InCombat)
                scriptCollection?.Invoke<IUnitScript>(s => s.OnLeaveCombat());

            Sheathed = !InCombat;
            SetStandState(InCombat ? StandState.Stand : StandState.State0);

            statUpdateManager.OnCombatStateUpdate(InCombat);
        }

        /// <summary>
        /// Invoked when <see cref="IWorldEntity"/> has a <see cref="Stat"/> updated.
        /// </summary>
        protected override void OnStatUpdate(IStatValue statValue, float previousValue)
        {
            statUpdateManager.OnStatUpdate(statValue, previousValue);
        }
    }
}
