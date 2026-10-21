using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Death state: Value 0 alive, 1 dead (140 of 169 uses are Equal 0).
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.DeadState)]
    public class PrerequisiteCheckDeadState : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckDeadState> log;

        public PrerequisiteCheckDeadState(
            ILogger<PrerequisiteCheckDeadState> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            return PrerequisiteComparer.Compare(comparison, player.IsAlive ? 0 : 1, value) ?? Unhandled(comparison);
        }

        private bool Unhandled(PrerequisiteComparison comparison)
        {
            log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.DeadState}!");
            return false;
        }
    }
}
