using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Instance.Expedition.SpaceMadness
{
    /// <summary>
    /// Expedition: Space Madness (public event 390), chained from the objective texts: talk to Captain Tero and Major Barmy, the
    /// observation deck, the hallucinating livestock, the lab and its hazmat closet, the airlock, the air scrubbers, the hallucinations,
    /// and the engineering computer. The hallucination waves and saving the workers need lost scripts and complete on their own.
    /// </summary>
    [ScriptFilterOwnerId(390)]
    public class SpaceMadnessEventScript : ChainedEventScript
    {
        protected override uint[][] Steps =>
        [
            [1579u],
            [1588u],
            [1589u],
            [1593u, 1590u],
            [1591u],
            [1592u, 4736u],
            [1656u],
            [1602u],
            [1595u, 2115u, 1662u],
            [1597u, 1641u, 1642u, 1643u],
            [1603u],
        ];

        protected override uint[] Auto => [1590u, 1656u, 2115u, 1662u, 1641u, 1642u, 1643u];

        protected override Dictionary<uint, uint[]> Kills => new()
        {
            { 1593u, [45729u, 45727u, 45726u, 45723u] },                              // hallucinating livestock
            { 1597u, [46132u, 46133u, 46135u, 46721u, 46722u, 46723u, 58798u] },     // nightmares
        };
    }
}
