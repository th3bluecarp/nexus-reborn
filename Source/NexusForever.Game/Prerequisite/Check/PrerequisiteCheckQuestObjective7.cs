using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.Game.Static.Prerequisite;
using NexusForever.GameTable;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Type 107 (named QuestObjective7 in the client's error text) with a PathMission id as ObjectId: the visibility prerequisite
    /// of mission units pairs it with PathMission155 (Rockridge Recovery's Dead Explorers: state equal 3 and 107 not-equal 0).
    /// Read as a mission state comparison; with any other object id it is unhandled as before (SEMANTICS: path missions start at their indicators).
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.QuestObjective7)]
    public class PrerequisiteCheckQuestObjective7 : IPrerequisiteCheck
    {
        #region Dependency Injection
        private readonly ILogger<PrerequisiteCheckQuestObjective7> log;

        public PrerequisiteCheckQuestObjective7(
            ILogger<PrerequisiteCheckQuestObjective7> log)
        {
            this.log = log;
        }
        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            if (GameTableManager.Instance.PathMission.GetEntry(objectId) == null)
            {
                log.LogWarning($"Unhandled PrerequisiteType {PrerequisiteType.QuestObjective7} for object {objectId}!");
                return parameters?.UnhandledResult ?? false;
            }

            PathMissionState state = player.PathMissionManager.GetMissionState(objectId);
            return comparison switch
            {
                PrerequisiteComparison.Equal    => (uint)state == value,
                PrerequisiteComparison.NotEqual => (uint)state != value,
                _                               => false
            };
        }
    }
}
