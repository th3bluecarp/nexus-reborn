using System.Collections.Concurrent;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    /// <summary>
    /// "Jump through the Unusual Rocks" (Deconstruct the Construct 10520/10523, checklist target group 14425): the rocks have no
    /// activation spell and all carry checklist index 0, so nothing could ever tick the three boxes. Stand-in: passing within
    /// 4 m of a rock the player has not passed yet ticks the next box. SEMANTICS: "Unusual Rocks tick on approach" (2026-11-01).
    /// </summary>
    [ScriptFilterCreatureId(73500)]
    public class UnusualRockEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private const uint ChecklistTargetGroup = 14425u;
        private static readonly ConcurrentDictionary<ulong, HashSet<uint>> RocksPassed = new();

        private IWorldEntity owner;

        public void OnLoad(IWorldEntity owner)
        {
            this.owner = owner;
            owner.SetInRangeCheck(4f);
        }

        public void OnEnterRange(IGridEntity entity)
        {
            if (entity is not IPlayer player || owner == null)
                return;

            HashSet<uint> passed = RocksPassed.GetOrAdd(player.CharacterId, _ => []);
            lock (passed)
            {
                if (!passed.Add(owner.Guid))
                    return;
                // checklist objectives store one bit per unit; the rocks all say index 0, so number them in passing order
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.ActivateTargetGroupChecklist, ChecklistTargetGroup, (uint)(passed.Count - 1));
            }
        }
    }
}
