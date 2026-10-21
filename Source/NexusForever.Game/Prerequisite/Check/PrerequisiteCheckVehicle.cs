using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Riding a vehicle: ObjectId the vehicle's creature (0 any vehicle), Equal = riding it, NotEqual = not. 44 uses, Value always -1.
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.Vehicle)]
    public class PrerequisiteCheckVehicle : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckVehicle> log;

        public PrerequisiteCheckVehicle(
            ILogger<PrerequisiteCheckVehicle> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            bool riding = player.PlatformGuid != null && (objectId == 0u || player.GetVisible<IWorldEntity>(player.PlatformGuid.Value)?.CreatureId == objectId);
            return comparison switch
            {
                PrerequisiteComparison.Equal    => riding,
                PrerequisiteComparison.NotEqual => !riding,
                _                               => Unhandled(comparison)
            };
        }

        private bool Unhandled(PrerequisiteComparison comparison)
        {
            log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Vehicle}!");
            return false;
        }
    }
}
