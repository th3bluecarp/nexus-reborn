using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    [PrerequisiteCheck(PrerequisiteType.IsCreature)]
    public class PrerequisiteCheckIsCreature : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckIsCreature> log;

        public PrerequisiteCheckIsCreature(
            ILogger<PrerequisiteCheckIsCreature> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // Value is the creature, zero for any creature.
            if (parameters?.Target == null)
                return parameters?.UnhandledResult ?? false;

            bool isCreature = parameters.Target is not IPlayer && (value == 0u || parameters.Target.CreatureId == value);
            bool? result = PrerequisiteCompare.Is(comparison, isCreature);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.IsCreature}!");
            return result ?? false;
        }
    }
}
