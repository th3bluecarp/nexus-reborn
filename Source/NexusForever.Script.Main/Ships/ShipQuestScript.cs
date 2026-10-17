using System.Numerics;
using System.Runtime.CompilerServices;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Script.Template;

namespace NexusForever.Script.Main.Ships
{
    /// <summary>
    /// A quest that takes the player aboard a ship whose interior is its own map with nothing placed in the world data.
    /// </summary>
    /// <remarks>
    /// Stand in for the lost retail scripts. Reaching the ship's spot in the open world moves the player inside, the quest's creatures
    /// and objects are placed at its own map markers, and once the last objective is done the player is returned to the ship's spot.
    /// </remarks>
    public abstract class ShipQuestScript : IQuestScript, IOwnedScript<IQuest>
    {
        private const float BoardRadius = 20f;

        // ship instances already populated, per script type
        private static readonly ConditionalWeakTable<IBaseMap, HashSet<Type>> populated = new();

        /// <summary>Open world map the ship is in.</summary>
        protected abstract ushort WorldId { get; }
        /// <summary>The ship interior map.</summary>
        protected abstract ushort ShipWorldId { get; }
        /// <summary>WorldLocation2 of the ship in the open world, the player boards near it and returns to it.</summary>
        protected abstract uint BoardLocationId { get; }
        /// <summary>WorldLocation2 inside the ship where the player arrives.</summary>
        protected abstract uint EntryLocationId { get; }
        /// <summary>Objective index that must be done before boarding, or null when reaching the ship is enough.</summary>
        protected virtual byte? BoardAfterObjective => null;
        /// <summary>Objective index whose completion sends the player back out.</summary>
        protected abstract byte LeaveAfterObjective { get; }
        /// <summary>Creatures placed in the ship, with the WorldLocation2 each stands at.</summary>
        protected abstract (uint CreatureId, uint LocationId)[] Spawns { get; }

        private IQuest owner;
        private double timer;
        private DateTime nextTeleport;

        #region Dependency Injection

        private readonly IEntityFactory entityFactory;
        private readonly ICreatureInfoManager creatureInfoManager;

        protected ShipQuestScript(
            IEntityFactory entityFactory,
            ICreatureInfoManager creatureInfoManager)
        {
            this.entityFactory       = entityFactory;
            this.creatureInfoManager = creatureInfoManager;
        }

        #endregion

        public void OnLoad(IQuest owner)
        {
            this.owner = owner;
        }

        public void Update(double lastTick)
        {
            timer += lastTick;
            if (timer < 1d)
                return;
            timer = 0d;

            IPlayer player = owner?.GetOwner();
            if (player?.Map == null || DateTime.UtcNow < nextTeleport)
                return;

            ushort worldId = (ushort)player.Map.Entry.Id;
            if (worldId == WorldId)
            {
                if (owner.State != QuestState.Accepted || IsDone(LeaveAfterObjective))
                    return;
                if (BoardAfterObjective is byte board && !IsDone(board))
                    return;

                WorldLocation2Entry location = GameTableManager.Instance.WorldLocation2.GetEntry(BoardLocationId);
                if (location == null || Vector3.DistanceSquared(player.Position, Position(location)) > BoardRadius * BoardRadius)
                    return;

                Teleport(player, EntryLocationId);
            }
            else if (worldId == ShipWorldId)
            {
                Populate(player.Map);

                if (owner.State != QuestState.Accepted || IsDone(LeaveAfterObjective))
                    Teleport(player, BoardLocationId);
            }
        }

        private bool IsDone(byte index)
        {
            return owner.GetQuestObjectiveByIndex(index)?.IsComplete() ?? false;
        }

        private void Teleport(IPlayer player, uint locationId)
        {
            WorldLocation2Entry location = GameTableManager.Instance.WorldLocation2.GetEntry(locationId);
            if (location == null)
                return;

            nextTeleport = DateTime.UtcNow.AddSeconds(10d);
            player.TeleportTo((ushort)location.WorldId, location.Position0, location.Position1, location.Position2);
        }

        private void Populate(IBaseMap map)
        {
            HashSet<Type> done = populated.GetOrCreateValue(map);
            if (!done.Add(GetType()))
                return;

            foreach ((uint creatureId, uint locationId) in Spawns)
                Spawn(map, creatureId, locationId);
        }

        private void Spawn(IBaseMap map, uint creatureId, uint locationId)
        {
            ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(creatureId);
            WorldLocation2Entry location = GameTableManager.Instance.WorldLocation2.GetEntry(locationId);
            if (creatureInfo == null || location == null)
                return;

            IWorldEntity entity = entityFactory.CreateWorldEntity(EntityType.NonPlayer);
            entity.Initialise(creatureInfo);
            entity.Rotation = new Vector3(2f * MathF.Atan2(location.Facing1, location.Facing3), 0f, 0f);
            entity.AddToMap(map, Position(location));
        }

        private static Vector3 Position(WorldLocation2Entry location)
        {
            return new Vector3(location.Position0, location.Position1, location.Position2);
        }
    }
}
