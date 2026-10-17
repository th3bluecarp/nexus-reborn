using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Instance.Expedition.RageLogic
{
    /// <summary>
    /// Expedition: Rage Logic (public event 214, the run itself, 213 only holds the vehicle choice), chained from the objective texts:
    /// obliterate the Ragebots on the asteroid and destroy its engines, get into the Ragebot factory and disable it, restore the
    /// Freebots and take out the factory's defenders, hack the signal relay, the server room, Axiom's control room, the broadcast
    /// transmitter and Axiom. Restoring the Freebots and the transmitter need lost scripts and complete on their own, the hoverbikes are
    /// not used.
    /// </summary>
    [ScriptFilterOwnerId(214)]
    public class RageLogicEventScript : ChainedEventScript
    {
        private static readonly uint[] Ragebots = [43273u, 32302u, 43276u, 32325u, 32324u, 32303u];

        protected override uint[][] Steps =>
        [
            [783u, 784u],
            [786u],
            [787u, 788u, 789u],
            [1529u],
            [790u],
            [5107u],
            [4691u],
            [791u],
        ];

        protected override uint[] Auto => [788u, 4691u];

        protected override Dictionary<uint, uint[]> Kills => new()
        {
            { 783u, Ragebots },     // Ragebots defending the asteroid
            { 789u, Ragebots },     // Ragebots defending the factory
            { 791u, [32326u] },     // Axiom
        };
    }
}
