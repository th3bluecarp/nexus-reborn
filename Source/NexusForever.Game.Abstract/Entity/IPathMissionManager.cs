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

        /// <summary>
        /// Invoked when an entity comes into the player's vision, marks scan targets and settler depots for the active path.
        /// </summary>
        void OnEntityVisible(IWorldEntity entity);

        /// <summary>
        /// Invoked when the player leaves the map, the scanbot is dismissed and a running holdout fails.
        /// </summary>
        void OnRemoveFromMap();

        /// <summary>
        /// Scientist: summon the scanbot of the supplied profile (0 for the default one).
        /// </summary>
        void SummonScanbot(uint profileId);

        /// <summary>
        /// Scientist: dismiss the scanbot.
        /// </summary>
        void DismissScanbot();

        /// <summary>
        /// Scientist: scan the target, or the nearest scan target, with the scanbot.
        /// </summary>
        void Scan();

        /// <summary>
        /// Settler: build the supplied tier (zero based) of an improvement group at a depot.
        /// </summary>
        void BuildImprovement(uint improvementGroupId, uint tier);
    }
}
