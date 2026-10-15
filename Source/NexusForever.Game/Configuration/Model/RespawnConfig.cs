using NexusForever.Shared.Configuration;

namespace NexusForever.Game.Configuration.Model
{
    /// <summary>
    /// Respawning of creatures spawned from the world database in the open world.
    /// </summary>
    [ConfigurationBind]
    public class RespawnConfig
    {
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Seconds a creature stays dead before it is replaced by a fresh copy at its spawn position.
        /// </summary>
        public double DelaySeconds { get; set; } = 60d;
    }
}
