using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;
using NexusForever.Shared;

namespace NexusForever.Script.Main.Celestion
{
    /// <summary>
    /// Jumpshroom (37703) in Celestion: stepping on it launches the player with the Celestion Jumpshroom spell (40649).
    /// </summary>
    /// <remarks>
    /// Stand in for the lost retail trigger. The creature has no activate spell, the launch spell is named after it in the client data.
    /// Range and the per player cooldown are guesses.
    /// </remarks>
    [ScriptFilterCreatureId(37703)]
    public class JumpshroomEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private const uint JumpSpell4Id = 40649;
        private const float TriggerRange = 2.5f;
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(2d);

        private IWorldEntity owner;
        private readonly Dictionary<uint, DateTime> lastLaunch = [];

        #region Dependency Injection

        private readonly IFactory<ISpellParameters> spellParametersFactory;

        public JumpshroomEntityScript(
            IFactory<ISpellParameters> spellParametersFactory)
        {
            this.spellParametersFactory = spellParametersFactory;
        }

        #endregion

        public void OnLoad(IWorldEntity owner)
        {
            this.owner = owner;
        }

        public void OnAddToMap(IBaseMap map)
        {
            owner.SetInRangeCheck(TriggerRange);
        }

        public void OnEnterRange(IGridEntity entity)
        {
            if (entity is not IPlayer player)
                return;

            DateTime now = DateTime.UtcNow;
            if (lastLaunch.TryGetValue(player.Guid, out DateTime last) && now - last < Cooldown)
                return;
            lastLaunch[player.Guid] = now;

            ISpellParameters parameters = spellParametersFactory.Resolve();
            parameters.PrimaryTargetId = player.Guid;
            player.CastSpell(JumpSpell4Id, parameters);
        }
    }
}
