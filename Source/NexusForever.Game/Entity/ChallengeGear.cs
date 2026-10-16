using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Reputation;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Entity
{
    /// <summary>
    /// Challenge reward gear: the green sets with ItemSourceId 87 and 88, levels 12 to 49, one armor piece or weapon per class and role at each level.
    /// </summary>
    /// <remarks>
    /// Retail reward tracks offered "Challenger's Gear Bag" items whose contents were server side, these sets are the closest match in the client data.
    /// Weapons give the class by weapon category. Armor gives two classes by armor category, at each slot and level the four pieces are ordered by id
    /// as two per class (the class order of the weapons) with the damage version first; checked against the names of all 552 armor pieces.
    /// Which source is which faction (87 Exile, 88 Dominion) is read from the set names. The slot given at each reward is a rotation, not retail.
    /// </remarks>
    public static class ChallengeGear
    {
        private const uint ExileSource    = 87u;
        private const uint DominionSource = 88u;

        // class order of the pieces in a slot, and the armor category each class wears
        private static readonly Class[] ArmorOrder = [Class.Warrior, Class.Engineer, Class.Medic, Class.Stalker, Class.Spellslinger, Class.Esper];
        private static readonly Dictionary<Class, uint> ArmorCategory = new()
        {
            { Class.Warrior, 3u }, { Class.Engineer, 3u }, { Class.Medic, 2u }, { Class.Stalker, 2u }, { Class.Spellslinger, 1u }, { Class.Esper, 1u }
        };
        private static readonly Dictionary<uint, Class> WeaponCategory = new()
        {
            { 8u, Class.Warrior }, { 108u, Class.Engineer }, { 22u, Class.Esper }, { 12u, Class.Medic }, { 24u, Class.Stalker }, { 16u, Class.Spellslinger }
        };

        // (source, class) -> slot type -> damage version pieces ordered by level
        private static Dictionary<(uint, Class), SortedDictionary<uint, List<Item2Entry>>> catalog;

        private static void Build()
        {
            var result = new Dictionary<(uint, Class), SortedDictionary<uint, List<Item2Entry>>>();
            void Add(uint source, Class @class, Item2Entry item)
            {
                if (!result.TryGetValue((source, @class), out var slots))
                    result.Add((source, @class), slots = new SortedDictionary<uint, List<Item2Entry>>());
                if (!slots.TryGetValue(item.Item2TypeId, out var items))
                    slots.Add(item.Item2TypeId, items = []);
                items.Add(item);
            }

            foreach (uint source in new[] { ExileSource, DominionSource })
            {
                var items = GameTableManager.Instance.Item.Entries.Where(i => i.ItemSourceId == source).ToList();

                // weapons, damage version is the lower id of the pair
                foreach (var group in items.Where(i => i.Item2FamilyId == 2u && WeaponCategory.ContainsKey(i.Item2CategoryId))
                    .GroupBy(i => (i.Item2CategoryId, i.RequiredLevel)))
                    Add(source, WeaponCategory[group.Key.Item2CategoryId], group.OrderBy(i => i.Id).First());

                foreach (var group in items.Where(i => i.Item2FamilyId == 1u).GroupBy(i => (i.Item2CategoryId, i.Item2TypeId, i.RequiredLevel)))
                {
                    Class[] classes = ArmorOrder.Where(c => ArmorCategory[c] == group.Key.Item2CategoryId).ToArray();
                    List<Item2Entry> ordered = group.OrderBy(i => i.Id).ToList();
                    if (classes.Length != 2 || ordered.Count != 4)
                        continue;

                    Add(source, classes[0], ordered[0]);
                    Add(source, classes[1], ordered[2]);
                }
            }

            foreach (var slots in result.Values)
                foreach (var items in slots.Values)
                    items.Sort((a, b) => a.RequiredLevel.CompareTo(b.RequiredLevel));

            catalog = result;
        }

        /// <summary>
        /// Return the challenge gear item for the reward at <paramref name="index"/> of a reward track, 0 when the player has no set.
        /// </summary>
        public static uint Pick(IPlayer player, uint rewardTrackId, int index)
        {
            if (catalog == null)
                Build();

            uint source = player.Faction1 == Faction.Dominion ? DominionSource : ExileSource;
            if (!catalog.TryGetValue((source, player.Class), out var slots) || slots.Count == 0)
                return 0u;

            // rotate through the slots so each track and reward gives a different piece
            List<Item2Entry> items = slots.Values.ElementAt((int)((rewardTrackId * 4u + (uint)index) % (uint)slots.Count));
            Item2Entry item = items.LastOrDefault(i => i.RequiredLevel <= player.Level) ?? items[0];
            return item.Id;
        }
    }
}
