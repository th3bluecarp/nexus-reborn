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
            // evaluated for a unit with a checklist objective (the fires on the Fiery Distraction huts), the client tests that
            // unit's QuestChecklistIdx bit in the progress of objective Value of the quest in the log (WildStar64.exe 0x14049f5b0)
            IQuestObjective checklist = parameters?.EvaluatedUnit is IWorldEntity unit && unit is not IPlayer
                ? player.QuestManager.GetActiveQuest((ushort)objectId)?.GetQuestObjectiveByIndex((byte)value)
                : null;
            if (parameters?.EvaluatedUnit != null && parameters.EvaluatedUnit is not IPlayer
                && (checklist?.ObjectiveInfo.Type ?? QuestObjectiveType.ActivateTargetGroupChecklist) == QuestObjectiveType.ActivateTargetGroupChecklist)
            {
                bool bit = checklist != null && parameters.EvaluatedUnit.QuestChecklistIdx < 32
                    && (checklist.Progress & (1u << parameters.EvaluatedUnit.QuestChecklistIdx)) != 0u;
                bool? bitResult = PrerequisiteCompare.Is(comparison, bit);
                return bitResult ?? false;
            }

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
