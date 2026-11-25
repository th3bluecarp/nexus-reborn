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
    /// A placed creature or object that is gone while a player's step still needs it is placed again: the ship is an instance
    /// without database spawns, so nothing in it comes back on its own.
    /// </remarks>
    public abstract class ShipQuestScript : IQuestScript, IOwnedScript<IQuest>
    {
        private const float BoardRadius = 20f;

        // how long a placed creature must be gone before it is placed again
        private static readonly TimeSpan ReplaceDelay = TimeSpan.FromSeconds(10d);

        private sealed class Placed
        {
            public IWorldEntity Entity;
            public DateTime? GoneSince;
        }

        // what each script type has placed in a ship instance, by index into Spawns
        private static readonly ConditionalWeakTable<IBaseMap, Dictionary<(Type Script, int Index), Placed>> placed = new();

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
        /// <summary>Creatures placed in the ship, with the WorldLocation2 each stands at and the objective index that needs it.</summary>
        protected abstract (uint CreatureId, uint LocationId, byte ObjectiveIndex)[] Spawns { get; }

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
            if (player?.Map?.Entry == null)
                return;

            ushort worldId = (ushort)player.Map.Entry.Id;
            // not held back by the teleport cooldown: the ship used to stand empty for the first ten seconds aboard
            if (worldId == ShipWorldId)
                Populate(player.Map);

            if (DateTime.UtcNow < nextTeleport)
                return;

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

        /// <summary>
        /// Place the quest's creatures in the ship instance, and place again what is gone while the owner's step still needs it.
        /// </summary>
        /// <remarks>
        /// Using a quest object removes it (WorldEntity.OnActivateSuccess) even when its step is not open yet: the Dust Stalker's
        /// access panel stands where the player arrives, and used before Boss Xagg was dead it gave no credit and was gone for
        /// good, with no other way to finish or leave. The same goes for a boss killed before its step opened.
        /// </remarks>
        private void Populate(IBaseMap map)
        {
            Dictionary<(Type Script, int Index), Placed> state = placed.GetOrCreateValue(map);
            (uint CreatureId, uint LocationId, byte ObjectiveIndex)[] spawns = Spawns;
            for (int i = 0; i < spawns.Length; i++)
            {
                (Type Script, int Index) key = (GetType(), i);
                if (!state.TryGetValue(key, out Placed entry))
                {
                    state[key] = new Placed { Entity = Spawn(map, spawns[i].CreatureId, spawns[i].LocationId) };
                    continue;
                }

                if (IsPresent(entry.Entity, map))
                {
                    entry.GoneSince = null;
                    continue;
                }

                // gone for good once the step it belongs to is done (Boss Xagg killed, the bridge controls used)
                if (owner.State != QuestState.Accepted || IsDone(spawns[i].ObjectiveIndex))
                    continue;

                entry.GoneSince ??= DateTime.UtcNow;
                if (DateTime.UtcNow - entry.GoneSince.Value < ReplaceDelay)
                    continue;

                entry.Entity    = Spawn(map, spawns[i].CreatureId, spawns[i].LocationId);
                entry.GoneSince = null;
            }
        }

        /// <summary>
        /// Returns if a placed entity is still on the ship's map and, for a creature with health, alive. An entity queued for the
        /// map is not on it yet, <see cref="ReplaceDelay"/> covers that.
        /// </summary>
        private static bool IsPresent(IWorldEntity entity, IBaseMap map)
        {
            if (entity == null || entity.Map != map || entity.PendingRemoval)
                return false;

            return entity.MaxHealth == 0u || entity.Health > 0u;
        }

        private IWorldEntity Spawn(IBaseMap map, uint creatureId, uint locationId)
        {
            ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(creatureId);
            WorldLocation2Entry location = GameTableManager.Instance.WorldLocation2.GetEntry(locationId);
            if (creatureInfo == null || location == null)
                return null;

            IWorldEntity entity = entityFactory.CreateWorldEntity(EntityType.NonPlayer);
            entity.Initialise(creatureInfo);
            entity.Rotation = new Vector3(2f * MathF.Atan2(location.Facing1, location.Facing3), 0f, 0f);
            entity.AddToMap(map, Position(location));
            return entity;
        }

        private static Vector3 Position(WorldLocation2Entry location)
        {
            return new Vector3(location.Position0, location.Position1, location.Position2);
        }
    }
}
