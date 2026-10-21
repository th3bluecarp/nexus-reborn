using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Pvp;
using NexusForever.Game.Static.Pvp;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Pvp;

namespace NexusForever.WorldServer.Network.Message.Handler.Pvp
{
    /// <summary>
    /// Challenge the targeted player to a duel.
    /// </summary>
    public class ClientDuelInitateHandler : IMessageHandler<IWorldSession, ClientDuelInitate>
    {
        /// <summary>
        /// Invented challenge range.
        /// </summary>
        private const float MaxChallengeDistance = 40f;

        public void HandleMessage(IWorldSession session, ClientDuelInitate _)
        {
            IPlayer challenger = session.Player;
            if (challenger?.Map == null)
                return;

            IPlayer opponent = challenger.TargetGuid != null ? challenger.GetVisible<IPlayer>(challenger.TargetGuid.Value) : null;

            DuelFailureReason? GetFailure()
            {
                if (opponent == null || opponent == challenger)
                    return DuelFailureReason.InvalidDuelTarget;
                if (!challenger.IsAlive)
                    return DuelFailureReason.YouCannotDuelWhileDead;
                if (!opponent.IsAlive)
                    return DuelFailureReason.CannotDuelDeadPlayer;
                if (challenger.Duel != null)
                    return challenger.Duel.IsActive ? DuelFailureReason.YouAreAlreadyDueling : DuelFailureReason.YouHaveDuelRequestPending;
                if (opponent.Duel != null)
                    return opponent.Duel.IsActive ? DuelFailureReason.PlayerIsAlreadyDueling : DuelFailureReason.PlayerHasDuelRequestPending;
                if (challenger.InCombat)
                    return DuelFailureReason.YouAreInCombat;
                if (opponent.InCombat)
                    return DuelFailureReason.PlayerIsInCombat;
                if (opponent.IgnoreDuelRequests)
                    return DuelFailureReason.PlayerIsIgnoringDuels;
                if (Vector3.Distance(challenger.Position, opponent.Position) > MaxChallengeDistance)
                    return DuelFailureReason.TooFarAway;
                return null;
            }

            DuelFailureReason? failure = GetFailure();
            if (failure != null)
            {
                session.EnqueueMessageEncrypted(new ServerDuelFailure
                {
                    Reason = failure.Value
                });
                return;
            }

            new Duel(challenger, opponent);
        }
    }

    public class ClientDuelAcceptHandler : IMessageHandler<IWorldSession, ClientDuelAccept>
    {
        public void HandleMessage(IWorldSession session, ClientDuelAccept _)
        {
            if (session.Player?.Duel?.Opponent == session.Player)
                session.Player.Duel.Accept();
        }
    }

    public class ClientDuelDeclineHandler : IMessageHandler<IWorldSession, ClientDuelDecline>
    {
        public void HandleMessage(IWorldSession session, ClientDuelDecline _)
        {
            if (session.Player?.Duel?.Opponent == session.Player)
                session.Player.Duel.Decline();
        }
    }

    public class ClientDuelForfeitHandler : IMessageHandler<IWorldSession, ClientDuelForfeit>
    {
        public void HandleMessage(IWorldSession session, ClientDuelForfeit _)
        {
            session.Player?.Duel?.Forfeit(session.Player);
        }
    }

    public class ClientSetIgnoreDuelRequestsHandler : IMessageHandler<IWorldSession, ClientSetIgnoreDuelRequests>
    {
        public void HandleMessage(IWorldSession session, ClientSetIgnoreDuelRequests ignore)
        {
            if (session.Player != null)
                session.Player.IgnoreDuelRequests = ignore.Ignore;
        }
    }
}
