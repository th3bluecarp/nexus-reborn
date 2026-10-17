using System;
using NexusForever.Game.Static.AccountInventory;
using NexusForever.GameTable;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.PlayerPath;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.WorldServer.Network.Message.Handler.Path
{
    public class ClientPathChangeRequestHandler : IMessageHandler<IWorldSession, ClientPathChangeRequest>
    {
        #region Dependency Injection

        private readonly IGameTableManager gameTableManager;

        public ClientPathChangeRequestHandler(
            IGameTableManager gameTableManager)
        {
            this.gameTableManager = gameTableManager;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientPathChangeRequest clientPathChangeRequest)
        {
            // Reborn: switching paths is free and has no cooldown, so every path can be played on one character
            // (retail: 30 minute cooldown from GameFormula 2366, skipped for 10 service tokens)
            bool needToUseTokens  = false;
            uint bypassCost       = 0u;

            GenericError CanActivatePath()
            {
                if (needToUseTokens && !clientPathChangeRequest.OnCooldown)
                    return GenericError.PathChangeOnCooldown;

                bool hasEnoughTokens = session.Account.CurrencyManager.CanAfford(AccountCurrencyType.ServiceToken, bypassCost); // TODO: Check user has enough tokens
                if (needToUseTokens && clientPathChangeRequest.OnCooldown && !hasEnoughTokens)
                    return GenericError.PathChangeInsufficientFunds;

                if (!session.Player.PathManager.IsPathUnlocked(clientPathChangeRequest.Path))
                    return GenericError.PathChangeNotUnlocked;

                if (session.Player.PathManager.IsPathActive(clientPathChangeRequest.Path))
                    return GenericError.PathChangeRequested;

                return GenericError.Ok;
            }

            GenericError result = CanActivatePath();
            if (result != GenericError.Ok)
            {
                session.Player.PathManager.SendServerPathActivateResult(result);
                return;
            }

            if (needToUseTokens && clientPathChangeRequest.OnCooldown)
                session.Account.CurrencyManager.CurrencySubtractAmount(AccountCurrencyType.ServiceToken, bypassCost);

            session.Player.PathManager.ActivatePath(clientPathChangeRequest.Path);
        }
    }
}
