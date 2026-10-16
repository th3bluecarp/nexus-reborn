using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Inventory (246): Value an Item2 id, Equal when the player carries it, NotEqual when not.
    /// </summary>
    /// <remarks>
    /// 793 uses, all NotEqual, Value an item in 791 (Hoverboard (Flux), Bag of Awesomeness, titles): the check that stops a
    /// player buying an unlock twice. ObjectId is set on 60 rows and its meaning is unknown, it is ignored.
    /// </remarks>
    [PrerequisiteCheck(PrerequisiteType.Inventory)]
    public class PrerequisiteCheckInventory : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckInventory> log;

        public PrerequisiteCheckInventory(
            ILogger<PrerequisiteCheckInventory> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            bool has = player.Inventory.GetItemCount(value) > 0u;
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return has;
                case PrerequisiteComparison.NotEqual:
                    return !has;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Inventory}!");
                    return false;
            }
        }
    }
}
