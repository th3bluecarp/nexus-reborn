using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Level of the active path compared with Value.
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.PathLevel)]
    public class PrerequisiteCheckPathLevel : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckPathLevel> log;

        public PrerequisiteCheckPathLevel(
            ILogger<PrerequisiteCheckPathLevel> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            return PrerequisiteComparer.Compare(comparison, player.PathManager.GetLevel(), value) ?? Unhandled(comparison);
        }

        private bool Unhandled(PrerequisiteComparison comparison)
        {
            log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.PathLevel}!");
            return false;
        }
    }
}
