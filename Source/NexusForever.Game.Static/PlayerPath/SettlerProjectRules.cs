namespace NexusForever.Game.Static.PlayerPath
{
    /// <summary>
    /// Resource creatures of settler projects (PathSettlerInfrastructure) that name none.
    /// </summary>
    /// <remarks>
    /// Most projects leave Creature2IdResource empty: retail tied the resource objects to the project on the server and that
    /// link is lost. The Creature2 table still describes each object as the resource of one zone tract ("Tempest Tech Scraps -
    /// Galeras T1 - Settler Infrastructure Resource"), the list below is read from those descriptions, zone by zone. The world
    /// server (PathMissionManager.Infrastructure) and the test bot (NexusForever.Bot) read this one list before they fall back
    /// to the creature named in the objective text.
    /// </remarks>
    public static class SettlerProjectRules
    {
        /// <summary>
        /// Resource creatures by PathSettlerInfrastructure id.
        /// </summary>
        public static IReadOnlyDictionary<uint, uint[]> ResourceCreatures { get; } = new Dictionary<uint, uint[]>
        {
            // Galeras (the first three are placed by data/missions.sql at their archived pins)
            [38u] = [34421u], // PROJECT: Tempest Tech Station (1871), Tempest Tech Scraps
            [39u] = [34560u], // PROJECT: Supply Teleporter (1873), Exile Transportation Part
            [40u] = [34569u], // PROJECT: Medical Station (1874), Medical Supplies
            [43u] = [34996u], // PROJECT: Oakby Farm (1884), Farmhouse Debris
        };
    }
}
