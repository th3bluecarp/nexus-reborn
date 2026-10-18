using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.PlayerPath;

namespace NexusForever.WorldServer.Network.Message.Handler.Path
{
    /// <summary>
    /// Scan with the scanbot (path action button with the scanbot out).
    /// </summary>
    public class ClientPathScientistScanHandler : IMessageHandler<IWorldSession, ClientPathScientistScan>
    {
        public void HandleMessage(IWorldSession session, ClientPathScientistScan message)
        {
            session.Player.PathMissionManager.Scan();
        }
    }
}
