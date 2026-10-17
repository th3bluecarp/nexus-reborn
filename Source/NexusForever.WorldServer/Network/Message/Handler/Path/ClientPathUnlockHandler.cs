using NexusForever.Game.Static.AccountInventory;
using NexusForever.GameTable;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.PlayerPath;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.WorldServer.Network.Message.Handler.Path
{
    public class ClientPathUnlockHandler : IMessageHandler<IWorldSession, ClientPathUnlockRequest>
    {
        #region Dependency Injection

        private readonly IGameTableManager gameTableManager;

        public ClientPathUnlockHandler(
            IGameTableManager gameTableManager)
        {
            this.gameTableManager = gameTableManager;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientPathUnlockRequest clientPathUnlockRequest)
        {
            // Reborn: unlocking a path is free (retail: 60 service tokens from GameFormula 2365)
            uint unlockCost = 0u;

            GenericError CanUnlockPath()
            {
                bool hasEnoughTokens = session.Account.CurrencyManager.CanAfford(AccountCurrencyType.ServiceToken, unlockCost);
                if (!hasEnoughTokens)
                    return GenericError.PathInsufficientFunds;

                if (session.Player.PathManager.IsPathUnlocked(clientPathUnlockRequest.Path))
                    return GenericError.PathAlreadyUnlocked;

                return GenericError.Ok;
            }

            GenericError result = CanUnlockPath();
            if (result != GenericError.Ok)
            {
                session.Player.PathManager.SendServerPathUnlockResult(result);
                return;
            }

            session.Player.PathManager.UnlockPath(clientPathUnlockRequest.Path);
            session.Account.CurrencyManager.CurrencySubtractAmount(AccountCurrencyType.ServiceToken, unlockCost);
        }
    }
}
