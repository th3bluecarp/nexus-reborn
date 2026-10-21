using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Quest
{
    /// <summary>
    /// The player answered or dismissed a communicator call. The call's quest was already mentioned when the call was sent
    /// (QuestManager.CheckPrerequisiteCommunicators), and accepting it arrives as a normal quest accept, so there is nothing to do.
    /// </summary>
    public class ClientCommunicatorActionHandler : IMessageHandler<IWorldSession, ClientCommunicatorAction>
    {
        public void HandleMessage(IWorldSession session, ClientCommunicatorAction action)
        {
        }
    }
}
