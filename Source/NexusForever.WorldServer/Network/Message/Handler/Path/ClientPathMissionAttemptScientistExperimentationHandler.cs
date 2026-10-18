using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.PlayerPath;

namespace NexusForever.WorldServer.Network.Message.Handler.Path
{
    /// <summary>
    /// Experimentation solved: the client scores attempts itself and sends only the one that matches the code.
    /// </summary>
    public class ClientPathMissionAttemptScientistExperimentationHandler : IMessageHandler<IWorldSession, ClientPathMissionAttemptScientistExperimentation>
    {
        public void HandleMessage(IWorldSession session, ClientPathMissionAttemptScientistExperimentation message)
        {
            session.Player.PathMissionManager.AttemptExperimentation(message.Choices);
        }
    }
}
