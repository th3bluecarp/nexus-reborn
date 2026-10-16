using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Abstract.PublicEvent;
using NexusForever.Game.Static.Prerequisite;
using NexusForever.Game.Static.PublicEvent;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// PublicEvent117: ObjectId a PublicEvent, Value 1 when it is running on the player's map, 0 when not.
    /// </summary>
    /// <remarks>
    /// 458 uses, ObjectId a PublicEvent id in 457, Value 0 or 1 with Equal and NotEqual. Read as running or not.
    /// </remarks>
    [PrerequisiteCheck(PrerequisiteType.PublicEvent117)]
    public class PrerequisiteCheckPublicEvent : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckPublicEvent> log;

        public PrerequisiteCheckPublicEvent(
            ILogger<PrerequisiteCheckPublicEvent> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            IPublicEvent publicEvent = player.Map?.PublicEventManager?.GetEvents().FirstOrDefault(e => e.Id == objectId);
            uint running = publicEvent != null && !publicEvent.HasFinished ? 1u : 0u;

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return running == value;
                case PrerequisiteComparison.NotEqual:
                    return running != value;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.PublicEvent117}!");
                    return false;
            }
        }
    }
}
