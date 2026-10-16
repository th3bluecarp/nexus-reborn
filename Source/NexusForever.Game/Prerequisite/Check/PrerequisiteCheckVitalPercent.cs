using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Unknown71: ObjectId is a <see cref="Vital"/>, Value a percentage of its maximum, on the prerequisite target or the player.
    /// </summary>
    /// <remarks>
    /// 166 uses; ObjectIds 1 (Health), 3 (ShieldCapacity), 6 to 9 (class resources) and 15 (Focus) match the Vital enum,
    /// values 0 to 100 and numeric comparisons. Which maximum a class resource uses is a guess (ResourceMax0 for Resource1),
    /// a resource with no known maximum is compared as is.
    /// </remarks>
    [PrerequisiteCheck(PrerequisiteType.Unknown71)]
    public class PrerequisiteCheckVitalPercent : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckVitalPercent> log;

        public PrerequisiteCheckVitalPercent(
            ILogger<PrerequisiteCheckVitalPercent> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            IUnitEntity unit = parameters?.Target ?? player;
            var vital = (Vital)objectId;

            float current = vital == Vital.Health ? unit.Health : unit.GetVitalValue(vital);
            float max = vital switch
            {
                Vital.Health                                  => unit.MaxHealth,
                Vital.ShieldCapacity                          => unit.GetPropertyValue(Property.ShieldCapacityMax),
                Vital.Focus                                   => unit.GetPropertyValue(Property.BaseFocusPool),
                >= Vital.Resource1 and <= Vital.Resource6     => unit.GetPropertyValue((Property)((int)Property.ResourceMax0 + (int)(vital - Vital.Resource1))),
                _                                             => 0f
            };
            float percent = max > 0f ? current / max * 100f : current;

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return MathF.Round(percent) == value;
                case PrerequisiteComparison.NotEqual:
                    return MathF.Round(percent) != value;
                case PrerequisiteComparison.GreaterThanOrEqual:
                    return percent >= value;
                case PrerequisiteComparison.GreaterThan:
                    return percent > value;
                case PrerequisiteComparison.LessThanOrEqual:
                    return percent <= value;
                case PrerequisiteComparison.LessThan:
                    return percent < value;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Unknown71}!");
                    return false;
            }
        }
    }
}
