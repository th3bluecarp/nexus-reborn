using System.Collections.Immutable;
using NexusForever.Game.Static;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Abstract
{
    public interface IAssetManager
    {
        /// <summary>
        /// Id to be assigned to the next created mail.
        /// </summary>
        ulong NextMailId { get; }

        void Initialise();

        /// <summary>
        /// Returns an <see cref="ImmutableList{T}"/> containing all <see cref="ItemDisplaySourceEntryEntry"/>'s for the supplied itemSource.
        /// </summary>
        ImmutableList<ItemDisplaySourceEntryEntry> GetItemDisplaySource(uint itemSource);

        /// <summary>
        /// Returns a Tutorial ID if it's found in the Zone Tutorials cache
        /// </summary>
        uint GetTutorialIdForZone(uint zoneId);

        /// <summary>
        /// Returns an <see cref="ImmutableList{T}"/> containing all TargetGroup ID's associated with the creatureId.
        /// </summary>
        ImmutableList<uint> GetTargetGroupsForCreatureId(uint creatureId);

        /// <summary>
        /// Target groups of type 9, which name marker entities (Icemaw Cavern's Cave Entrances for "Kill Yeti in Icemaw Cavern"):
        /// group id to the creature ids of its markers.
        /// </summary>
        ImmutableDictionary<uint, ImmutableList<uint>> AreaMarkerTargetGroups { get; }

        /// <summary>
        /// WorldLocation2 ids of the quest objective indicators of each area marker target group: where kills count when
        /// none of the group's marker entities stands on the map.
        /// </summary>
        ImmutableDictionary<uint, ImmutableList<uint>> AreaMarkerIndicatorLocations { get; }

        /// <summary>
        /// Returns an <see cref="ImmutableList{T}"/> containing all <see cref="RewardPropertyPremiumModifierEntry"/> for the given <see cref="AccountTier"/>.
        /// </summary>
        ImmutableList<RewardPropertyPremiumModifierEntry> GetRewardPropertiesForTier(AccountTier tier);
    }
}