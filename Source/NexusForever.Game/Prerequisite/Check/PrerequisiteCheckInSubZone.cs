using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    [PrerequisiteCheck(PrerequisiteType.InSubZone)]
    public class PrerequisiteCheckInSubZone : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckInSubZone> log;

        public PrerequisiteCheckInSubZone(
            ILogger<PrerequisiteCheckInSubZone> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // Value is a world zone, met when the player is in it or in one of its sub zones. The map file's areas do not
            // always agree with the zone the data means (The Hidden Grotto 1083 is area 365/1084 in the map file, so A Hidden
            // Threat's call never came), so the quest manager's check also accepts a WorldLocation2 row of the zone family within 60 m.
            bool inZone = player.QuestManager?.IsInWorldZone(value) ?? false;

            bool? result = PrerequisiteCompare.Is(comparison, inZone);
            if (result == null)
                log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.InSubZone}!");
            return result ?? false;
        }
    }
}
