using NexusForever.Network.Message;
using NexusForever.Network.World.Message.AccountInventory;

namespace NexusForever.WorldServer.Network.Message.Handler.Account
{
    /// <summary>
    /// The daily login reward calendar asks for the login days.
    /// </summary>
    public class ClientDailyLoginRewardsRequestHandler : IMessageHandler<IWorldSession, ClientDailyLoginRewardsRequest>
    {
        public void HandleMessage(IWorldSession session, ClientDailyLoginRewardsRequest message)
        {
            session.Account.DailyLoginManager.SendUpdate();
        }
    }
}
