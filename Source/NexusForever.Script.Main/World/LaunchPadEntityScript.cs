using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;
using NexusForever.Shared;

namespace NexusForever.Script.Main.World
{
    /// <summary>
    /// Jump pads (Jumpshrooms, Airbooms): stepping on one launches the player with its launch spell.
    /// </summary>
    /// <remarks>
    /// Stand in for the lost retail triggers. These creatures have no activate spell, the launch spells are named after them in the
    /// client data. Range and the per player cooldown are guesses.
    /// </remarks>
    [ScriptFilterCreatureId(37703, 37681, 39097)]
    public class LaunchPadEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private static readonly Dictionary<uint, uint> LaunchSpells = new()
        {
            { 37703u, 40649u }, // Jumpshroom, Celestion: Jumpshroom Spell - Celestion
            { 37681u, 40649u }, // Jumpshroom, Sylvan Glade (smaller): same spell
            { 39097u, 41263u }, // Airboom, Whitevale Dead Rock Prison: Dead Rock Prison - Trampoline Spell - Boom Jump
        };

        private const float TriggerRange = 2.5f;
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(2d);

        private IWorldEntity owner;
        private readonly Dictionary<uint, DateTime> lastLaunch = [];

        #region Dependency Injection

        private readonly IFactory<ISpellParameters> spellParametersFactory;

        public LaunchPadEntityScript(
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
            if (entity is not IPlayer player || owner.CreatureInfo?.Entry == null
                || !LaunchSpells.TryGetValue(owner.CreatureInfo.Entry.Id, out uint spell4Id))
                return;

            DateTime now = DateTime.UtcNow;
            if (lastLaunch.TryGetValue(player.Guid, out DateTime last) && now - last < Cooldown)
                return;
            lastLaunch[player.Guid] = now;

            ISpellParameters parameters = spellParametersFactory.Resolve();
            parameters.PrimaryTargetId = player.Guid;
            player.CastSpell(spell4Id, parameters);
        }
    }
}
