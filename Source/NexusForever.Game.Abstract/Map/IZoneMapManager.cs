using System.Numerics;
using NexusForever.Database.Character;
using NexusForever.Game.Abstract.Entity;

namespace NexusForever.Game.Abstract.Map
{
    public interface IZoneMapManager : IDatabaseCharacter
    {
        void SendInitialPackets();
        void SendZoneMaps();

        /// <summary>
        /// Invoked when <see cref="IPlayer"/> moves to a new <see cref="Vector3"/>.
        /// </summary>
        void OnRelocate(Vector3 vector);

        /// <summary>
        /// Invoked when <see cref="IPlayer"/> moves to a new zone.
        /// </summary>
        void OnZoneUpdate();

        /// <summary>
        /// Return the explored percentage (0 to 100) of the zone map for the supplied world zone, 0 when never visited.
        /// </summary>
        float GetExploredPercent(uint worldZoneId);
    }
}