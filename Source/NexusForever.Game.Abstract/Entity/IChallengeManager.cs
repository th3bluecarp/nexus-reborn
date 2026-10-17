using NexusForever.Game.Static.Challenges;
using NexusForever.Database.Character;
using NexusForever.GameTable.Model;
using NexusForever.Shared;

namespace NexusForever.Game.Abstract.Entity
{
    public interface IChallengeManager : IDatabaseCharacter, IUpdate
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
        /// Invoked when the player successfully activates a creature.
        /// </summary>
        void OnActivate(IEnumerable<uint> targetGroupIds);

        /// <summary>
        /// Handle the challenge window buttons.
        /// </summary>
        void HandleChoice(uint challengeId, ChallengeChoice choice);

        /// <summary>
        /// Returns if the challenge is unlocked for the player (it has been in its area this session).
        /// </summary>
        bool IsUnlocked(uint challengeId);

        /// <summary>
        /// Returns if the challenge is running for the player.
        /// </summary>
        bool IsActive(uint challengeId);

        /// <summary>
        /// Returns how many times the player completed the challenge this session.
        /// </summary>
        uint GetCompletionCount(uint challengeId);
    }
}
