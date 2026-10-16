using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// TradeSkillProfession: ObjectId a TradeskillTier, compared as 1 when the player has reached that tier of its tradeskill, else 0.
    /// </summary>
    /// <remarks>
    /// 268 uses, ObjectIds are TradeskillTier ids (5 Weaponsmith tier 5, 34 Technologist tier 1), Value 0 with GreaterThan (181) or
    /// LessThanOrEqual (21) and Value 1 with Equal (66): all read as reached or not reached.
    /// </remarks>
    [PrerequisiteCheck(PrerequisiteType.TradeSkillProfession)]
    public class PrerequisiteCheckTradeskillProfession : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckTradeskillProfession> log;

        public PrerequisiteCheckTradeskillProfession(
            ILogger<PrerequisiteCheckTradeskillProfession> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            uint reached = player.TradeskillManager.HasReachedTier(objectId) ? 1u : 0u;
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return reached == value;
                case PrerequisiteComparison.NotEqual:
                    return reached != value;
                case PrerequisiteComparison.GreaterThan:
                    return reached > value;
                case PrerequisiteComparison.GreaterThanOrEqual:
                    return reached >= value;
                case PrerequisiteComparison.LessThan:
                    return reached < value;
                case PrerequisiteComparison.LessThanOrEqual:
                    return reached <= value;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.TradeSkillProfession}!");
                    return false;
            }
        }
    }
}
