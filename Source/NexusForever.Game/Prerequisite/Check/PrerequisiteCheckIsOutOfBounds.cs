using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Whether the player is outside the WorldLocation2 named by ObjectId. Value is 1 outside and 0 inside.
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.IsOutOfBounds)]
    public class PrerequisiteCheckIsOutOfBounds : IPrerequisiteCheck
    {
        private const float MinimumLocationRadius = 10f;

        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckIsOutOfBounds> log;
        private readonly IGameTableManager gameTableManager;

        public PrerequisiteCheckIsOutOfBounds(
            ILogger<PrerequisiteCheckIsOutOfBounds> log,
            IGameTableManager gameTableManager)
        {
            this.log              = log;
            this.gameTableManager = gameTableManager;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // Visibility checks pass UnhandledResult = true; this type was unhandled there before 2026-11-09, keep it visible.
            if (parameters?.UnhandledResult == true)
                return true;

            if (objectId == 0u)
            {
                log.LogDebug($"{PrerequisiteType.IsOutOfBounds} has no WorldLocation2 ObjectId; treating Equal 0 as met.");
                return comparison == PrerequisiteComparison.Equal && value == 0u;
            }

            WorldLocation2Entry location = gameTableManager.WorldLocation2.GetEntry(objectId);
            if (location == null)
            {
                log.LogDebug($"{PrerequisiteType.IsOutOfBounds} references missing WorldLocation2 {objectId}; treating Equal 0 as met.");
                return comparison == PrerequisiteComparison.Equal && value == 0u;
            }

            bool inside = player?.Map != null && location.WorldId == player.Map.Entry.Id;
            if (inside)
            {
                float dx = player.Position.X - location.Position0;
                float dz = player.Position.Z - location.Position2;
                float radius = Math.Max(location.Radius, MinimumLocationRadius);
                inside = dx * dx + dz * dz <= radius * radius;
            }

            uint outOfBounds = inside ? 0u : 1u;
            return comparison switch
            {
                PrerequisiteComparison.Equal    => outOfBounds == value,
                PrerequisiteComparison.NotEqual => outOfBounds != value,
                _                               => Unhandled(comparison)
            };
        }

        private bool Unhandled(PrerequisiteComparison comparison)
        {
            log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.IsOutOfBounds}!");
            return false;
        }
    }
}
