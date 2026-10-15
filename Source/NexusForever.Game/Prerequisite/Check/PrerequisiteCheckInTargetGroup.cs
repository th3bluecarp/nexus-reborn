using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    [PrerequisiteCheck(PrerequisiteType.InTargetGroup)]
    public class PrerequisiteCheckInTargetGroup : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckInTargetGroup> log;

        public PrerequisiteCheckInTargetGroup(
            ILogger<PrerequisiteCheckInTargetGroup> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // Value is the target group.
            if (parameters?.Target == null)
                return parameters?.UnhandledResult ?? false;

            bool inGroup = AssetManager.Instance.GetTargetGroupsForCreatureId(parameters.Target.CreatureId).Contains(value);
            bool? result = PrerequisiteCompare.Is(comparison, inGroup);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.InTargetGroup}!");
            return result ?? false;
        }
    }
}
