using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Unknown50: like UnderSpell (15) but on the prerequisite target when there is one, else the player.
    /// </summary>
    /// <remarks>
    /// 450 uses with ObjectId 0, Value a Spell4 id for 336 of 352 values (Exhausted: Combat Stealth, Overdrive) and Equal or
    /// NotEqual comparisons, the shape of UnderSpell; that it looks at the target is a guess.
    /// </remarks>
    [PrerequisiteCheck(PrerequisiteType.Unknown50)]
    public class PrerequisiteCheckTargetUnderSpell : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckTargetUnderSpell> log;

        public PrerequisiteCheckTargetUnderSpell(
            ILogger<PrerequisiteCheckTargetUnderSpell> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            if (value == 0u)
                return false;

            IUnitEntity unit = parameters?.Target ?? player;
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return unit.HasSpell(s => s.Spell4Id == value, out ISpell _);
                case PrerequisiteComparison.NotEqual:
                    return !unit.HasSpell(s => s.Spell4Id == value, out ISpell _);
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Unknown50}!");
                    return false;
            }
        }
    }
}
