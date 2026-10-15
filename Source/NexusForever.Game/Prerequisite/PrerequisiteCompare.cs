using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite
{
    public static class PrerequisiteCompare
    {
        /// <summary>
        /// Compare an actual value against a prerequisite value, returns null for an unknown <see cref="PrerequisiteComparison"/>.
        /// </summary>
        public static bool? Compare(PrerequisiteComparison comparison, float actual, float value)
        {
            return comparison switch
            {
                PrerequisiteComparison.Equal              => actual == value,
                PrerequisiteComparison.NotEqual           => actual != value,
                PrerequisiteComparison.GreaterThanOrEqual => actual >= value,
                PrerequisiteComparison.GreaterThan        => actual > value,
                PrerequisiteComparison.LessThanOrEqual    => actual <= value,
                PrerequisiteComparison.LessThan           => actual < value,
                _                                         => null
            };
        }

        /// <summary>
        /// Apply an Equal or NotEqual comparison to a condition, returns null for any other <see cref="PrerequisiteComparison"/>.
        /// </summary>
        public static bool? Is(PrerequisiteComparison comparison, bool condition)
        {
            return comparison switch
            {
                PrerequisiteComparison.Equal    => condition,
                PrerequisiteComparison.NotEqual => !condition,
                _                               => null
            };
        }
    }
}
