using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Marketplace;

namespace NexusForever.WorldServer.Network.Message.Handler.Marketplace
{
    /// <summary>
    /// The commodity exchange is not implemented, the request (sent at login and when the exchange opens) is accepted.
    /// </summary>
    public class ClientRequestOwnedCommodityOrdersHandler : IMessageHandler<IWorldSession, ClientRequestOwnedCommodityOrders>
    {
        public void HandleMessage(IWorldSession session, ClientRequestOwnedCommodityOrders message)
        {
            // no reply: the reply layout is unverified and the client works without one
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
