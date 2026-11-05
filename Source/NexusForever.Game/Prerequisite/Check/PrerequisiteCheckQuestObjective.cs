using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Quest objective is complete.
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.QuestObjective)]
    public class PrerequisiteCheckQuestObjective : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckQuestObjective> log;

        public PrerequisiteCheckQuestObjective(
            ILogger<PrerequisiteCheckQuestObjective> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // ObjectId is the quest and Value the objective index, met when that objective is complete.
            bool complete;
            if (player.QuestManager.GetQuestState((ushort)objectId) == QuestState.Completed)
                complete = true;
            else
            {
                IQuest quest = player.QuestManager.GetActiveQuest((ushort)objectId);
                IQuestObjective objective = quest?.GetQuestObjectiveByIndex((byte)value);
                complete = objective?.IsComplete() ?? false;
                if (complete && objective.ObjectiveInfo.IsHidden() && objective.ObjectiveInfo.Entry.Data == 0u
                    && quest.Any(other => other.Index < objective.Index && !other.ObjectiveInfo.IsHidden() && !other.IsComplete()))
                    complete = false;
            }

            bool? result = PrerequisiteCompare.Is(comparison, complete);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.QuestObjective}!");
            return result ?? false;
        }
    }
}
