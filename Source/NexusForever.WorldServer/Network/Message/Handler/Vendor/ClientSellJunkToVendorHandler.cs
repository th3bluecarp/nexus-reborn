using System.Collections.Generic;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.Shared;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.WorldServer.Network.Message.Handler.Vendor
{
    /// <summary>
    /// Sell Junk: every inferior (grey, ItemQuality 1) item in the bags is sold to the open vendor, as the single sell does.
    /// </summary>
    public class ClientSellJunkToVendorHandler : IMessageHandler<IWorldSession, ClientSellJunkToVendor>
    {
        private const uint JunkQuality = 1u;
        private const uint MaxBagSlots = 256u;

        #region Dependency Injection

        private readonly IBuybackManager buybackManager;

        public ClientSellJunkToVendorHandler(
            IBuybackManager buybackManager)
        {
            this.buybackManager = buybackManager;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientSellJunkToVendor sellJunk)
        {
            IVendorInfo vendorInfo = session.Player.SelectedVendorInfo;
            if (vendorInfo == null)
                return;

            var junk = new List<IItem>();
            for (uint bagIndex = 0u; bagIndex < MaxBagSlots; bagIndex++)
            {
                IItem item = session.Player.Inventory.GetItem(InventoryLocation.Inventory, bagIndex);
                if (item?.Info?.Entry != null && item.Info.Entry.ItemQualityId == JunkQuality)
                    junk.Add(item);
            }

            foreach (IItem item in junk)
            {
                uint quantity = item.StackCount;
                float costMultiplier = vendorInfo.SellPriceMultiplier * quantity;

                var currencyChange = new List<(CurrencyType CurrencyTypeId, ulong CurrencyAmount)>();
                for (int i = 0; i < item.Info.Entry.CurrencyTypeIdSellToVendor.Length; i++)
                {
                    CurrencyType currencyId = item.Info.Entry.CurrencyTypeIdSellToVendor[i];
                    if (currencyId == CurrencyType.None)
                        continue;

                    currencyChange.Add((currencyId, (ulong)(item.Info.Entry.CurrencyAmountSellToVendor[i] * costMultiplier)));
                }

                currencyChange.Add((CurrencyType.Credits, (ulong)(item.GetVendorSellAmount(0) * costMultiplier)));

                foreach ((CurrencyType currencyTypeId, ulong currencyAmount) in currencyChange)
                    session.Player.CurrencyManager.CurrencyAddAmount(currencyTypeId, currencyAmount);

                IItem soldItem = session.Player.Inventory.ItemDelete(new ItemLocation
                {
                    Location = item.Location,
                    BagIndex = item.BagIndex
                }, ItemUpdateReason.Vendor);
                buybackManager.AddItem(session.Player, soldItem, quantity, currencyChange);
            }
        }
    }
}
