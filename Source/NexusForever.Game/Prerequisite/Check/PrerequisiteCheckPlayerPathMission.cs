using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Whether the player has a path mission, ObjectId is the PathMission id.
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.PlayerPathMission)]
    public class PrerequisiteCheckPlayerPathMission : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckPlayerPathMission> log;

        public PrerequisiteCheckPlayerPathMission(
            ILogger<PrerequisiteCheckPlayerPathMission> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            bool has = player.PathMissionManager.GetMissionState(objectId) != PathMissionState.NoMission;
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return has;
                case PrerequisiteComparison.NotEqual:
                    return !has;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.PlayerPathMission}!");
                    return false;
            }
        }
    }
}
