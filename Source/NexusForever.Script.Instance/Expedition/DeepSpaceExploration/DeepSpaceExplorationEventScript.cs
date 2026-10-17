using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Instance.Expedition.DeepSpaceExploration
{
    /// <summary>
    /// Expedition: Deep Space Exploration (public event 447), chained from the objective texts: Captain Tyrania on the Galactic
    /// Observer, the airlock, the Steel Serpent (escape the cells, fight the Steelfin, Captain Rotfin, sabotage the mainframe), back to
    /// the Starlab to rescue and talk to the crew, restore life support, the engine valves and Riptide, then stabilise the ship.
    /// Escaping the cells, infiltrating the command quarters, the maintenance drone and the stabilising wait complete on their own.
    /// </summary>
    [ScriptFilterOwnerId(447)]
    public class DeepSpaceExplorationEventScript : ChainedEventScript
    {
        protected override uint[][] Steps =>
        [
            [1843u],
            [1844u],
            [2000u],
            [1850u],
            [1979u, 1845u],
            [1846u, 1965u],
            [1999u, 1998u],
            [1849u],
            [2435u],
            [1847u, 1968u],
            [1966u, 1851u],
            [4683u],
            [1853u],
            [1852u],
            [2100u, 2118u],
        ];

        protected override uint[] Auto => [1979u, 1999u, 1966u, 1851u, 2118u];

        protected override Dictionary<uint, uint[]> Kills => new()
        {
            // Steelfin forces aboard the Steel Serpent
            { 1846u, [48740u, 49389u, 48742u, 49407u, 48738u, 48702u, 48762u, 48706u, 48705u, 48708u] },
            { 1998u, [48703u] },   // Captain Rotfin
        };
    }
}
