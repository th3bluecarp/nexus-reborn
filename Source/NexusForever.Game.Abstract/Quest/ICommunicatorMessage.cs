using NexusForever.Game.Abstract.Entity;
using NexusForever.Network.Session;

namespace NexusForever.Game.Abstract.Quest
{
    public interface ICommunicatorMessage
    {
        uint Id { get; }
        ushort QuestId { get; }

        /// <summary>
        /// Sent when the player walks into this world zone (or a sub zone of it); 0 when the zone does not matter.
        /// </summary>
        uint WorldZoneId { get; }

        /// <summary>
        /// A zone entry call: its only quest condition is the offered quest itself (in state 0, never started).
        /// </summary>
        bool ZoneEntry { get; }

        /// <summary>
        /// Checks if <see cref="IPlayer"/> meets the required conditions for this quest to be added to their communicator.
        /// </summary>
        bool Meets(IPlayer player);

        /// <summary>
        /// Send communicator message to <see cref="IGameSession"/>.
        /// </summary>
        void Send(IGameSession session);
    }
}