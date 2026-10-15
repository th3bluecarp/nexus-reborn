using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    [PrerequisiteCheck(PrerequisiteType.IsPlayer)]
    public class PrerequisiteCheckIsPlayer : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckIsPlayer> log;

        public PrerequisiteCheckIsPlayer(
            ILogger<PrerequisiteCheckIsPlayer> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // Without an explicit target the unit being checked is the player itself.
            bool? result = PrerequisiteCompare.Is(comparison, parameters?.Target == null || parameters.Target is IPlayer);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.IsPlayer}!");
            return result ?? false;
        }
    }
}
