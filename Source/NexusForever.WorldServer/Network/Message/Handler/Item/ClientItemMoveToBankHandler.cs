using System.Linq;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.Shared;

namespace NexusForever.WorldServer.Network.Message.Handler.Item
{
    /// <summary>
    /// Clicking an item in the bags while the bank is open moves it into the first free bank slot.
    /// </summary>
    public class ClientItemMoveToBankHandler : IMessageHandler<IWorldSession, ClientItemMoveToBank>
    {
        public void HandleMessage(IWorldSession session, ClientItemMoveToBank moveToBank)
        {
            IItem item = session.Player.Inventory.GetItem(moveToBank.ItemGuid);
            if (item == null || item.Location != InventoryLocation.Inventory)
                return;

            IBag bank = session.Player.Inventory.FirstOrDefault(b => b.Location == InventoryLocation.PlayerBank);
            uint? bagIndex = bank?.GetFirstAvailableBagIndex();
            if (bagIndex == null)
                return;

            var location = new ItemLocation
            {
                Location = InventoryLocation.PlayerBank,
                BagIndex = bagIndex.Value
            };
            if (session.Player.Inventory.CanMoveItem(item, location) != null)
                return;

            session.Player.Inventory.ItemMove(item, location);
        }
    }
}
