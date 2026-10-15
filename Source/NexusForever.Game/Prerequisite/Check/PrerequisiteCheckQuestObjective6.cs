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
    [PrerequisiteCheck(PrerequisiteType.QuestObjective6)]
    public class PrerequisiteCheckQuestObjective6 : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckQuestObjective6> log;

        public PrerequisiteCheckQuestObjective6(
            ILogger<PrerequisiteCheckQuestObjective6> log)
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
                complete = quest?.GetQuestObjectiveByIndex((byte)value)?.IsComplete() ?? false;
            }

            bool? result = PrerequisiteCompare.Is(comparison, complete);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.QuestObjective6}!");
            return result ?? false;
        }
    }
}
