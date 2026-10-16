using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Pet;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.Network.World.Message.Model.Pet;
using NexusForever.Shared;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Summons a combat pet for the caster (Engineer bots, Esper Geist and Phantoms, Settler reinforcements and turrets).
    /// </summary>
    /// <remarks>
    /// DataBits00 is the Creature2 id. The pet takes its owner's factions, follows and assists through the creature AI,
    /// and player owners get the pet frame. Timed effects unsummon the pet when they end, permanent ones (bots) stay
    /// until the owner unsummons or leaves.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.SummonPet)]
    public class SpellEffectSummonPetHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>, ISpellEffectRemoveHandler<ISpellEffectDefaultData>
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<(uint Caster, uint CastingId), uint> summoned = new();

        #region Dependency Injection

        private readonly ICreatureInfoManager creatureInfoManager;

        public SpellEffectSummonPetHandler(
            ICreatureInfoManager creatureInfoManager)
        {
            this.creatureInfoManager = creatureInfoManager;
        }

        #endregion

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            IUnitEntity caster = executionContext.Spell.Caster;
            if (caster?.Map == null)
                return SpellEffectExecutionResult.PreventEffect;

            ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(data.DataBits00);
            if (creatureInfo == null)
                return SpellEffectExecutionResult.PreventEffect;

            // recasting a bot replaces it instead of stacking copies
            caster.SummonFactory.UnsummonCreature(data.DataBits00);

            uint spell4Id = executionContext.Spell.Parameters.SpellInfo.Entry.Id;
            IWorldEntity pet = caster.SummonFactory.Summon(creatureInfo, caster.Position.GetPoint2D(caster.Rotation.X + MathF.PI * 0.75f, 2.5f), caster.Rotation,
                (map, guid, vector) =>
                {
                    summoned[(caster.Guid, executionContext.Spell.CastingId)] = guid;

                    if (caster is not IPlayer player)
                        return;

                    player.AddCombatPet(guid);
                    player.Session.EnqueueMessageEncrypted(new ServerPetSpawned
                        {
                            PetUnitId         = guid,
                            SummoningSpell4Id = spell4Id,
                            ValidStances      = 0x1F,
                            Stance            = (uint)PetStance.Assist
                        });
                });

            if (pet != null)
            {
                pet.Faction1 = caster.Faction1;
                pet.Faction2 = caster.Faction2;
            }

            return SpellEffectExecutionResult.Ok;
        }

        public void Remove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            // only timed pets go away with the effect
            if (info.Entry.DurationTime == 0u)
                return;

            // a later cast may have replaced this pet already, only remove the one this cast made
            if (spell.Caster != null && summoned.TryRemove((spell.Caster.Guid, spell.CastingId), out uint guid))
                spell.Caster.SummonFactory.Unsummon(guid);
        }
    }
}
