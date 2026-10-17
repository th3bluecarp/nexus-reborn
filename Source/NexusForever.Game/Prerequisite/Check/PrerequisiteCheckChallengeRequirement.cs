using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// A challenge, ObjectId is the Challenge id. Value 0 means the challenge is running, other values a completion count reached.
    /// </summary>
    /// <remarks>
    /// The meaning of Value is inferred from the data: 276 of 396 uses are Equal 0 on collect challenges whose objects should only
    /// be usable while the challenge runs (Bringin' the Bingberries).
    /// </remarks>
    [PrerequisiteCheck(PrerequisiteType.ChallengeRequirement)]
    public class PrerequisiteCheckChallengeRequirement : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckChallengeRequirement> log;

        public PrerequisiteCheckChallengeRequirement(
            ILogger<PrerequisiteCheckChallengeRequirement> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            bool meets = value == 0u
                ? player.ChallengeManager.IsActive(objectId)
                : player.ChallengeManager.GetCompletionCount(objectId) >= value;

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return meets;
                case PrerequisiteComparison.NotEqual:
                    return !meets;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.ChallengeRequirement}!");
                    return false;
            }
        }
    }
}
