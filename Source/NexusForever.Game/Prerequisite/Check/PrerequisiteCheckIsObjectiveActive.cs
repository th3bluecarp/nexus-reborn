using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Quest objective can currently be progressed.
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.IsObjectiveActive)]
    public class PrerequisiteCheckIsObjectiveActive : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckIsObjectiveActive> log;

        public PrerequisiteCheckIsObjectiveActive(
            ILogger<PrerequisiteCheckIsObjectiveActive> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // ObjectId is the quest and Value the objective index. A zero quest refers to a context quest we don't track.
            if (objectId == 0)
                return parameters?.UnhandledResult ?? false;

            IQuest quest = player.QuestManager.GetActiveQuest((ushort)objectId);
            bool? result = PrerequisiteCompare.Is(comparison, quest?.IsObjectiveActive((byte)value) ?? false);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.IsObjectiveActive}!");
            return result ?? false;
        }
    }
}
