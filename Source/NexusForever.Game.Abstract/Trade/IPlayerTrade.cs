using NexusForever.Game.Abstract.Entity;

namespace NexusForever.Game.Abstract.Trade
{
    public interface IPlayerTrade
    {
        IPlayer Initiator { get; }
        IPlayer Target { get; }

        /// <summary>
        /// Returns if the invite was accepted and the trade window is open.
        /// </summary>
        bool IsActive { get; }

        void Accept(IPlayer player);
        void Decline(IPlayer player);
        void Cancel(IPlayer player);
        void AddItem(IPlayer player, ulong itemGuid);
        void RemoveItem(IPlayer player, ulong itemGuid);
        void SetMoney(IPlayer player, ulong credits);
        void Commit(IPlayer player);

        /// <summary>
        /// End the trade because a player left (logout, map change, out of range).
        /// </summary>
        void Abort();

        void Update(double lastTick);
    }
}
