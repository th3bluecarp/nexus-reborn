using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Trade;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;
using P2PTradeResult = NexusForever.Network.World.Message.Model.ServerP2PTradeResult.P2PTradeResult;

namespace NexusForever.WorldServer.Network.Message.Handler.Item
{
    /// <summary>
    /// Invite a player to trade. The initiator's client opens its side by itself, so refusals must close it (Cancelled).
    /// </summary>
    public class ClientP2PTradingInitiateTradeHandler : IMessageHandler<IWorldSession, ClientP2PTradingInitiateTrade>
    {
        public void HandleMessage(IWorldSession session, ClientP2PTradingInitiateTrade initiate)
        {
            IPlayer initiator = session.Player;
            if (initiator?.Map == null)
                return;

            IPlayer target = initiator.GetVisible<IPlayer>(initiate.TargetUnitId);

            P2PTradeResult? GetFailure()
            {
                if (target == null || target == initiator || !target.IsAlive || !initiator.IsAlive)
                    return P2PTradeResult.MissingPlayer;
                if (initiator.Trade != null)
                    return P2PTradeResult.ErrorInitiating;
                if (target.Trade != null)
                    return P2PTradeResult.TargetBusy;
                if (Vector3.Distance(initiator.Position, target.Position) > PlayerTrade.MaxDistance)
                    return P2PTradeResult.ErrorInitiating;
                return null;
            }

            P2PTradeResult? failure = GetFailure();
            if (failure != null)
            {
                session.EnqueueMessageEncrypted(new ServerP2PTradeResult
                {
                    Result    = failure.Value,
                    Cancelled = true
                });
                return;
            }

            new PlayerTrade(initiator, target);
        }
    }

    public class ClientP2PTradingAcceptInviteHandler : IMessageHandler<IWorldSession, ClientP2PTradingAcceptInvite>
    {
        public void HandleMessage(IWorldSession session, ClientP2PTradingAcceptInvite _)
        {
            session.Player?.Trade?.Accept(session.Player);
        }
    }

    public class ClientP2PTradingDeclineInviteHandler : IMessageHandler<IWorldSession, ClientP2PTradingDeclineInvite>
    {
        public void HandleMessage(IWorldSession session, ClientP2PTradingDeclineInvite _)
        {
            session.Player?.Trade?.Decline(session.Player);
        }
    }

    public class ClientP2PTradingCancelTradeHandler : IMessageHandler<IWorldSession, ClientP2PTradingCancelTrade>
    {
        public void HandleMessage(IWorldSession session, ClientP2PTradingCancelTrade _)
        {
            session.Player?.Trade?.Cancel(session.Player);
        }
    }

    public class ClientP2PTradingAddItemHandler : IMessageHandler<IWorldSession, ClientP2PTradingAddItem>
    {
        public void HandleMessage(IWorldSession session, ClientP2PTradingAddItem add)
        {
            session.Player?.Trade?.AddItem(session.Player, add.ItemGuid);
        }
    }

    public class ClientP2PTradingRemoveItemHandler : IMessageHandler<IWorldSession, ClientP2PTradingRemoveItem>
    {
        public void HandleMessage(IWorldSession session, ClientP2PTradingRemoveItem remove)
        {
            session.Player?.Trade?.RemoveItem(session.Player, remove.ItemGuid);
        }
    }

    public class ClientP2PTradingSetMoneyHandler : IMessageHandler<IWorldSession, ClientP2PTradingSetMoney>
    {
        public void HandleMessage(IWorldSession session, ClientP2PTradingSetMoney money)
        {
            session.Player?.Trade?.SetMoney(session.Player, money.Credits);
        }
    }

    public class ClientP2PTradingCommitHandler : IMessageHandler<IWorldSession, ClientP2PTradingCommit>
    {
        public void HandleMessage(IWorldSession session, ClientP2PTradingCommit _)
        {
            session.Player?.Trade?.Commit(session.Player);
        }
    }
}
