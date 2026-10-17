using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Instance.Expedition.Gautlet
{
    /// <summary>
    /// Expedition: The Gauntlet (public event 446), chained from the objective texts: Pilot Taboro and the shuttle airlock, the switches
    /// out of the electrified room, the Swarm Pit (the Goon Squad and the Bash Brothers), the Faction Friction arena (The Handler and his
    /// pets), the Charnel Chamber (the Voodoo Kin, Slice and Dice, Pyro and Maniac), the Main Event (the Rockstar Yeti, the Championator,
    /// Showtime and the Shock King), then Judge Kain and Agent Lex. Arena waves, scoring and the opposing faction team need lost scripts
    /// and complete on their own, fighters that are not placed complete through the base.
    /// </summary>
    [ScriptFilterOwnerId(446)]
    public class GauntletEventScript : ChainedEventScript
    {
        protected override uint[][] Steps =>
        [
            [1821u],
            [1818u],
            [1819u],
            [1837u],
            [1842u],
            [1854u, 1869u, 1822u],
            [1857u],
            [1835u, 1870u, 1834u],
            [1859u],
            [1866u, 1871u, 1872u],
            [1858u],
            [1864u, 1865u, 1873u, 1874u, 1875u],
            [1896u, 1942u],
            [1914u, 1915u],
        ];

        protected override uint[] Auto => [1819u, 1854u, 1835u, 1875u, 1896u, 1942u];

        protected override Dictionary<uint, uint[]> Kills => new()
        {
            { 1822u, [48454u] },   // the Bash Brothers
            { 1834u, [48516u] },   // The Handler (his pets are not placed)
        };
    }
}
