using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;
using NexusForever.Game.Static.Quest;
using NexusForever.GameTable;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Episode (zone storyline) state, ObjectId is the episode and Value a <see cref="QuestState"/> (3 = completed).
    /// </summary>
    /// <remarks>
    /// The episode state is derived from its quests: completed when every quest is completed, accepted when any quest is held or done.
    /// </remarks>
    [PrerequisiteCheck(PrerequisiteType.EpisodeState)]
    public class PrerequisiteCheckEpisodeState : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckEpisodeState> log;
        private readonly IGameTableManager gameTableManager;

        public PrerequisiteCheckEpisodeState(
            ILogger<PrerequisiteCheckEpisodeState> log,
            IGameTableManager gameTableManager)
        {
            this.log              = log;
            this.gameTableManager = gameTableManager;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            var states = gameTableManager.EpisodeQuest.Entries
                .Where(e => e.EpisodeId == objectId)
                .Select(e => player.QuestManager.GetQuestState((ushort)e.QuestId))
                .ToList();

            QuestState state = QuestState.Unknown;
            if (states.Count > 0 && states.All(s => s == QuestState.Completed))
                state = QuestState.Completed;
            else if (states.Any(s => s is QuestState.Accepted or QuestState.Achieved or QuestState.Completed))
                state = QuestState.Accepted;

            bool? result = PrerequisiteCompare.Is(comparison, state == (QuestState)value);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.EpisodeState}!");
            return result ?? false;
        }
    }
}
