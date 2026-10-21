using System.Collections.Generic;
using System.Linq;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Vendor
{
    /// <summary>
    /// Repair one item or everything at a vendor, charging what the client shows (<see cref="IItem.GetRepairCost"/>).
    /// </summary>
    /// <remarks>
    /// Guild repairs (GuildId set) are paid by the player, there is no guild bank.
    /// </remarks>
    public class ClientItemRepairHandler : IMessageHandler<IWorldSession, ClientItemRepair>
    {
        public void HandleMessage(IWorldSession session, ClientItemRepair repair)
        {
            IPlayer player = session.Player;
            if (player?.SelectedVendorInfo == null)
                return;

            List<IItem> items;
            if (repair.ItemGuid != 0ul)
            {
                IItem item = player.Inventory.GetItem(repair.ItemGuid);
                if (item == null)
                    return;
                items = [item];
            }
            else
                items = player.Inventory.GetDamagedItems().ToList();

            items = items.Where(i => i.GetRepairCost() > 0ul).ToList();
            if (items.Count == 0)
                return;

            ulong cost = items.Aggregate(0ul, (total, item) => total + item.GetRepairCost());
            if (!player.CurrencyManager.CanAfford(CurrencyType.Credits, cost))
            {
                player.SendSystemMessage("You cannot afford the repair.");
                return;
            }

            player.CurrencyManager.CurrencySubtractAmount(CurrencyType.Credits, cost);
            foreach (IItem item in items)
                player.Inventory.ItemDurabilityUpdate(item, 1f);
        }
    }
}
