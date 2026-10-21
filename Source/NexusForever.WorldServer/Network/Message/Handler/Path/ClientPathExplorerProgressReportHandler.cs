using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.PlayerPath;

namespace NexusForever.WorldServer.Network.Message.Handler.Path
{
    /// <summary>
    /// The client reports an Explorer mission node it reached (its mission update loop, WildStar64.exe 0x14056fbe0).
    /// </summary>
    public class ClientPathExplorerProgressReportHandler : IMessageHandler<IWorldSession, ClientPathExplorerProgressReport>
    {
        public void HandleMessage(IWorldSession session, ClientPathExplorerProgressReport report)
        {
            session.Player?.PathMissionManager.OnExplorerProgressReport(report.PathMissionId, report.ExplorerNodeIndex);
        }
    }
}
