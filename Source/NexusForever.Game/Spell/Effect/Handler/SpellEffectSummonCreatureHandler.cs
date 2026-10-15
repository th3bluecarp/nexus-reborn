using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.Shared;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Summons creatures around the effect target, owned by the caster.
    /// </summary>
    /// <remarks>
    /// DataBits00 is the Creature2 id, DataBits02 the count, DataBits03 and DataBits04 the minimum and maximum distance
    /// and DataBits05 the angle in degrees relative to the target facing. Timed effects unsummon the creatures when they end.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.SummonCreature)]
    public class SpellEffectSummonCreatureHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        private const uint MaxSummonsPerEffect = 20u;

        #region Dependency Injection

        private readonly ICreatureInfoManager creatureInfoManager;

        public SpellEffectSummonCreatureHandler(
            ICreatureInfoManager creatureInfoManager)
        {
            this.creatureInfoManager = creatureInfoManager;
        }

        #endregion

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            IUnitEntity caster = executionContext.Spell.Caster;
            if (caster?.Map == null)
                return SpellEffectExecutionResult.Ok;

            ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(data.DataBits00);
            if (creatureInfo == null)
                return SpellEffectExecutionResult.Ok;

            uint  count       = Math.Clamp(data.DataBits02, 1u, MaxSummonsPerEffect);
            float minDistance = Math.Min(data.DataBits03, data.DataBits04);
            float maxDistance = Math.Max(data.DataBits03, data.DataBits04);
            float angle       = target.Rotation.X + data.DataBits05 * MathF.PI / 180f;

            for (uint i = 0u; i < count; i++)
            {
                float distance = minDistance + (float)Random.Shared.NextDouble() * (maxDistance - minDistance);
                Vector3 position = distance > 0f ? target.Position.GetPoint2D(angle, distance) : target.Position;
                caster.SummonFactory.Summon(creatureInfo, position, target.Rotation);
            }

            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            // only timed summons go away with the effect, permanent ones (no duration) stay until despawned
            if (info.Entry.DurationTime == 0u)
                return;

            spell.Caster?.SummonFactory.UnsummonCreature(data.DataBits00);
        }
    }
}
