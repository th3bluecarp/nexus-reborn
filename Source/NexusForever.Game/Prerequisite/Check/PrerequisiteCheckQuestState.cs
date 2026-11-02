using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;
using NexusForever.Game.Static.Quest;

namespace NexusForever.Game.Prerequisite.Check
{
    [PrerequisiteCheck(PrerequisiteType.QuestState)]
    public class PrerequisiteCheckQuestState : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckQuestState> log;

        public PrerequisiteCheckQuestState(
            ILogger<PrerequisiteCheckQuestState> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            switch (comparison)
            {
                // a quest the player never had is in state 0 (Unknown): "quest X state = 0" means the player has not
                // touched it (Serve and Protect needs More Important Than Revenge untouched), null compared as never equal
                case PrerequisiteComparison.Equal:
                    return (player.QuestManager.GetQuestState((ushort)objectId) ?? QuestState.Unknown) == (QuestState)value;
                case PrerequisiteComparison.NotEqual:
                    return (player.QuestManager.GetQuestState((ushort)objectId) ?? QuestState.Unknown) != (QuestState)value;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.QuestState}!");
                    return false;
            }
        }
    }
}
