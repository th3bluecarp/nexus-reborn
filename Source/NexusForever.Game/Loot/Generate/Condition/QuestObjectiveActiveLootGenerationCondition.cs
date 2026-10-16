using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Loot.Generate.Condition;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Quest;

namespace NexusForever.Game.Loot.Generate.Condition
{
    public class QuestObjectiveActiveLootGenerationCondition : ILootGenerationCondition
    {
        /// <summary>
        /// Checks if <see cref="IPlayer"/> has an accepted quest with the QuestObjective id in condition still incomplete,
        /// so quest items drop only for players who need them.
        /// </summary>
        public bool Meets(IPlayer player, uint condition)
        {
            foreach (IQuest quest in player.QuestManager.GetActiveQuests())
            {
                if (quest.State != QuestState.Accepted)
                    continue;

                foreach (IQuestObjective objective in quest)
                    if (objective.ObjectiveInfo.Entry.Id == condition && !objective.IsComplete())
                        return true;
            }

            return false;
        }
    }
}
