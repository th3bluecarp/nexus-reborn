using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Instance.Expedition.FragmentZero
{
    /// <summary>
    /// Expedition: They Came from Fragment Zero! (public event 680), chained from the objective texts: follow the friendly Skeech to the
    /// Incubation Complex and its three prototypes, then the Biomatics Chamber and Project "Matron", then the Life-Overseer's Creche,
    /// find Captain Hugo and get him back to the ship. The open searches, the Skeech ambush, staying close to Hugo, the black box and
    /// the final wait need lost scripts and complete on their own.
    /// </summary>
    [ScriptFilterOwnerId(680)]
    public class FragmentZeroEventScript : ChainedEventScript
    {
        protected override uint[][] Steps =>
        [
            [4397u],
            [4422u],
            [4421u, 4425u],
            [4423u, 4449u, 4450u],
            [4424u],
            [4427u],
            [4426u],
            [4420u],
            [4417u],
            [4432u],
            [4418u],
            [4429u],
            [4430u],
            [4443u],
            [4444u],
            [4445u],
            [4446u],
            [4641u],
            [4447u, 4693u, 4655u],
            [4642u],
        ];

        protected override uint[] Auto => [4397u, 4421u, 4425u, 4420u, 4447u, 4693u, 4655u];
    }
}
