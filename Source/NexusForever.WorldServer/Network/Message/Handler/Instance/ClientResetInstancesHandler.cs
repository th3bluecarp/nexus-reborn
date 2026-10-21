using System.Linq;
using NexusForever.Game.Abstract.Map.Lock;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Instance;

namespace NexusForever.WorldServer.Network.Message.Handler.Instance
{
    /// <summary>
    /// Reset Instances (social menu and portal dialog): drop the character's solo instance locks so the next entry
    /// starts a fresh copy. The instance the character stands in is kept, like retail refused to reset an occupied one.
    /// </summary>
    public class ClientResetInstancesHandler : IMessageHandler<IWorldSession, ClientResetInstances>
    {
        #region Dependency Injection

        private readonly IMapLockManager mapLockManager;

        public ClientResetInstancesHandler(
            IMapLockManager mapLockManager)
        {
            this.mapLockManager = mapLockManager;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientResetInstances _)
        {
            InstanceReset.Reset(session, mapLockManager);
        }
    }

    public class ClientResetSingleInstanceHandler : IMessageHandler<IWorldSession, ClientResetSingleInstance>
    {
        #region Dependency Injection

        private readonly IMapLockManager mapLockManager;

        public ClientResetSingleInstanceHandler(
            IMapLockManager mapLockManager)
        {
            this.mapLockManager = mapLockManager;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientResetSingleInstance _)
        {
            // the portal does not say which world it leads to, so this resets every solo lock like Reset Instances
            InstanceReset.Reset(session, mapLockManager);
        }
    }

    internal static class InstanceReset
    {
        public static void Reset(IWorldSession session, IMapLockManager mapLockManager)
        {
            if (session.Player?.Map == null)
                return;

            uint removed = mapLockManager.ResetSoloLocks(session.Player.Identity, session.Player.Map.Entry.Id);
            session.EnqueueMessageEncrypted(new ServerInstanceResetResult
            {
                Success = true
            });

            session.Player.SendSystemMessage(removed > 0u
                ? $"{removed} instance(s) have been reset."
                : "You have no instances to reset.");
        }
    }
}
