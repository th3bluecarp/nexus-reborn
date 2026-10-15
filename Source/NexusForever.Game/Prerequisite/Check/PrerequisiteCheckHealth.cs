using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    [PrerequisiteCheck(PrerequisiteType.Health)]
    public class PrerequisiteCheckHealth : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckHealth> log;

        public PrerequisiteCheckHealth(
            ILogger<PrerequisiteCheckHealth> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // Value is a health percentage.
            float percent = player.MaxHealth == 0u ? 0f : player.Health * 100f / player.MaxHealth;
            bool? result = PrerequisiteCompare.Compare(comparison, percent, value);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Health}!");
            return result ?? false;
        }
    }
}
