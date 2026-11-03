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

        /// <summary>
        /// The quest state a prerequisite sees: a quest only mentioned by a communicator call, or abandoned, has not been
        /// started and reads as Unknown. Sergeant Kormak is visible while He's the Brains is untouched (state 0) or accepted
        /// (state 1); the call that offers it set the quest to Mentioned (5) and hid the receiver of four quests.
        /// </summary>
        private static QuestState EffectiveState(IPlayer player, uint objectId)
        {
            QuestState state = player.QuestManager.GetQuestState((ushort)objectId) ?? QuestState.Unknown;
            return state is QuestState.Mentioned or QuestState.Abandoned ? QuestState.Unknown : state;
        }

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            switch (comparison)
            {
                // a quest the player never had is in state 0 (Unknown): "quest X state = 0" means the player has not
                // touched it (Serve and Protect needs More Important Than Revenge untouched), null compared as never equal
                case PrerequisiteComparison.Equal:
                    return EffectiveState(player, objectId) == (QuestState)value;
                case PrerequisiteComparison.NotEqual:
                    return EffectiveState(player, objectId) != (QuestState)value;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.QuestState}!");
                    return false;
            }
        }
    }
}
