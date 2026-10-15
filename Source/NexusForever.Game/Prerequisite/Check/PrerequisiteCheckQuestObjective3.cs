using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Prerequisite;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Prerequisite.Check
{
    [PrerequisiteCheck(PrerequisiteType.QuestObjective3)]
    public class PrerequisiteCheckQuestObjective3 : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckQuestObjective3> log;

        public PrerequisiteCheckQuestObjective3(
            ILogger<PrerequisiteCheckQuestObjective3> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // ObjectId is the quest, Value is compared against the progress of objective 3.
            uint progress;
            if (player.QuestManager.GetQuestState((ushort)objectId) == QuestState.Completed)
            {
                // completed quests are no longer tracked, every objective counts as fully progressed
                Quest2Entry entry = GameTableManager.Instance.Quest2.GetEntry(objectId);
                uint objectiveId = entry != null && entry.Objectives.Length > 3 ? entry.Objectives[3] : 0u;
                progress = GameTableManager.Instance.QuestObjective.GetEntry(objectiveId)?.Count ?? 0u;
            }
            else
            {
                IQuest quest = player.QuestManager.GetActiveQuest((ushort)objectId);
                IQuestObjective objective = quest?.GetQuestObjectiveByIndex(3);
                progress = objective?.Progress ?? 0u;
            }

            bool? result = PrerequisiteCompare.Compare(comparison, progress, value);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.QuestObjective3}!");
            return result ?? false;
        }
    }
}
