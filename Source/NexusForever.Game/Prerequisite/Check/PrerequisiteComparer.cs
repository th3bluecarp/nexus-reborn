using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Numeric comparison shared by the prerequisite checks that compare a count or level with the prerequisite value.
    /// </summary>
    internal static class PrerequisiteComparer
    {
        public static bool? Compare(PrerequisiteComparison comparison, long actual, long value)
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
    }
}
