using System.Numerics;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Static.Entity;

namespace NexusForever.Game.Abstract.Entity
{
    public interface IEntitySummonFactory
    {
        public uint SummonCount { get; }

        /// <summary>
        /// Initialises the factory with the owner of the summons.
        /// </summary>
        void Initialise(IWorldEntity owner);

        /// <summary>
        /// Summons an entity of <typeparamref name="T"/> at the specified position and rotation and optional callback.
        /// </summary>
        T Summon<T>(ICreatureInfo creatureInfo, Vector3 position, Vector3 rotation, OnAddDelegate add = null) where T : IWorldEntity;

        /// <summary>
        /// Summons an entity at the specified position and rotation and optional callback.
        /// </summary>
        IWorldEntity Summon(ICreatureInfo creatureInfo, Vector3 position, Vector3 rotation, OnAddDelegate add = null);

        /// <summary>
        /// Summons an entity of <see cref="EntityType"/> at the specified position and rotation and optional callback.
        /// </summary>
        IWorldEntity Summon(ICreatureInfo creatureInfo, EntityType entityType, Vector3 position, Vector3 rotation, OnAddDelegate add = null);

        /// <summary>
        /// Start tracking a summon.
        /// </summary>
        /// <remarks>
        /// This should be called when a summon is added to the world.
        /// </remarks>
        void TrackSummon(IWorldEntity entity);

        /// <summary>
        /// Stop tracking a summon.
        /// </summary>
        /// <remarks>
        /// This should be called when a summon is removed from the world.
        /// </remarks>
        void UntrackSummon(IWorldEntity entity);

        /// <summary>
        /// Unsummon an summoned entity with supplied guid.
        /// </summary>
        void Unsummon(uint guid);

        /// <summary>
        /// Unsummon all summoned entities of creature id.
        /// </summary>
        void UnsummonCreature<T>(T creatureId) where T : Enum;

        /// <summary>
        /// Unsummon all summoned entities of creature id.
        /// </summary>
        void UnsummonCreature(uint creatureId);

        /// <summary>
        /// Return summoned entities of creature id that are still on the map.
        /// </summary>
        IEnumerable<IWorldEntity> GetSummons(uint creatureId);

        /// <summary>
        /// Unsummon all summoned entities.
        /// </summary>
        void Unsummon();
    }
}
