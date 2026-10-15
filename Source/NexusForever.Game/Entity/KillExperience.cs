using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Configuration.Model;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Shared.Configuration;

namespace NexusForever.Game.Entity
{
    public static class KillExperience
    {
        /// <summary>
        /// Return the experience <see cref="IPlayer"/> earns for killing <see cref="IUnitEntity"/>, see <see cref="KillXpConfig"/>.
        /// </summary>
        public static uint Calculate(IUnitEntity victim, IPlayer player)
        {
            KillXpConfig config = SharedConfiguration.Instance.Get<RealmConfig>()?.KillXp ?? new KillXpConfig();
            if (!config.Enable)
                return 0u;

            if (victim is IPlayer || victim.CreatureInfo?.Entry == null)
                return 0u;

            XpPerLevelEntry levelEntry = GameTableManager.Instance.XpPerLevel.GetEntry(Math.Max(1u, victim.Level));
            if (levelEntry == null)
                return 0u;

            float rank = victim.CreatureInfo.DifficultyEntry?.UnitPropertyMultiplier is { Length: > 0 } multipliers
                ? multipliers[0]
                : config.NoRankMultiplier;

            int difference = (int)victim.Level - (int)player.Level;
            float levelModifier;
            if (difference >= 0)
                levelModifier = 1f + config.LevelAboveBonusPerLevel * Math.Min(difference, (int)config.LevelAboveMaxLevels);
            else if (-difference >= config.GrayLevels)
                levelModifier = 0f;
            else
                levelModifier = Math.Max(0f, 1f + config.LevelBelowPenaltyPerLevel * difference);

            return (uint)Math.Round(levelEntry.BaseQuestXpPerLevel * config.QuestXpFraction * rank * levelModifier);
        }
    }
}
