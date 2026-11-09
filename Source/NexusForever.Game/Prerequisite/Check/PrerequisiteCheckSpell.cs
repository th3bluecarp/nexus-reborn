using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    [PrerequisiteCheck(PrerequisiteType.UnderSpell)]
    public class PrerequisiteCheckSpell : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckSpell> log;

        public PrerequisiteCheckSpell(
            ILogger<PrerequisiteCheckSpell> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            if (value == 0 && objectId == 0)
                return false;

            uint? excludedCastingId = (parameters as PrerequisiteParameters)?.ExcludedSpellCastingId;
            bool hasSpell = player.HasSpell(
                spell => spell.CastingId != excludedCastingId && spell.Spell4Id == value,
                out ISpell _);

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return hasSpell;
                case PrerequisiteComparison.NotEqual:
                    return !hasSpell;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.UnderSpell}!");
                    return false;
            }
        }
    }
}
