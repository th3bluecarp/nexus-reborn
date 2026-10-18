using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Reward;

namespace NexusForever.WorldServer.Network.Message.Handler.Reward
{
    /// <summary>
    /// Reward rotations (the weekly bonus content list) are not implemented, the request is accepted and nothing is sent.
    /// </summary>
    public class ClientRewardRotationsRequestUpdateHandler : IMessageHandler<IWorldSession, ClientRewardRotationsRequestUpdate>
    {
        public void HandleMessage(IWorldSession session, ClientRewardRotationsRequestUpdate message)
        {
        }
    }
}
