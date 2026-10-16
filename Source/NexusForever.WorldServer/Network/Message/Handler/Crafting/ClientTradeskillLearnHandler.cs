using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Crafting;

namespace NexusForever.WorldServer.Network.Message.Handler.Crafting
{
    public class ClientTradeskillLearnHandler : IMessageHandler<IWorldSession, ClientTradeskillLearn>
    {
        public void HandleMessage(IWorldSession session, ClientTradeskillLearn tradeskillLearn)
        {
            session.Player.TradeskillManager.Learn(tradeskillLearn.ToLearnTradeskillId, tradeskillLearn.ToDropTradeskillId);
        }
    }
}
