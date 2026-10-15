using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.Entity;

namespace NexusForever.WorldServer.Network.Message.Handler.Combat
{
    public class ClientCCStateStunUpdateHandler : IMessageHandler<IWorldSession, ClientCCStateStunUpdate>
    {
        public void HandleMessage(IWorldSession session, ClientCCStateStunUpdate packet)
        {
        }
    }
}
