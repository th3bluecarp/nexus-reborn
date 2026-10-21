using System;
using NexusForever.Game.Abstract;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Entity;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Marketplace;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.WorldServer.Network.Message.Handler.Marketplace
{
    /// <summary>
    /// The Commodity Exchange is run by the server as an unlimited NPC market: Buy Now always fills at the server's ask,
    /// Sell Now always fills at its bid, limit orders fill at once when their price crosses the quote and are refused
    /// otherwise (there are no other players to fill them).
    /// </summary>
    /// <remarks>
    /// Prices are invented (docs/reborn/SEMANTICS.md): the ask is the item's vendor buy price in credits (its value when it
    /// has none), the bid is a fifth of the ask, never below the vendor sell price.
    /// </remarks>
    public static class NpcCommodityMarket
    {
        public const uint ListedCount = 999u;

        public static (ulong Ask, ulong Bid)? GetQuote(IItemInfo info)
        {
            // only stackables with a plain credit price are traded, so items priced in other currencies or never sold stay out
            if (info == null || !info.IsStackable() || info.GetVendorBuyCurrency(0) != CurrencyType.Credits || info.Entry.CurrencyTypeId[0] != CurrencyType.Credits)
                return null;

            ulong ask = info.GetVendorBuyAmount(0);
            if (ask == 0ul)
                return null;

            ulong vendorSell = info.GetVendorSellCurrency(0) == CurrencyType.Credits ? info.GetVendorSellAmount(0) : 0ul;
            ulong bid = Math.Max(Math.Max(ask / 5ul, vendorSell), 1ul);
            return (Math.Max(ask, bid + 1ul), bid);
        }
    }

    /// <summary>
    /// The exchange asks about every listed commodity and shows a row only once it is answered.
    /// </summary>
    public class ClientRequestCommodityInfoHandler : IMessageHandler<IWorldSession, ClientRequestCommodityInfo>
    {
        #region Dependency Injection

        private readonly IItemManager itemManager;

        public ClientRequestCommodityInfoHandler(
            IItemManager itemManager)
        {
            this.itemManager = itemManager;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientRequestCommodityInfo request)
        {
            var results = new ServerCommodityInfoResults
            {
                Item2Id = request.Item2Id
            };

            (ulong Ask, ulong Bid)? quote = NpcCommodityMarket.GetQuote(itemManager.GetItemInfo(request.Item2Id));
            if (quote != null)
            {
                // buy orders are what a seller gets (Sell Now), sell orders what a buyer pays (Buy Now), for 1, 10 and 50 units
                results.BuyOrderCount  = NpcCommodityMarket.ListedCount;
                results.SellOrderCount = NpcCommodityMarket.ListedCount;
                results.BuyOrderPrices  = [quote.Value.Bid, quote.Value.Bid, quote.Value.Bid];
                results.SellOrderPrices = [quote.Value.Ask, quote.Value.Ask, quote.Value.Ask];
            }

            session.EnqueueMessageEncrypted(results);
        }
    }

    /// <summary>
    /// Buy and sell orders (one message, IsBuyOrder and ForceImmediate from the four exchange tabs).
    /// </summary>
    public class ClientCommodityOrderSubmitHandler : IMessageHandler<IWorldSession, ClientCommoditySellOrderSubmit>
    {
        #region Dependency Injection

        private readonly IItemManager itemManager;

        public ClientCommodityOrderSubmitHandler(
            IItemManager itemManager)
        {
            this.itemManager = itemManager;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientCommoditySellOrderSubmit submit)
        {
            CommodityOrder order = submit.Order;
            IPlayer player = session.Player;
            if (player == null)
                return;

            GenericError Fill(out ulong cost)
            {
                cost = 0ul;
                if (order.Quantity == 0u || order.Quantity > NpcCommodityMarket.ListedCount)
                    return GenericError.AuctionOrderTooBig;

                IItemInfo info = itemManager.GetItemInfo(order.Item2Id);
                (ulong Ask, ulong Bid)? quote = NpcCommodityMarket.GetQuote(info);
                if (quote == null)
                    return GenericError.AuctionNotFound;

                if (order.IsBuyOrder)
                {
                    if (!order.ForceImmediate && order.PricePerUnit < quote.Value.Ask)
                        return GenericError.AuctionCannotFillOrder;

                    cost = quote.Value.Ask * order.Quantity;
                    if (!player.CurrencyManager.CanAfford(CurrencyType.Credits, cost))
                        return GenericError.VendorNotEnoughCash;

                    uint stacks = (order.Quantity + Math.Max(info.Entry.MaxStackCount, 1u) - 1u) / Math.Max(info.Entry.MaxStackCount, 1u);
                    if (player.Inventory.GetInventorySlotsRemaining(InventoryLocation.Inventory) < stacks)
                        return GenericError.ItemInventoryFull;

                    player.CurrencyManager.CurrencySubtractAmount(CurrencyType.Credits, cost);
                    player.Inventory.ItemCreate(InventoryLocation.Inventory, order.Item2Id, order.Quantity, ItemUpdateReason.Auction);
                }
                else
                {
                    if (!order.ForceImmediate && order.PricePerUnit > quote.Value.Bid)
                        return GenericError.AuctionCannotFillOrder;

                    if (player.Inventory.GetItemCount(order.Item2Id) < order.Quantity)
                        return GenericError.VendorNotEnoughToFillQuantity;

                    cost = quote.Value.Bid * order.Quantity;
                    player.Inventory.ItemDelete(order.Item2Id, order.Quantity, ItemUpdateReason.Auction);
                    player.CurrencyManager.CurrencyAddAmount(CurrencyType.Credits, cost);
                }

                order.PricePerUnit = order.IsBuyOrder ? quote.Value.Ask : quote.Value.Bid;
                order.Price        = cost;
                return GenericError.Ok;
            }

            GenericError result = Fill(out ulong total);
            session.EnqueueMessageEncrypted(new ServerCommodityOrderResult
            {
                Result          = result,
                OrderPosted     = order,
                CostToPostOrder = 0ul
            });
        }
    }

    /// <summary>
    /// There are never open orders to cancel on the NPC market.
    /// </summary>
    public class ClientCommodityOrderCancelHandler : IMessageHandler<IWorldSession, ClientCommodityOrderCancel>
    {
        public void HandleMessage(IWorldSession session, ClientCommodityOrderCancel cancel)
        {
        }
    }
}
