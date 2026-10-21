using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Marketplace;

namespace NexusForever.WorldServer.Network.Message.Handler.Marketplace
{
    /// <summary>
    /// The commodity exchange is an NPC market that fills orders at once (CommodityExchangeHandlers), so a player never has open orders.
    /// </summary>
    public class ClientRequestOwnedCommodityOrdersHandler : IMessageHandler<IWorldSession, ClientRequestOwnedCommodityOrders>
    {
        public void HandleMessage(IWorldSession session, ClientRequestOwnedCommodityOrders message)
        {
            // layout verified against WildStar64.exe (reader 0x14009acf0)
            session.EnqueueMessageEncrypted(new ServerOwnedCommodityOrders());
        }
    }

    /// <summary>
    /// The auction house is not implemented, the request is accepted.
    /// </summary>
    public class ClientRequestOwnedItemAuctionsHandler : IMessageHandler<IWorldSession, ClientRequestOwnedItemAuctions>
    {
        public void HandleMessage(IWorldSession session, ClientRequestOwnedItemAuctions message)
        {
            // no reply: the reply layout is unverified and the client works without one
        }
    }
}
