using NexusForever.Game.Abstract.Entity;
using NexusForever.Network.World.Message.Static;

namespace NexusForever.Game.Abstract.Prerequisite
{
    public interface IPrerequisiteParameters
    {
        public CastResult? CastResult { get; set; }
        public IUnitEntity Target { get; set; }
        public ushort? TaxiNode { get; set; }

        /// <summary>
        /// Result to use for prerequisite types that have no handler yet.
        /// </summary>
        /// <remarks>
        /// Defaults to false. Entity visibility sets this to true so an entity is not hidden just because one of its
        /// prerequisite types is not implemented.
        /// </remarks>
        public bool UnhandledResult { get; set; }
    }
}
