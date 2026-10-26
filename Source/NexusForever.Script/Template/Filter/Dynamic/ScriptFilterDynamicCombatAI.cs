using NexusForever.GameTable;
using NexusForever.GameTable.Static;

namespace NexusForever.Script.Template.Filter.Dynamic
{
    public class ScriptFilterDynamicCombatAI : IScriptFilterDynamicCombatAI
    {
        private HashSet<uint> creatureIds;

        #region Dependency Injection

        private readonly IGameTableManager gameTableManager;

        public ScriptFilterDynamicCombatAI(
            IGameTableManager gameTableManager)
        {
            this.gameTableManager = gameTableManager;
        }

        #endregion

        public void Initialise(Type scriptType)
        {
            creatureIds = gameTableManager.Creature2.Entries
                .Where(e => (e.Flags & CreatureFlags.NoAI) == 0 || IsHoldoutAttacker(e))
                .Select(e => e.Id)
                .ToHashSet();
        }

        // the Soldier holdout rosters ("Northern Wilds Holdout - M156 - Bruiser", "... Holdout Boss - M156 - Tank") all carry NoAI,
        // on retail the holdout script drove them, here they fight with the default AI or they stand where they spawn
        private static readonly System.Text.RegularExpressions.Regex holdoutRoster = new(@"Holdout( Boss)? - M\d+ - ");

        private static bool IsHoldoutAttacker(Creature2Entry entry)
        {
            return entry.Description != null
                && !entry.Description.StartsWith("[")
                && !entry.Description.StartsWith("(")
                && holdoutRoster.IsMatch(entry.Description);
        }

        /// <summary>
        /// Returns if <see cref="IScriptFilterSearch"/> can match dynamic filter.
        /// </summary>
        public bool Match(IScriptFilterSearch search)
        {
            if (creatureIds != null)
            {
                if (search.CreatureId == null)
                    return false;

                return creatureIds.Contains(search.CreatureId.Value);
            }

            return true;
        }
    }
}
