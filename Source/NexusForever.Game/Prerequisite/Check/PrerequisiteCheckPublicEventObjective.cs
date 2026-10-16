using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Abstract.PublicEvent;
using NexusForever.Game.Static.Prerequisite;
using NexusForever.Game.Static.PublicEvent;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// PublicEventObjective188 (type 118): ObjectId a PublicEventObjective, Value its <see cref="PublicEventStatus"/>.
    /// </summary>
    /// <remarks>
    /// 2,422 uses, ObjectId a PublicEventObjective id in 2,392, Values 1 (1,689), 2 (658), 0 (43), 3 (32) match
    /// Inactive, Active, Succeeded, Failed. An objective of an event that is not running counts as Inactive.
    /// </remarks>
    [PrerequisiteCheck(PrerequisiteType.PublicEventObjective188)]
    public class PrerequisiteCheckPublicEventObjective : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckPublicEventObjective> log;

        public PrerequisiteCheckPublicEventObjective(
            ILogger<PrerequisiteCheckPublicEventObjective> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            PublicEventStatus status = PublicEventStatus.Inactive;
            foreach (IPublicEvent publicEvent in player.Map?.PublicEventManager?.GetEvents() ?? [])
            {
                IPublicEventObjective objective = publicEvent.GetTeams()
                    .SelectMany(t => t.GetObjectives())
                    .FirstOrDefault(o => o.Entry.Id == objectId);
                if (objective == null)
                    continue;

                status = objective.Status;
                break;
            }

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return (uint)status == value;
                case PrerequisiteComparison.NotEqual:
                    return (uint)status != value;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.PublicEventObjective188}!");
                    return false;
            }
        }
    }
}
