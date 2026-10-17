using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// The world (map) the player is in, ObjectId is the World id.
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.WorldRequirement)]
    public class PrerequisiteCheckWorldRequirement : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckWorldRequirement> log;

        public PrerequisiteCheckWorldRequirement(
            ILogger<PrerequisiteCheckWorldRequirement> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            uint worldId = player.Map?.Entry.Id ?? 0u;
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return worldId == objectId;
                case PrerequisiteComparison.NotEqual:
                    return worldId != objectId;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.WorldRequirement}!");
                    return false;
            }
        }
    }
}
