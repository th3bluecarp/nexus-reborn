using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// State of a path mission, ObjectId is the PathMission id and Value the <see cref="PathMissionState"/> (2 unlocked, 3 started, 4 complete).
    /// </summary>
    /// <remarks>
    /// Used for the visibility of path mission creatures, such as the Exile Sniper of Surveillance: Sniper Support.
    /// </remarks>
    [PrerequisiteCheck(PrerequisiteType.PathMission155)]
    public class PrerequisiteCheckPathMission155 : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckPathMission155> log;

        public PrerequisiteCheckPathMission155(
            ILogger<PrerequisiteCheckPathMission155> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            PathMissionState state = player.PathMissionManager.GetMissionState(objectId);
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return (uint)state == value;
                case PrerequisiteComparison.NotEqual:
                    return (uint)state != value;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.PathMission155}!");
                    return false;
            }
        }
    }
}
