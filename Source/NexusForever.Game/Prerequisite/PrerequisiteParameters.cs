using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.Game.Prerequisite
{
    public class PrerequisiteParameters : IPrerequisiteParameters
    {
        public CastResult? CastResult { get; set; }
        public IUnitEntity Target { get; set; }
        public ushort? TaxiNode { get; set; }
        public bool UnhandledResult { get; set; }
        public IWorldEntity EvaluatedUnit { get; set; }

        /// <summary>
        /// Spell cast to exclude from UnderSpell-style checks while evaluating that spell's effect prerequisites.
        /// The executing spell is present in the caster's spell collection but is not an aura the caster was already under.
        /// </summary>
        public uint? ExcludedSpellCastingId { get; set; }
    }
}
