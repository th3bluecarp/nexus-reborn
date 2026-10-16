using NexusForever.Game.Static.Challenges;
using NexusForever.GameTable.Model;
using NexusForever.Shared;

namespace NexusForever.Game.Abstract.Entity
{
    public interface IChallengeManager : IUpdate
    {
        void SendInitialPackets();

        /// <summary>
        /// Invoked when the player enters a new zone, unlocks the combat challenges of the area.
        /// </summary>
        void OnZoneUpdate(WorldZoneEntry zone);

        /// <summary>
        /// Invoked when the player gets kill credit for a creature.
        /// </summary>
        void OnKill(IEnumerable<uint> targetGroupIds);

        /// <summary>
        /// Handle the challenge window buttons.
        /// </summary>
        void HandleChoice(uint challengeId, ChallengeChoice choice);
    }
}
