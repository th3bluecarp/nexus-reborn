using NexusForever.Shared.Configuration;

namespace NexusForever.Game.Configuration.Model
{
    /// <summary>
    /// Experience rewarded for killing creatures.
    /// </summary>
    /// <remarks>
    /// Retail calculated kill experience server side and the formula was never recovered, these values are a tunable
    /// approximation built on client game table data (quest experience per level and creature rank multipliers).
    /// </remarks>
    [ConfigurationBind]
    public class KillXpConfig
    {
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Fraction of a same level quest's base experience (XpPerLevel.BaseQuestXpPerLevel) rewarded for a rank 1.0 creature.
        /// </summary>
        public float QuestXpFraction { get; set; } = 0.1f;

        /// <summary>
        /// Multiplier for creatures without a rank (Creature2DifficultyId 0), creatures with a rank use the rank's unit property multiplier.
        /// </summary>
        public float NoRankMultiplier { get; set; } = 1f;

        /// <summary>
        /// Bonus per level the creature is above the player, up to <see cref="LevelAboveMaxLevels"/> levels.
        /// </summary>
        public float LevelAboveBonusPerLevel { get; set; } = 0.1f;
        public uint LevelAboveMaxLevels { get; set; } = 3u;

        /// <summary>
        /// Penalty per level the creature is below the player, creatures <see cref="GrayLevels"/> or more levels below reward nothing.
        /// </summary>
        public float LevelBelowPenaltyPerLevel { get; set; } = 0.2f;
        public uint GrayLevels { get; set; } = 5u;
    }
}
