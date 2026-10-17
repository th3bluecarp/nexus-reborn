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
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Algoroc
{
    /// <summary>
    /// Finish Him! (3770) in Algoroc: board the Skull's Eye, kill Captain Thokov on the bridge and flee in the escape pod.
    /// </summary>
    /// <remarks>
    /// Stand in for the lost retail script. The ship interior is its own map (The Skull's Eye, world 930) with nothing placed in the
    /// world data. Meeting the contact at the ship in Algoroc moves the player inside, Captain Thokov (13147) and the escape pod (13306)
    /// are placed at the quest's own map markers, and using the pod returns the player to the ship's spot in Algoroc.
    /// </remarks>
    [ScriptFilterOwnerId(3770)]
    public class FinishHimQuestScript : IQuestScript, IOwnedScript<IQuest>
    {
        private const ushort AlgorocWorldId     = 51;
        private const ushort ShipWorldId        = 930;
        private const uint BoardLocationId      = 9377;  // the Skull's Eye in Algoroc, first objective marker
        private const uint ShipEntryLocationId  = 9371;
        private const uint ThokovLocationId     = 9714;
        private const uint PodLocationId        = 9424;
        private const uint ThokovCreatureId     = 13147;
        private const uint PodCreatureId        = 13306;
        private const float BoardRadius         = 20f;

        // ship instances already given Thokov and the pod
        private static readonly ConditionalWeakTable<IBaseMap, object> populated = new();

        private IQuest owner;
        private double timer;
        private DateTime nextTeleport;

        #region Dependency Injection

        private readonly IEntityFactory entityFactory;
        private readonly ICreatureInfoManager creatureInfoManager;

        public FinishHimQuestScript(
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
            if (worldId == AlgorocWorldId)
            {
                if (owner.State != QuestState.Accepted || !(owner.GetQuestObjectiveByIndex(0)?.IsComplete() ?? false))
                    return;

                WorldLocation2Entry board = GameTableManager.Instance.WorldLocation2.GetEntry(BoardLocationId);
                if (board == null || Vector3.DistanceSquared(player.Position, Position(board)) > BoardRadius * BoardRadius)
                    return;

                Teleport(player, ShipEntryLocationId);
            }
            else if (worldId == ShipWorldId)
            {
                Populate(player.Map);

                // the escape pod objective is the last one, using the pod finishes the quest
                if (owner.State != QuestState.Accepted || (owner.GetQuestObjectiveByIndex(3)?.IsComplete() ?? false))
                    Teleport(player, BoardLocationId);
            }
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
            if (populated.TryGetValue(map, out _))
                return;
            populated.Add(map, new object());

            Spawn(map, ThokovCreatureId, ThokovLocationId);
            Spawn(map, PodCreatureId, PodLocationId);
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
