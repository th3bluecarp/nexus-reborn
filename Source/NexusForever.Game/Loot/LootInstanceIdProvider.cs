using NexusForever.Game.Abstract.Loot;

namespace NexusForever.Game.Loot
{
    public class LootInstanceIdProvider : ILootInstanceIdProvider
    {
        // Loot ids go to the client as LootItem.LootUnitId. On ServerLootGrant the client removes the unit with that id
        // (WildStar64 0x1403db183, retail loot dropped as units), so ids that are also entity guids removed real units: id 1 is
        // the first player of a map, whose removal empties the client player pointer and crashes the next item stack update
        // (0x1403c0c3d). Loot ids start far above any entity guid.
        private uint id = 0x7F000000u;

        public uint GetId()
        {
            return id++;
        }
    }
}
