using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Static.Entity;
using NexusForever.Shared;

namespace NexusForever.Game.Entity
{
    public class EntitySummonFactory : IEntitySummonFactory
    {
        public uint SummonCount => (uint)summonGuids.Count;

        private IWorldEntity owner;

        private readonly HashSet<uint> summonGuids = [];
        private readonly Dictionary<uint, List<uint>> creatureGuids = [];

        #region Dependency Injection

        private readonly IEntityFactory entityFactory;

        public EntitySummonFactory(
            IEntityFactory entityFactory)
        {
            this.entityFactory = entityFactory;
        }

        #endregion

        /// <summary>
        /// Initialises the factory with the owner of the summons.
        /// </summary>
        public void Initialise(IWorldEntity owner)
        {
            if (this.owner != null)
                throw new InvalidOperationException();

            this.owner = owner;
        }

        /// <summary>
        /// Summons an entity of <typeparamref name="T"/> at the specified position and rotation and optional callback.
        /// </summary>
        public T Summon<T>(ICreatureInfo creatureInfo, Vector3 position, Vector3 rotation, OnAddDelegate add = null) where T : IWorldEntity
        {
            T entity = entityFactory.CreateEntity<T>();
            Summon(creatureInfo, entity, position, rotation, add);
            return entity;
        }

        /// <summary>
        /// Summons an entity at the specified position and rotation and optional callback.
        /// </summary>
        public IWorldEntity Summon(ICreatureInfo creatureInfo, Vector3 position, Vector3 rotation, OnAddDelegate add = null)
        {
            IWorldEntity entity = entityFactory.CreateWorldEntity(creatureInfo.Entry.CreationTypeEnum);
            Summon(creatureInfo, entity, position, rotation, add);
            return entity;
        }

        /// <summary>
        /// Summons an entity of <see cref="EntityType"/> at the specified position and rotation and optional callback.
        /// </summary>
        public IWorldEntity Summon(ICreatureInfo creatureInfo, EntityType entityType, Vector3 position, Vector3 rotation, OnAddDelegate add = null)
        {
            IWorldEntity entity = entityFactory.CreateWorldEntity(entityType);
            Summon(creatureInfo, entity, position, rotation, add);
            return entity;
        }

        private void Summon(ICreatureInfo creatureInfo, IWorldEntity entity, Vector3 position, Vector3 rotation, OnAddDelegate add = null)
        {
            entity.Initialise(creatureInfo);
            entity.Rotation = rotation;
            entity.SummonerGuid = owner.Guid;
            entity.AddToMap(owner.Map, position, add);
        }

        /// <summary>
        /// Start tracking a summon.
        /// </summary>
        /// <remarks>
        /// This should be called when a summon is added to the world.
        /// </remarks>
        public void TrackSummon(IWorldEntity entity)
        {
            if (entity.SummonerGuid != owner.Guid)
                throw new ArgumentException();

            summonGuids.Add(entity.Guid);

            if (entity.CreatureId != 0)
            {
                if (!creatureGuids.TryGetValue(entity.CreatureId, out List<uint> guids))
                {
                    guids = [];
                    creatureGuids.Add(entity.CreatureId, guids);
                }

                guids.Add(entity.Guid);
            }
        }

        /// <summary>
        /// Stop tracking a summon.
        /// </summary>
        /// <remarks>
        /// This should be called when a summon is removed from the world.
        /// </remarks>
        public void UntrackSummon(IWorldEntity entity)
        {
            if (entity.SummonerGuid != owner.Guid)
                throw new ArgumentException();

            summonGuids.Remove(entity.Guid);

            if (creatureGuids.TryGetValue(entity.CreatureId, out List<uint> guids))
                guids.Remove(entity.Guid);
        }

        /// <summary>
        /// Unsummon an summoned entity with supplied guid.
        /// </summary>
        public void Unsummon(uint guid)
        {
            if (!summonGuids.Contains(guid))
                return;

            var summon = owner.Map.GetEntity<IWorldEntity>(guid);
            if (summon == null)
                return;

            UntrackSummon(summon);

            summon.RemoveFromMap();
        }

        /// <summary>
        /// Unsummon all summoned entities of creature id.
        /// </summary>
        public void UnsummonCreature<T>(T creatureId) where T : Enum
        {
            UnsummonCreature(creatureId.As<T, uint>());
        }

        /// <summary>
        /// Unsummon all summoned entities of creature id.
        /// </summary>
        public void UnsummonCreature(uint creatureId)
        {
            if (!creatureGuids.TryGetValue(creatureId, out List<uint> guids))
                return;

            foreach (uint guid in guids.ToList())
                Unsummon(guid);

            creatureGuids.Remove(creatureId);
        }

        /// <summary>
        /// Return summoned entities of creature id that are still on the map.
        /// </summary>
        public IEnumerable<IWorldEntity> GetSummons(uint creatureId)
        {
            if (!creatureGuids.TryGetValue(creatureId, out List<uint> guids))
                yield break;

            foreach (uint guid in guids.ToList())
            {
                IWorldEntity summon = owner.Map?.GetEntity<IWorldEntity>(guid);
                if (summon != null)
                    yield return summon;
            }
        }

        /// <summary>
        /// Unsummon all summoned entities.
        /// </summary>
        public void Unsummon()
        {
            foreach (uint guid in summonGuids)
                Unsummon(guid);

            summonGuids.Clear();
        }
    }
}
