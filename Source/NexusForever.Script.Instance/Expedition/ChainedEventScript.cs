using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.PublicEvent;
using NexusForever.Game.Static.PublicEvent;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Script.Template;

namespace NexusForever.Script.Instance.Expedition
{
    /// <summary>
    /// An expedition whose objectives run as a fixed chain of steps, standing in for a lost retail event script.
    /// </summary>
    /// <remarks>
    /// Each step activates its objectives together and the next step starts once they have all succeeded. Objectives listed in
    /// <see cref="Auto"/> complete as soon as their step starts (they need lost scripts or creatures that are not placed). Kill objectives
    /// in <see cref="Kills"/> count deaths of their creatures, their count is the number of those creatures still alive when they start.
    /// When the last step is done any main objective that is still open is completed, so the event can finish.
    /// </remarks>
    public abstract class ChainedEventScript : IPublicEventScript, IOwnedScript<IPublicEvent>
    {
        /// <summary>Objective ids per step, in order.</summary>
        protected abstract uint[][] Steps { get; }
        /// <summary>Objectives that complete as soon as their step starts.</summary>
        protected virtual uint[] Auto => [];
        /// <summary>Kill objectives and the creatures whose deaths count for them.</summary>
        protected virtual Dictionary<uint, uint[]> Kills => [];

        private IPublicEvent publicEvent;
        private int step = -1;
        private bool advancing;
        private double startDelay = 5d;
        private readonly HashSet<uint> succeeded = [];
        private readonly Dictionary<uint, HashSet<uint>> alive = [];   // creature id -> guids alive
        private readonly HashSet<uint> seenCreatures = [];
        private readonly Dictionary<uint, double> triggerTimers = [];   // area objective -> seconds left

        /// <summary>Seconds after which a reach-the-area objective completes when its trigger volume never fires.</summary>
        private const double TriggerFallbackSeconds = 90d;

        public void OnLoad(IPublicEvent owner)
        {
            publicEvent = owner;
        }

        public void Update(double lastTick)
        {
            // the event is created before the map's creatures are added, the first step waits for them
            if (step < 0)
            {
                startDelay -= lastTick;
                if (startDelay <= 0d)
                    NextStep();
                return;
            }

            foreach (uint objectiveId in triggerTimers.Keys.ToList())
            {
                if (succeeded.Contains(objectiveId))
                {
                    triggerTimers.Remove(objectiveId);
                    continue;
                }

                triggerTimers[objectiveId] -= lastTick;
                if (triggerTimers[objectiveId] <= 0d)
                {
                    triggerTimers.Remove(objectiveId);
                    Complete(objectiveId);
                }
            }
        }

        public void OnAddToMap(IGridEntity entity)
        {
            if (entity is IWorldEntity seen)
                seenCreatures.Add(seen.CreatureId);

            if (entity is not IWorldEntity worldEntity || !Kills.Values.Any(c => c.Contains(worldEntity.CreatureId)))
                return;

            if (!alive.TryGetValue(worldEntity.CreatureId, out HashSet<uint> guids))
                alive.Add(worldEntity.CreatureId, guids = []);
            guids.Add(worldEntity.Guid);
        }

        public void OnRemoveFromMap(IGridEntity entity)
        {
            if (entity is not IUnitEntity unit || !alive.TryGetValue(unit.CreatureId, out HashSet<uint> guids) || !guids.Remove(unit.Guid))
                return;
            if (unit.IsAlive)
                return;

            foreach ((uint objectiveId, uint[] creatures) in Kills)
                if (creatures.Contains(unit.CreatureId))
                    publicEvent.UpdateObjective(objectiveId, 1);
        }

        public void OnPublicEventObjectiveStatus(IPublicEventObjective objective)
        {
            if (objective.Status != PublicEventStatus.Succeeded || !succeeded.Add(objective.Entry.Id) || advancing)
                return;

            if (step >= 0 && step < Steps.Length && Steps[step].All(succeeded.Contains))
                NextStep();
        }

        private void NextStep()
        {
            advancing = true;
            try
            {
                AdvanceStep();
            }
            finally
            {
                advancing = false;
            }
        }

        private void AdvanceStep()
        {
            step++;
            if (step >= Steps.Length)
            {
                // close any main objective the chain did not cover so the event can finish
                foreach (PublicEventObjectiveEntry entry in GameTableManager.Instance.PublicEventObjective.Entries
                    .Where(e => e.PublicEventId == publicEvent.Id && e.PublicEventObjectiveCategoryEnum == PublicEventObjectiveCategory.Main && !succeeded.Contains(e.Id)))
                    Complete(entry.Id);
                return;
            }

            foreach (uint objectiveId in Steps[step])
            {
                PublicEventObjectiveEntry entry = GameTableManager.Instance.PublicEventObjective.GetEntry(objectiveId);
                if (entry == null || Auto.Contains(objectiveId))
                {
                    Complete(objectiveId);
                    continue;
                }

                if (Kills.TryGetValue(objectiveId, out uint[] creatures))
                {
                    uint count = (uint)creatures.Sum(c => alive.TryGetValue(c, out HashSet<uint> g) ? g.Count : 0);
                    if (count == 0u)
                        Complete(objectiveId);
                    else
                        publicEvent.ActivateObjective(objectiveId, entry.Count > 0u ? Math.Min(entry.Count, count) : count);
                    continue;
                }

                // objectives aimed at creatures or objects that are not placed on this map would never progress
                if (TargetsMissing(entry))
                {
                    Complete(objectiveId);
                    continue;
                }

                publicEvent.ActivateObjective(objectiveId, entry.Count > 0u ? 0u : 1u);
                if (entry.PublicEventObjectiveTypeEnum == PublicEventObjectiveType.ParticipantsInTriggerVolume)
                    triggerTimers[objectiveId] = TriggerFallbackSeconds;
            }

            // a step whose objectives all completed straight away moves on
            if (step < Steps.Length && Steps[step].All(succeeded.Contains))
                AdvanceStep();
        }

        private bool TargetsMissing(PublicEventObjectiveEntry entry)
        {
            switch (entry.PublicEventObjectiveTypeEnum)
            {
                case PublicEventObjectiveType.KillTargetGroup:
                case PublicEventObjectiveType.ActivateTargetGroup:
                case PublicEventObjectiveType.ActivateTargetGroupChecklist:
                case PublicEventObjectiveType.KillClusterTargetGroup:
                case PublicEventObjectiveType.TalkTo:
                case PublicEventObjectiveType.TalkToChecklist:
                    HashSet<uint> members = TargetGroupMembers(entry.ObjectId, 0);
                    return members.Count == 0 || !members.Overlaps(seenCreatures);
                default:
                    return false;
            }
        }

        private static HashSet<uint> TargetGroupMembers(uint targetGroupId, int depth)
        {
            var members = new HashSet<uint>();
            TargetGroupEntry entry = GameTableManager.Instance.TargetGroup.GetEntry(targetGroupId);
            if (entry == null || depth > 3)
                return members;

            foreach (uint data in entry.DataEntries.Where(d => d != 0u))
            {
                if (entry.Type == 1u)
                    members.Add(data);
                else
                    members.UnionWith(TargetGroupMembers(data, depth + 1));
            }

            return members;
        }

        private void Complete(uint objectiveId)
        {
            publicEvent.ActivateObjective(objectiveId, 1u);
            publicEvent.UpdateObjective(objectiveId, 1);
        }
    }
}
