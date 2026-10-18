using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.PlayerPath;

namespace NexusForever.WorldServer.Network.Message.Handler.Path
{
    /// <summary>
    /// Summon the scanbot (sent by the scanbot toggle, and by the path action button when no scanbot is out).
    /// </summary>
    public class ClientPathScientistRequestScanbotHandler : IMessageHandler<IWorldSession, ClientPathScientistRequestScanbot>
    {
        public void HandleMessage(IWorldSession session, ClientPathScientistRequestScanbot message)
        {
            session.Player.PathMissionManager.SummonScanbot(message.ScanbotProfile);
        }
    }
}
