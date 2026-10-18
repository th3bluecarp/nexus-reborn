using NexusForever.Database.Auth;
using NexusForever.Game.Abstract.Entity;

namespace NexusForever.Game.Abstract.Account.DailyLogin
{
    public interface IAccountDailyLoginManager : IDatabaseAuth
    {
        /// <summary>
        /// Days the account logged in, the position on the daily login reward calendar.
        /// </summary>
        uint LoginDays { get; }

        /// <summary>
        /// Invoked when a character of the account enters the world, the first time each day counts the day and grants its rewards.
        /// </summary>
        void OnEnteredWorld(IPlayer player);

        /// <summary>
        /// Send the login days and the time to the next reward.
        /// </summary>
        void SendUpdate();
    }
}
