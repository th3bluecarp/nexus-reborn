using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.PlayerPath;

namespace NexusForever.WorldServer.Network.Message.Handler.Path
{
    /// <summary>
    /// Dismiss the scanbot (sent by the scanbot toggle when one is out).
    /// </summary>
    public class ClientPathScientistDismissScanbotHandler : IMessageHandler<IWorldSession, ClientPathScientistDismissScanbot>
    {
        public void HandleMessage(IWorldSession session, ClientPathScientistDismissScanbot message)
        {
            session.Player.PathMissionManager.DismissScanbot();
        }
    }
}
