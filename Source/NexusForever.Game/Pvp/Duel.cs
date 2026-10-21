using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Pvp;
using NexusForever.Game.Static.Pvp;
using NexusForever.Network.World.Message.Model.Pvp;

namespace NexusForever.Game.Pvp
{
    /// <summary>
    /// A duel between two players: challenge, 5 second countdown, fight until one is brought to 1 health, forfeits or stays out of the area.
    /// </summary>
    /// <remarks>
    /// Timings and the area radius are invented (see docs/reborn/SEMANTICS.md). The challenger's update drives the duel.
    /// </remarks>
    public class Duel : IDuel
    {
        public const double RequestTimeout = 30d;
        public const double CountdownTime  = 5d;
        public const double LeaveAreaGrace = 10d;
        public const float AreaRadius      = 50f;

        private enum DuelState
        {
            Requested,
            Countdown,
            Active,
            Finished
        }

        public IPlayer Challenger { get; }
        public IPlayer Opponent { get; }
        public bool IsActive => state == DuelState.Active;

        private DuelState state = DuelState.Requested;
        private double timer;
        private readonly Vector3 center;
        private readonly Dictionary<uint, double> outside = [];

        public Duel(IPlayer challenger, IPlayer opponent)
        {
            Challenger = challenger;
            Opponent   = opponent;
            center     = Vector3.Lerp(challenger.Position, opponent.Position, 0.5f);

            challenger.Duel = this;
            opponent.Duel   = this;

            var challenge = new ServerDuelChallenge
            {
                ChallengerUnitId = challenger.Guid,
                OpponentUnitId   = opponent.Guid
            };
            challenger.Session.EnqueueMessageEncrypted(challenge);
            opponent.Session.EnqueueMessageEncrypted(challenge);
        }

        public bool Involves(IPlayer player)
        {
            return player != null && (player == Challenger || player == Opponent);
        }

        public void Accept()
        {
            if (state != DuelState.Requested)
                return;

            state = DuelState.Countdown;
            timer = 0d;

            var countdown = new ServerDuelCountdown
            {
                ChallengerUnitId = Challenger.Guid,
                OpponentUnitId   = Opponent.Guid
            };
            Challenger.Session.EnqueueMessageEncrypted(countdown);
            Opponent.Session.EnqueueMessageEncrypted(countdown);
        }

        public void Decline()
        {
            if (state != DuelState.Requested)
                return;

            Finish(Challenger, Opponent, DuelFinishReason.DeclinedRequest);
        }

        public void Forfeit(IPlayer player)
        {
            if (!Involves(player) || state == DuelState.Finished)
                return;

            IPlayer winner = player == Challenger ? Opponent : Challenger;
            Finish(winner, player, state == DuelState.Active ? DuelFinishReason.Forfeited2 : DuelFinishReason.DuelCancelled);
        }

        public void Defeat(IPlayer loser)
        {
            if (!Involves(loser) || state != DuelState.Active)
                return;

            Finish(loser == Challenger ? Opponent : Challenger, loser, DuelFinishReason.Defeated);
        }

        public void Cancel()
        {
            if (state == DuelState.Finished)
                return;

            Finish(Challenger, Opponent, DuelFinishReason.DuelCancelled);
        }

        public void Update(double lastTick)
        {
            if (state == DuelState.Finished)
                return;

            if (Challenger.Map == null || Opponent.Map == null || Challenger.Map != Opponent.Map)
            {
                Cancel();
                return;
            }

            timer += lastTick;
            switch (state)
            {
                case DuelState.Requested:
                    if (timer >= RequestTimeout)
                        Finish(Challenger, Opponent, DuelFinishReason.DeclinedRequest);
                    break;
                case DuelState.Countdown:
                    if (timer >= CountdownTime)
                        Start();
                    break;
                case DuelState.Active:
                    CheckArea(Challenger, lastTick);
                    if (state == DuelState.Active)
                        CheckArea(Opponent, lastTick);
                    break;
            }
        }

        private void Start()
        {
            state = DuelState.Active;
            var start = new ServerDuelStart
            {
                ChallengerUnitId = Challenger.Guid,
                OpponentUnitId   = Opponent.Guid
            };
            Challenger.Session.EnqueueMessageEncrypted(start);
            Opponent.Session.EnqueueMessageEncrypted(start);
        }

        private void CheckArea(IPlayer player, double lastTick)
        {
            bool inside = Vector3.Distance(player.Position, center) <= AreaRadius;
            if (inside)
            {
                if (outside.Remove(player.Guid))
                    player.Session.EnqueueMessageEncrypted(new ServerDuelCancelWarning());
                return;
            }

            if (!outside.TryGetValue(player.Guid, out double time))
                player.Session.EnqueueMessageEncrypted(new ServerDuelLeftArea());

            time += lastTick;
            outside[player.Guid] = time;
            if (time >= LeaveAreaGrace)
                Finish(player == Challenger ? Opponent : Challenger, player, DuelFinishReason.Forfeited1);
        }

        private void Finish(IPlayer winner, IPlayer loser, DuelFinishReason reason)
        {
            state = DuelState.Finished;

            var result = new ServerDuelResult
            {
                WinnerUnitId = winner.Guid,
                LoserUnitId  = loser.Guid,
                Reason       = reason
            };
            foreach (IPlayer player in new[] { Challenger, Opponent })
            {
                if (player.Duel == this)
                    player.Duel = null;

                player.Session?.EnqueueMessageEncrypted(result);
            }

            // the fight is over, stop the two from chasing each other
            Challenger.ThreatManager?.RemoveHostile(Opponent.Guid);
            Opponent.ThreatManager?.RemoveHostile(Challenger.Guid);
        }
    }
}
