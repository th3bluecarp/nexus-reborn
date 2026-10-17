using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Instance.Expedition.OutpostM13
{
    /// <summary>
    /// Expedition: Outpost M-13 (public event 108), chained from the objective texts: talk to Captain Milo, go to the cargo hold and
    /// fight off the Novaburn boarders and "Ransacker" Rorgh, leave the ship, find Foreman Krause, gather outside the mine, clear the
    /// hive and kill the Hive Queen, then head back to the shuttle. The Novaburn waves on M-13, the datachrons and the mine search need
    /// lost scripts or creatures that are not placed and complete on their own.
    /// </summary>
    [ScriptFilterOwnerId(108)]
    public class OutpostM13EventScript : ChainedEventScript
    {
        protected override uint[][] Steps =>
        [
            [1441u],
            [1442u],
            [253u, 254u],
            [262u],
            [1440u],
            [656u, 1439u, 4733u, 4734u, 4697u, 4700u],
            [4701u],
            [256u],
            [257u],
            [5134u],
        ];

        protected override uint[] Auto => [656u, 1439u, 4733u, 4734u, 4697u, 4700u];

        protected override Dictionary<uint, uint[]> Kills => new()
        {
            { 253u, [23453u, 23454u] },                          // Novaburn Corsairs and Plunderers
            { 254u, [23455u] },                                  // "Ransacker" Rorgh
            { 256u, [23514u, 23500u, 23536u, 29703u, 29612u] },  // Hive Pods, Infected Miners, Hive Infectors
            { 257u, [23513u] },                                  // Hive Queen
        };
    }
}
