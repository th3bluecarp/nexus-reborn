using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Ships
{
    /// <summary>
    /// Boss Xagg's Due (4516) in Galeras: enter the Dust Stalker (world 1138), kill Boss Xagg, set the ship to self destruct at the bridge
    /// controls and get out through the access panel.
    /// </summary>
    /// <remarks>
    /// Reaching the ship in Galeras moves the player inside, arriving at the access panel. Xagg's crew is not placed.
    /// Boss Xagg belongs to the kill step (index 1), the bridge controls to step 2 and the access panel to step 3.
    /// </remarks>
    [ScriptFilterOwnerId(4516)]
    public class BossXaggsDueQuestScript : ShipQuestScript
    {
        protected override ushort WorldId => 51;
        protected override ushort ShipWorldId => 1138;
        protected override uint BoardLocationId => 27395;
        protected override uint EntryLocationId => 12400;
        protected override byte LeaveAfterObjective => 3;
        protected override (uint CreatureId, uint LocationId, byte ObjectiveIndex)[] Spawns => [(16707u, 17584u, 1), (16733u, 17585u, 2), (23758u, 12400u, 3)]; // Boss Xagg, bridge controls, access panel

        public BossXaggsDueQuestScript(IEntityFactory entityFactory, ICreatureInfoManager creatureInfoManager)
            : base(entityFactory, creatureInfoManager)
        {
        }
    }
}
