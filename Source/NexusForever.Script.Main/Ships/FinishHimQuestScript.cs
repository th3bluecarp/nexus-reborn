using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Ships
{
    /// <summary>
    /// Finish Him! (3770) in Algoroc: board the Skull's Eye (world 930), kill Captain Thokov on the bridge and flee in the escape pod.
    /// </summary>
    /// <remarks>
    /// Meeting the contact at the ship in Algoroc moves the player inside. Thokov's crew is not placed.
    /// Captain Thokov belongs to the kill step (index 2), the escape pod to step 3.
    /// </remarks>
    [ScriptFilterOwnerId(3770)]
    public class FinishHimQuestScript : ShipQuestScript
    {
        protected override ushort WorldId => 51;
        protected override ushort ShipWorldId => 930;
        protected override uint BoardLocationId => 9377;
        protected override uint EntryLocationId => 9371;
        protected override byte? BoardAfterObjective => 0;
        protected override byte LeaveAfterObjective => 3;
        protected override (uint CreatureId, uint LocationId, byte ObjectiveIndex)[] Spawns => [(13147u, 9714u, 2), (13306u, 9424u, 3)]; // Captain Thokov, escape pod

        public FinishHimQuestScript(IEntityFactory entityFactory, ICreatureInfoManager creatureInfoManager)
            : base(entityFactory, creatureInfoManager)
        {
        }
    }
}
