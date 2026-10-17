using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Whether a challenge is locked for the player, ObjectId is the Challenge id. A challenge unlocks in its area.
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.ChallengeLocked)]
    public class PrerequisiteCheckChallengeLocked : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckChallengeLocked> log;

        public PrerequisiteCheckChallengeLocked(
            ILogger<PrerequisiteCheckChallengeLocked> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            bool locked = !player.ChallengeManager.IsUnlocked(objectId);
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return locked;
                case PrerequisiteComparison.NotEqual:
                    return !locked;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.ChallengeLocked}!");
                    return false;
            }
        }
    }
}
