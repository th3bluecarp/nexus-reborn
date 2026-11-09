using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// Unknown47: whether the caster has an active spell whose Spell4Base id is Value.
    /// </summary>
    /// <remarks>
    /// All 1,788 uses are Equal or NotEqual. ObjectId is zero in 1,650 uses, while Value matches a Spell4Base row in 1,420;
    /// this is the base-spell counterpart of UnderSpell (15), which compares an active Spell4 id.
    /// </remarks>
    [PrerequisiteCheck(PrerequisiteType.Unknown47)]
    public class PrerequisiteCheckUnknown47 : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckUnknown47> log;

        public PrerequisiteCheckUnknown47(
            ILogger<PrerequisiteCheckUnknown47> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // Visibility checks (Player.cs) evaluate with UnhandledResult = true; type 47 was unhandled there and so always
            // visible. Keep that until the aura reading is confirmed for phased creatures (2026-11-09).
            if (parameters?.UnhandledResult == true)
                return true;

            uint? excludedCastingId = (parameters as PrerequisiteParameters)?.ExcludedSpellCastingId;
            bool hasSpell = player != null && player.HasSpell(
                spell => spell.CastingId != excludedCastingId
                    && spell.Parameters?.SpellInfo?.BaseInfo?.Entry?.Id == value,
                out ISpell _);

            return comparison switch
            {
                PrerequisiteComparison.Equal    => hasSpell,
                PrerequisiteComparison.NotEqual => !hasSpell,
                _                               => Unhandled(comparison)
            };
        }

        private bool Unhandled(PrerequisiteComparison comparison)
        {
            log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Unknown47}!");
            return false;
        }
    }
}
