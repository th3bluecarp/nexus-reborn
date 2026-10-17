using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Instance.Expedition.Infestation
{
    /// <summary>
    /// Expedition: Infestation (public event 95) aboard the X-82 cargo ship, chained from the objective texts: board the ship, close the
    /// vents and seal the hull breaches, find the medical bay and its supplies, clear the Lumbering Parasites and kill the Cyclopean
    /// Parasite. Rescuing the shiphands, healing them and the medbay attack complete on their own (the attack's Lashing Fiends exist
    /// only as level 50 veterans). Tagging cargo is optional and not activated.
    /// </summary>
    [ScriptFilterOwnerId(95)]
    public class InfestationEventScript : ChainedEventScript
    {
        protected override uint[][] Steps =>
        [
            [1005u],
            [228u, 1007u, 1006u],
            [1438u],
            [230u],
            [231u, 4698u],
            [229u],
            [1500u],
        ];

        protected override uint[] Auto => [1006u, 231u, 4698u];

        protected override Dictionary<uint, uint[]> Kills => new()
        {
            { 229u, [22669u] },     // Lumbering Parasites
            { 1500u, [26833u] },    // Cyclopean Parasite
        };
    }
}
