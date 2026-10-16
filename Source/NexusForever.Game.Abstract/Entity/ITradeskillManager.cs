using NexusForever.Database.Character;
using NexusForever.Game.Static.Crafting;

namespace NexusForever.Game.Abstract.Entity
{
    public interface ITradeskillManager : IDatabaseCharacter
    {
        void SendInitialPackets();

        /// <summary>
        /// Learn a tradeskill, dropping another active crafting tradeskill when the limit is reached.
        /// </summary>
        void Learn(TradeskillType toLearn, TradeskillType toDrop);

        /// <summary>
        /// Returns if the player knows the tradeskill of the supplied TradeskillTier and has reached that tier.
        /// </summary>
        bool HasReachedTier(uint tradeskillTierId);
    }
}
