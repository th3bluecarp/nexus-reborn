using NexusForever.Shared.Configuration;

namespace NexusForever.Game.Configuration.Model
{
    /// <summary>
    /// Path XP for a completed path mission: Base + PerLevel x character level.
    /// </summary>
    /// <remarks>
    /// Retail mission XP is not in the client data. The defaults put a player near path level 10 by character level 15,
    /// following the PathLevel curve (about 30 x (level - 1)^2).
    /// </remarks>
    [ConfigurationBind]
    public class PathMissionXpConfig
    {
        public uint Base { get; set; } = 25u;
        public uint PerLevel { get; set; } = 5u;
    }
}
