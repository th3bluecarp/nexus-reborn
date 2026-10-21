using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.GalacticArchive;

namespace NexusForever.WorldServer.Network.Message.Handler.Misc
{
    /// <summary>
    /// The client unlocked a Galactic Archive article itself (creature interaction, archive link) and reports it.
    /// </summary>
    public class ClientGalacticArchiveUnlockHandler : IMessageHandler<IWorldSession, ClientGalacticArchiveUnlock>
    {
        public void HandleMessage(IWorldSession session, ClientGalacticArchiveUnlock unlock)
        {
            session.Player?.ArchiveManager.ClientUnlock(unlock.ArchiveArticleId);
        }
    }

    public class ClientGalacticArchiveViewedHandler : IMessageHandler<IWorldSession, ClientGalacticArchiveViewed>
    {
        public void HandleMessage(IWorldSession session, ClientGalacticArchiveViewed viewed)
        {
            session.Player?.ArchiveManager.Viewed(viewed.ArchiveArticleId);
        }
    }
}
