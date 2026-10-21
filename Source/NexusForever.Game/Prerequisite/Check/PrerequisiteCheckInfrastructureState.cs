using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// State of settler infrastructure project ObjectId (2 built, 1 under way, 0 not started) compared with Value.
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.InfrastructureState)]
    public class PrerequisiteCheckInfrastructureState : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckInfrastructureState> log;

        public PrerequisiteCheckInfrastructureState(
            ILogger<PrerequisiteCheckInfrastructureState> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            return PrerequisiteComparer.Compare(comparison, player.PathMissionManager.GetInfrastructureState(objectId), value) ?? Unhandled(comparison);
        }

        private bool Unhandled(PrerequisiteComparison comparison)
        {
            log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.InfrastructureState}!");
            return false;
        }
    }
}
