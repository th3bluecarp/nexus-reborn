using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Prerequisite.Check
{
    [PrerequisiteCheck(PrerequisiteType.Level)]
    public class PrerequisiteCheckLevel : IPrerequisiteCheck
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteCheckLevel> log;

        public PrerequisiteCheckLevel(
            ILogger<PrerequisiteCheckLevel> log)
        {
            this.log = log;
        }

        #endregion

        public bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            // Level rows that carry an ObjectId (4106: quests 3479/10100/10101 "LessThanOrEqual 1" obj 4065; 6232: quest 8944 obj 1)
            // are not player-level checks: 8944 also requires PrerequisiteLevel 4, and all four quests are open to any level on
            // retail (Jabbithole minimum level 1). Read literally they lock the quests to level-1 characters. Treated as met.
            // SEMANTICS: "Level prerequisite with ObjectId" (Invented, 2026-10-31, found by the quest bot).
            if (objectId != 0u)
                return true;

            switch (comparison)
            {
                case PrerequisiteComparison.Equal:
                    return player.Level == value;
                case PrerequisiteComparison.NotEqual:
                    return player.Level != value;
                case PrerequisiteComparison.GreaterThan:
                    return player.Level > value;
                case PrerequisiteComparison.GreaterThanOrEqual:
                    return player.Level >= value;
                case PrerequisiteComparison.LessThan:
                    return player.Level < value;
                case PrerequisiteComparison.LessThanOrEqual:
                    return player.Level <= value;
                default:
                    log.LogWarning($"Unhandled PrerequisiteComparison {comparison} for {PrerequisiteType.Level}!");
                    return false;
            }
        }
    }
}
