using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Map.Search;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Places a trap at the target that fires a spell on the first unit to come close (Stalker mines, Medic probes and stations).
    /// </summary>
    /// <remarks>
    /// DataBits00 is the trap Creature2 id, DataBits01 the Spell4 it fires, DataBits02 the arming delay in milliseconds,
    /// DataBits03 1 when it triggers on allies instead of enemies, DataBits04 the trigger radius (float). The effect duration
    /// is the trap lifetime.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.SummonTrap)]
    public class SpellEffectSummonTrapHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        private const double CheckIntervalMs = 250d;

        #region Dependency Injection

        private readonly ICreatureInfoManager creatureInfoManager;

        public SpellEffectSummonTrapHandler(
            ICreatureInfoManager creatureInfoManager)
        {
            this.creatureInfoManager = creatureInfoManager;
        }

        #endregion

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            IUnitEntity caster = executionContext.Spell.Caster;
            if (caster?.Map == null || data.DataBits01 == 0u)
                return SpellEffectExecutionResult.PreventEffect;

            ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(data.DataBits00);
            if (creatureInfo == null)
                return SpellEffectExecutionResult.PreventEffect;

            IWorldEntity trap = caster.SummonFactory.Summon(creatureInfo, target.Position, caster.Rotation);
            if (trap == null)
                return SpellEffectExecutionResult.PreventEffect;

            trap.Faction1 = caster.Faction1;
            trap.Faction2 = caster.Faction2;

            uint   spell4Id   = data.DataBits01;
            double armMs      = data.DataBits02;
            bool   onAllies   = data.DataBits03 == 1u;
            float  radius     = BitConverter.UInt32BitsToSingle(data.DataBits04);
            if (!float.IsFinite(radius) || radius <= 0f || radius > 30f)
                radius = 3f;
            double lifetimeMs = info.Entry.DurationTime > 0u ? info.Entry.DurationTime : 60000d;
            double elapsedMs  = 0d;

            caster.Map.AddTimer(TimeSpan.FromMilliseconds(CheckIntervalMs), () =>
            {
                elapsedMs += CheckIntervalMs;

                // trap or owner gone (unsummoned, owner logged out or changed map)
                if (trap.Map == null || caster.Map != trap.Map)
                    return false;

                if (elapsedMs >= lifetimeMs)
                {
                    caster.SummonFactory.Unsummon(trap.Guid);
                    return false;
                }

                if (elapsedMs < armMs)
                    return true;

                var check = new SearchCheckRange<IUnitEntity>();
                check.Initialise(trap.Position, radius);

                IUnitEntity victim = trap.Map.Search(trap.Position, radius, check)
                    .FirstOrDefault(u => u.IsAlive
                        && u.Guid != trap.Guid
                        && u.SummonerGuid != caster.Guid
                        && !u.IsStealthed
                        && (onAllies ? !caster.CanAttack(u) : caster.CanAttack(u)));
                if (victim == null)
                    return true;

                // fire from the trap when it can cast, otherwise from its owner
                IUnitEntity source = trap as IUnitEntity ?? caster;
                source.CastSpell(spell4Id, new SpellParameters
                {
                    PrimaryTargetId        = victim.Guid,
                    UserInitiatedSpellCast = false,
                    IsProxy                = true
                });

                caster.SummonFactory.Unsummon(trap.Guid);
                return false;
            });

            return SpellEffectExecutionResult.Ok;
        }
    }
}
