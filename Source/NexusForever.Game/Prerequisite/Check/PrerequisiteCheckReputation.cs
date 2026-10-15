using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Reputation;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    [PrerequisiteCheck(PrerequisiteType.Reputation)]
    public class PrerequisiteCheckReputation : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckReputation> log;

        public PrerequisiteCheckReputation(
            ILogger<PrerequisiteCheckReputation> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // ObjectId is the faction and Value the reputation amount.
            float amount = player.ReputationManager.GetReputation((Faction)objectId)?.Amount ?? 0f;
            bool? result = PrerequisiteCompare.Compare(comparison, amount, value);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Reputation}!");
            return result ?? false;
        }
    }
}
