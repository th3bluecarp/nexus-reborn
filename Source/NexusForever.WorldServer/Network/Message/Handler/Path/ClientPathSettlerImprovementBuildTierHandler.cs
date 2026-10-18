using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.PlayerPath;

namespace NexusForever.WorldServer.Network.Message.Handler.Path
{
    /// <summary>
    /// Build an improvement tier at a settler depot.
    /// </summary>
    public class ClientPathSettlerImprovementBuildTierHandler : IMessageHandler<IWorldSession, ClientPathSettlerImprovementBuildTier>
    {
        public void HandleMessage(IWorldSession session, ClientPathSettlerImprovementBuildTier message)
        {
            session.Player.PathMissionManager.BuildImprovement(message.PathSettlerImprovementGroupId, message.BuildTier);
        }
    }
}
