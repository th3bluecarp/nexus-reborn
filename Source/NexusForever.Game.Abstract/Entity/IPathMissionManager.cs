using NexusForever.Database.Character;
using NexusForever.Game.Static.PlayerPath;
using NexusForever.GameTable.Model;
using NexusForever.Shared;

namespace NexusForever.Game.Abstract.Entity
{
    public interface IPathMissionManager : IDatabaseCharacter, IUpdate
    {
        void SendInitialPackets();

        /// <summary>
        /// Invoked when the player enters a new zone, switches to that zone's episode for the active path.
        /// </summary>
        void OnZoneUpdate(WorldZoneEntry zone);

        /// <summary>
        /// Invoked when the player gets kill credit for a creature.
        /// </summary>
        void OnKill(uint creatureId, IEnumerable<uint> targetGroupIds);

        /// <summary>
        /// Invoked when the player successfully activates a creature.
        /// </summary>
        void OnActivate(uint creatureId, IEnumerable<uint> targetGroupIds);

        /// <summary>
        /// Invoked when the player completes a quest.
        /// </summary>
        void OnQuestComplete(uint questId);

        /// <summary>
        /// Returns the state of the path mission, <see cref="PathMissionState.NoMission"/> when the player does not have it.
        /// </summary>
        PathMissionState GetMissionState(uint missionId);
    }
}
