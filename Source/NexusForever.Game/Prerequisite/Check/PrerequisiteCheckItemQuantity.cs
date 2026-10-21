using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Number of item ObjectId carried, compared with Value (Equal 0 = carries none).
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.ItemQuantity)]
    public class PrerequisiteCheckItemQuantity : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckItemQuantity> log;

        public PrerequisiteCheckItemQuantity(
            ILogger<PrerequisiteCheckItemQuantity> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            return PrerequisiteComparer.Compare(comparison, player.Inventory.GetItemCount(objectId), value) ?? Unhandled(comparison);
        }

        private bool Unhandled(PrerequisiteComparison comparison)
        {
            log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.ItemQuantity}!");
            return false;
        }
    }
}
