using System.Numerics;
using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Entity.Movement.Command;
using NexusForever.Game.Abstract.Entity.Movement.Command.Position;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Pet;
using NexusForever.Game.Static.Entity.Movement.Spline;
using NexusForever.Game.Static.Reputation;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter.Dynamic;
using NexusForever.Shared;
using NexusForever.Shared.Game;

namespace NexusForever.Script.Main.AI
{
    //[ScriptFilterIgnore]
    [ScriptFilterDynamic<IScriptFilterDynamicCombatAI>]
    public class CombatAI : IUnitScript, IOwnedScript<ICreatureEntity>
    {
        protected ICreatureEntity entity;

        private int autoAttackIndex = 0;
        protected List<uint> autoAttacks = [5649, 5652];
        private readonly UpdateTimer autoAttackTimer = new(TimeSpan.FromSeconds(1.5d));

        private float chaseDistance = 5f;
        private readonly UpdateTimer chaseDistanceTimer = new(TimeSpan.FromSeconds(1f));

        // chasing further than this from where combat pulled it from makes the creature give up and walk home, retail range unknown
        private const float LeashRange = 50f;
        // while walking home the creature ignores players coming into range, until it arrives or this runs out
        private static readonly TimeSpan EvadeTime = TimeSpan.FromSeconds(10d);
        private DateTime? evadingUntil;
        // where combat started, patrolling creatures can be pulled far from their spawn
        private Vector3? pullPosition;

        #region Dependency Injection

        private readonly IFactory<ISpellParameters> spellParametersFactory;
        private readonly IGameTableManager gameTableManager;
        private readonly ICreatureSpellCatalog creatureSpellCatalog;

        public CombatAI(
            IFactory<ISpellParameters> spellParametersFactory,
            IGameTableManager gameTableManager,
            ICreatureSpellCatalog creatureSpellCatalog = null)
        {
            this.spellParametersFactory = spellParametersFactory;
            this.gameTableManager       = gameTableManager;
            this.creatureSpellCatalog   = creatureSpellCatalog;
        }

        #endregion

        /// <summary>
        /// Invoked when <see cref="IScript"/> is loaded.
        /// </summary>
        public virtual void OnLoad(ICreatureEntity owner)
        {
            entity = owner;
            entity.SetInRangeCheck(15f);

            // spells this creature was seen using on retail replace the generic punch and jab
            IReadOnlyList<uint> observed = creatureSpellCatalog?.GetAutoAttacks(entity.CreatureId) ?? [];
            if (observed.Count > 0)
                autoAttacks = observed.ToList();

            specials = creatureSpellCatalog?.GetSpecials(entity.CreatureId).ToList() ?? [];
        }

        /// <summary>
        /// Invoked each world tick with the delta since the previous tick occurred.
        /// </summary>
        public virtual void Update(double lastTick)
        {
            if (!entity.IsAlive)
                return;

            IPlayer petOwner = GetPetOwner();
            if (petOwner != null)
                UpdatePet(petOwner, lastTick);

            if (watchedInRange.Count > 0)
                UpdateWatched(lastTick);

            if (evadingUntil.HasValue && (DateTime.UtcNow >= evadingUntil || entity.Position.GetDistance(entity.LeashPosition) < 2f))
                evadingUntil = null;

            if (!entity.TargetGuid.HasValue)
                return;

            if (entity.IsCasting())
                return;

            UpdateAI(lastTick);
        }

        protected virtual void UpdateAI(double lastTick)
        {
            autoAttackTimer.Update(lastTick);
            if (autoAttackTimer.HasElapsed)
            {
                DoAutoAttack();
                autoAttackTimer.Reset();
            }

            chaseDistanceTimer.Update(lastTick);
            if (chaseDistanceTimer.HasElapsed)
            {
                DoChase();
                chaseDistanceTimer.Reset();
            }

            if (specials.Count > 0)
            {
                specialTimer.Update(lastTick);
                if (specialTimer.HasElapsed)
                {
                    DoSpecial();
                    specialTimer = new UpdateTimer(TimeSpan.FromSeconds(8d + Random.Shared.NextDouble() * 6d));
                }
            }
        }

        private List<uint> specials = [];
        private UpdateTimer specialTimer = new(TimeSpan.FromSeconds(6d));

        /// <summary>
        /// Cast one of the creature's observed cast-time spells (telegraphed attacks such as a yeti's Claw Slam) at its target when in range.
        /// </summary>
        private void DoSpecial()
        {
            if (!entity.TargetGuid.HasValue)
                return;

            IUnitEntity target = entity.Map.GetEntity<IUnitEntity>(entity.TargetGuid.Value);
            if (target == null)
                return;

            uint spell4Id = specials[Random.Shared.Next(specials.Count)];
            Spell4Entry spell4Entry = gameTableManager.Spell4.GetEntry(spell4Id);
            if (spell4Entry == null)
                return;

            float range = spell4Entry.TargetMaxRange > 0f ? spell4Entry.TargetMaxRange : 5f;
            if (entity.Position.GetDistance(target.Position) > range + 1f)
                return;

            var parameters = spellParametersFactory.Resolve();
            parameters.PrimaryTargetId = target.Guid;
            entity.MovementManager.SetRotationFaceUnit(target.Guid);
            entity.CastSpell(spell4Id, parameters);
        }

        private readonly UpdateTimer petTimer = new(TimeSpan.FromSeconds(1d));

        private readonly HashSet<uint> watchedInRange = [];
        private readonly UpdateTimer stealthTimer = new(TimeSpan.FromSeconds(1d));

        /// <summary>
        /// Aggro on units that came into range but did not pull straight away: stealthed, further than the level based aggro range,
        /// or passed while the creature walked home, once that no longer holds while they are still in range.
        /// </summary>
        private void UpdateWatched(double lastTick)
        {
            stealthTimer.Update(lastTick);
            if (!stealthTimer.HasElapsed)
                return;

            stealthTimer.Reset();
            foreach (uint guid in watchedInRange.ToList())
            {
                IUnitEntity unit = entity.GetVisible<IUnitEntity>(guid);
                if (unit == null || !unit.IsAlive || entity.Position.GetDistance(unit.Position) > 15f || entity.InCombat)
                {
                    watchedInRange.Remove(guid);
                    continue;
                }

                if (unit.IsStealthed || evadingUntil.HasValue || entity.Position.GetDistance(unit.Position) > AggroRange(unit))
                    continue;

                watchedInRange.Remove(guid);
                AggroEntity(unit);
            }
        }

        /// <summary>
        /// Proximity aggro range against a unit: 15 m, 2 m less per level the player is above the creature down to 5 m,
        /// players 8 or more levels above are ignored. Retail used a level based range, the numbers here are invented.
        /// </summary>
        private float AggroRange(IUnitEntity unit)
        {
            if (unit is not IPlayer)
                return 15f;

            int diff = (int)unit.Level - (int)entity.Level;
            if (diff >= 8)
                return 0f;

            return Math.Clamp(15f - 2f * diff, 5f, 15f);
        }

        /// <summary>
        /// Return the player that summoned this creature as a pet, null for anything else.
        /// </summary>
        protected IPlayer GetPetOwner()
        {
            if (entity.SummonerGuid is not uint summonerGuid)
                return null;

            return entity.Map?.GetEntity<IPlayer>(summonerGuid);
        }

        /// <summary>
        /// Pets assist their owner's target and follow the owner when they have nothing to fight.
        /// </summary>
        private void UpdatePet(IPlayer owner, double lastTick)
        {
            petTimer.Update(lastTick);
            if (!petTimer.HasElapsed)
                return;

            petTimer.Reset();

            if (entity.PetStance == PetStance.Defensive)
                DefendOwner(owner);
            else if (entity.PetStance != PetStance.Passive && owner.InCombat && owner.TargetGuid is uint ownerTargetGuid && ownerTargetGuid != entity.TargetGuid)
            {
                IUnitEntity target = entity.Map.GetEntity<IUnitEntity>(ownerTargetGuid);
                if (target != null && target.IsAlive && entity.CanAttack(target))
                    entity.ThreatManager.UpdateThreat(target, 100);
            }

            if (entity.PetStance != PetStance.Stay && !entity.TargetGuid.HasValue && entity.Position.GetDistance(owner.Position) > 6f)
                entity.MovementManager.Chase(owner, 3f);
        }

        /// <summary>
        /// Defensive stance: when idle, fight whatever is in combat with the owner, preferring units that are attacking the owner,
        /// instead of following the owner's target. Being hit pulls the pet in any stance but Passive through the normal damage aggro.
        /// </summary>
        private void DefendOwner(IPlayer owner)
        {
            if (entity.TargetGuid.HasValue || !owner.InCombat)
                return;

            IUnitEntity target = owner.ThreatManager
                .Select(h => entity.Map.GetEntity<IUnitEntity>(h.HatedUnitId))
                .Where(u => u != null && u.IsAlive && entity.CanAttack(u))
                .OrderByDescending(u => u.TargetGuid == owner.Guid)
                .FirstOrDefault();

            if (target != null)
                entity.ThreatManager.UpdateThreat(target, 100);
        }

        private void DoAutoAttack()
        {
            if (autoAttacks.Count == 0)
                return;

            if (!entity.TargetGuid.HasValue)
                return;

            IUnitEntity target = entity.Map.GetEntity<IUnitEntity>(entity.TargetGuid.Value);
            if (target == null)
                return;

            uint spell4Id = autoAttacks[autoAttackIndex];
            autoAttackIndex = (autoAttackIndex + 1) % autoAttacks.Count;

            Spell4Entry spell4Entry = gameTableManager.Spell4.GetEntry(spell4Id);
            if (spell4Entry == null)
                return;

            chaseDistance = Math.Min(chaseDistance, spell4Entry.TargetMaxRange);

            if (entity.Position.GetDistance(target.Position) > spell4Entry.TargetMaxRange)
                return;

            ISpellParameters spellParameters = spellParametersFactory.Resolve();
            spellParameters.PrimaryTargetId = entity.TargetGuid.Value;
            entity.CastSpell(spell4Id, spellParameters);
        }

        private void DoChase()
        {
            if (!entity.TargetGuid.HasValue)
                return;

            IUnitEntity target = entity.Map.GetEntity<IUnitEntity>(entity.TargetGuid.Value);
            if (target == null)
                return;

            // pets follow their owner anywhere, everything else gives up past the leash range (scripted bosses that skip OnEnterCombat never leash)
            if (GetPetOwner() == null && pullPosition.HasValue && entity.Position.GetDistance(pullPosition.Value) > LeashRange)
            {
                evadingUntil = DateTime.UtcNow + EvadeTime;
                entity.ThreatManager.ClearThreatList();
                return;
            }

            if (entity.Position.GetDistance(target.Position) < chaseDistance)
                return;
            
            entity.MovementManager.Chase(target, chaseDistance / 2);
        }

        /// <summary>
        /// Invoked when <see cref="IPositionCommand"/> is finalised.
        /// </summary>
        public void OnEntityCommandFinalise(IEntityCommand command)
        {
            if (command is not IPositionCommand)
                return;

            if (!entity.TargetGuid.HasValue)
                return;

            entity.MovementManager.SetRotationFaceUnit(entity.TargetGuid.Value);
        }

        /// <summary>
        /// Invoked when <see cref="IGridEntity"/> is added to range check range.
        /// </summary>
        public void OnEnterRange(IGridEntity entity)
        {
            // stealthed units walk past, damage still pulls through OnHealthChange; they are re-checked in case stealth ends nearby
            if (entity is IUnitEntity { IsStealthed: true })
            {
                watchedInRange.Add(entity.Guid);
                return;
            }

            // pets only pull on their own in the Aggressive stance, otherwise they fight what their owner fights
            if (GetPetOwner() != null && this.entity.PetStance != PetStance.Aggressive)
                return;

            // walking home after leashing, or the unit is further than the level based range: watch it and pull later if that changes
            if (entity is IUnitEntity unit && (evadingUntil.HasValue || this.entity.Position.GetDistance(unit.Position) > AggroRange(unit)))
            {
                watchedInRange.Add(entity.Guid);
                return;
            }

            AggroEntity(entity);
        }

        /// <summary>
        /// Invoked when <see cref="IGridEntity"/> is removed from vision range.
        /// </summary>
        public void OnRemoveVisibleEntity(IGridEntity entity)
        {
            if (entity.Guid == this.entity.TargetGuid)
                SelectTarget();
        }

        /// <summary>
        /// Invoked when health is changed by source <see cref="IUnitEntity"/>.
        /// </summary>
        public virtual void OnHealthChange(IUnitEntity source, uint amount, DamageType? type)
        {
            if (type is DamageType.Heal or null)
                return;

            AggroEntity(source);
        }

        private void AggroEntity(IGridEntity source)
        {
            if (!entity.IsAlive)
                return;

            if (entity.InCombat)
                return;

            if (source is not IUnitEntity unit)
                return;

            // units without a faction can't be evaluated for disposition
            if (unit.Faction1 == 0)
                return;

            if (entity.GetDispositionTo(unit.Faction1) != Disposition.Hostile)
                return;

            var spellParameters = spellParametersFactory.Resolve();
            entity.CastSpell(41368, spellParameters);

            entity.MovementManager.Finalise();
            entity.MovementManager.SetRotationFaceUnit(unit.Guid);

            entity.ThreatManager.UpdateThreat(unit, 1);
        }

        /// <summary>
        /// Invoked when <see cref="IUnitEntity"/> enters combat.
        /// </summary>
        public virtual void OnEnterCombat()
        {
            pullPosition = entity.Position;
            CallForHelp();
        }

        // creatures pulled in by a call for help do not call for help themselves, so a call reaches one ring of neighbours
        [ThreadStatic]
        private static bool answeringCall;

        /// <summary>
        /// Idle creatures of the same faction within 10 m join the fight against the same target. Retail had social aggro,
        /// the range and rules here are invented.
        /// </summary>
        private void CallForHelp()
        {
            if (answeringCall || GetPetOwner() != null || entity.Faction1 == 0)
                return;

            IHostileEntity hostile = entity.ThreatManager.GetTopHostile();
            IUnitEntity target = hostile != null ? entity.GetVisible<IUnitEntity>(hostile.HatedUnitId) : null;
            if (target == null)
                return;

            answeringCall = true;
            try
            {
                foreach (ICreatureEntity ally in entity.GetVisible().OfType<ICreatureEntity>().ToList())
                {
                    if (ally == entity || !ally.IsAlive || ally.InCombat || ally.SummonerGuid.HasValue || ally.Faction1 != entity.Faction1)
                        continue;
                    if (ally.Position.GetDistance(entity.Position) > 10f)
                        continue;
                    if (ally.GetDispositionTo(target.Faction1) != Disposition.Hostile)
                        continue;

                    ally.ThreatManager.UpdateThreat(target, 1);
                }
            }
            finally
            {
                answeringCall = false;
            }
        }

        /// <summary>
        /// Invoked when <see cref="IUnitEntity"/> leaves combat.
        /// </summary>
        public virtual void OnLeaveCombat()
        {
            pullPosition = null;
        }

        /// <summary>
        /// Invoked when <see cref="IUnitEntity"/> is killed.
        /// </summary>
        public virtual void OnDeath()
        {
            // deliberately empty
        }

        /// <summary>
        /// Invoked when a new <see cref="IHostileEntity"/> is added to the threat list.
        /// </summary>
        public void OnThreatAddTarget(IHostileEntity hostile)
        {
            SelectTarget();
        }

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is removed from the threat list.
        /// </summary>
        public void OnThreatRemoveTarget(IHostileEntity hostile)
        {
            SelectTarget();
        }

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is update on the threat list.
        /// </summary>
        public void OnThreatChange(IHostileEntity hostile)
        {
            SelectTarget();
        }

        protected virtual void SelectTarget()
        {
            IHostileEntity hostile = entity.ThreatManager.GetTopHostile();
            if (hostile == null)
            {
                Reset();
                return;
            }

            if (entity.TargetGuid == hostile.HatedUnitId)
                return;

            entity.SetTarget(hostile.HatedUnitId, hostile.Threat);
        }

        private void Reset()
        {
            entity.SetTarget((IWorldEntity)null);

            // a dead creature keeps its corpse where it fell, healing and leashing only apply to the living
            if (!entity.IsAlive)
                return;

            // pets go back to following their owner instead of healing and leashing
            if (GetPetOwner() != null)
                return;

            entity.ModifyHealth(entity.MaxHealth, DamageType.Heal, null);

            // TODO: cancel all pending spells

            float speed = entity.GetPropertyValue(Property.MoveSpeedMultiplier) * 10f;
            entity.MovementManager.LaunchSpline([entity.Position, entity.LeashPosition], SplineType.Linear, SplineMode.OneShot, speed);
        }
    }
}
