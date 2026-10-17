using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    /// <summary>
    /// An AMP in the player's active action set, ObjectId is the EldanAugmentation id. Gates the abilities AMPs unlock (Spectral Swarm, Mirage).
    /// </summary>
    [PrerequisiteCheck(PrerequisiteType.EldanAugmentation227)]
    public class PrerequisiteCheckEldanAugmentation227 : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckEldanAugmentation227> log;

        public PrerequisiteCheckEldanAugmentation227(
            ILogger<PrerequisiteCheckEldanAugmentation227> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            IActionSet actionSet = player.SpellManager?.GetActionSet(player.SpellManager.ActiveActionSet);
            bool has = actionSet?.GetAmp((ushort)objectId) != null;
            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return has;
                case PrerequisiteComparison.NotEqual:
                    return !has;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.EldanAugmentation227}!");
                    return false;
            }
        }
    }
}
