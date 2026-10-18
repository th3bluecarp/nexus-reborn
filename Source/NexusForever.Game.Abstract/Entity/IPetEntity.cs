using NexusForever.Game.Abstract.Entity.Creature;

namespace NexusForever.Game.Abstract.Entity
{
    public interface IPetEntity : IWorldEntity
    {
        uint OwnerGuid { get; }

        /// <summary>
        /// A scientist scanbot follows its owner like a vanity pet but does not take the vanity pet slot.
        /// </summary>
        bool IsScanbot { get; }

        void Initialise(IPlayer owner, ICreatureInfo creatureInfo, bool scanbot = false);
    }
}
