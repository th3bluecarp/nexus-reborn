using NexusForever.Game.Abstract.Entity;

namespace NexusForever.Game.Abstract.Pvp
{
    public interface IDuel
    {
        IPlayer Challenger { get; }
        IPlayer Opponent { get; }

        /// <summary>
        /// Returns if the countdown is over and the two players can fight.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Returns if the supplied <see cref="IPlayer"/> takes part in this duel.
        /// </summary>
        bool Involves(IPlayer player);

        /// <summary>
        /// Opponent accepts the challenge, the countdown starts.
        /// </summary>
        void Accept();

        /// <summary>
        /// Opponent declines the challenge.
        /// </summary>
        void Decline();

        /// <summary>
        /// The supplied <see cref="IPlayer"/> gives up.
        /// </summary>
        void Forfeit(IPlayer player);

        /// <summary>
        /// The supplied <see cref="IPlayer"/> was brought to 1 health by the other.
        /// </summary>
        void Defeat(IPlayer loser);

        /// <summary>
        /// End the duel without a winner (logout, map change).
        /// </summary>
        void Cancel();

        void Update(double lastTick);
    }
}
