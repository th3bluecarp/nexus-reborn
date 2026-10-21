using NexusForever.Network.Message;
using NexusForever.Network.World.Message.AccountInventory;
using NexusForever.Network.World.Message.Model.AccountInventory;

namespace NexusForever.WorldServer.Network.Message.Handler.Account
{
    /// <summary>
    /// CREDD exchange listing, asked for at login: there is no CREDD market, so the answer is an empty one.
    /// </summary>
    public class ClientCreddExchangeInfoRequestHandler : IMessageHandler<IWorldSession, ClientCreddExchangeInfoRequest>
    {
        public void HandleMessage(IWorldSession session, ClientCreddExchangeInfoRequest request)
        {
            session.EnqueueMessageEncrypted(new ServerCreddExchangeInfo());
        }
    }
}
