using System;
using System.Linq;
using NexusForever.Game.Abstract.Entity;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Item;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.WorldServer.Network.Message.Handler.Item
{
    /// <summary>
    /// The client's Salvage action (confirm type SalvageItem, WildStar64.exe 0x1403c1460). Loot bags are salvaged to open them,
    /// armor and weapons are broken down into their salvage loot.
    /// </summary>
    public class HandleClientItemUseLootBagHandler : IMessageHandler<IWorldSession, ClientItemUseLootBag>
    {
        /// <summary>
        /// Item loot ids of salvage tables are this plus the client Salvage table id.
        /// </summary>
        public const uint SalvageLootIdBase = 3000000u;

        public void HandleMessage(IWorldSession session, ClientItemUseLootBag useLootBag)
        {
            IItem item = session.Player.Inventory.GetItem(useLootBag.ItemLocation);
            if (item == null)
                throw new ArgumentException($"Item missing at Inventory Location {useLootBag.ItemLocation.Location} and Index {useLootBag.ItemLocation.BagIndex}.");

            if (useLootBag.Guid != item.Guid)
                throw new InvalidOperationException($"Guid {useLootBag.Guid} received does not match the Item found at Inventory Location {useLootBag.ItemLocation.Location} and Index {useLootBag.ItemLocation.BagIndex}.");

            if (!session.Player.Map.LootManager.HasItemLoot(item.Id))
            {
                uint? salvageLootId = GetSalvageLootId(item);
                if (salvageLootId != null && session.Player.Map.LootManager.HasItemLoot(salvageLootId.Value))
                {
                    session.Player.Inventory.ItemDelete(useLootBag.ItemLocation, ItemUpdateReason.Salvage);
                    session.Player.Map.LootManager.GenerateItemLoot(session.Player, salvageLootId.Value);
                    return;
                }
            }

            if (session.Player.Inventory.ItemUse(item))
                session.Player.Map.LootManager.GenerateLoot(session.Player, item);
        }

        /// <summary>
        /// Salvage loot id for armor and weapons: the client Salvage entry of the item's type at the highest level not above its power level.
        /// </summary>
        private static uint? GetSalvageLootId(IItem item)
        {
            SalvageEntry entry = GameTableManager.Instance.Salvage.Entries
                .Where(s => s.Item2TypeId == item.Info.Entry.Item2TypeId && s.Level > 0u && s.Level <= item.Info.Entry.PowerLevel)
                .OrderByDescending(s => s.Level)
                .FirstOrDefault();
            return entry != null ? SalvageLootIdBase + entry.Id : null;
        }
    }
}
