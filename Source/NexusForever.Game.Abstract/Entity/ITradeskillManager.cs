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

        /// <summary>
        /// Craft a schematic count times, consuming materials from the supply satchel and bags.
        /// </summary>
        void Craft(uint tradeskillSchematic2Id, uint count);

        /// <summary>
        /// Learn a schematic from a recipe item (GiveSchematic).
        /// </summary>
        void LearnSchematic(uint tradeskillSchematic2Id);

        /// <summary>
        /// Invoked when the player destroys a creature, gathering tradeskills earn xp from harvest nodes.
        /// </summary>
        void OnKill(uint creatureId);

        /// <summary>
        /// Add xp to a learned tradeskill (quest TradeSkillXp rewards).
        /// </summary>
        void AddXp(TradeskillType type, uint xp);
    }
}
