using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.PublicEvent;
using NexusForever.Game.Static.PublicEvent;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Instance.Expedition.OutpostM13
{
    /// <summary>
    /// Expedition: Outpost M-13 (public event 108), the Steady Traveler and the asteroid mine.
    /// </summary>
    /// <remarks>
    /// Stand in for the lost retail script, the order is read from the objective texts: talk to Captain Milo, go to the cargo hold and
    /// fight off the Novaburn boarders and "Ransacker" Rorgh, leave the ship, find Foreman Krause, gather outside the mine, clear the
    /// hive and kill the Hive Queen, then head back to the shuttle. Objectives that need lost scripts or creatures that are not placed
    /// (the Novaburn waves on M-13, the datachrons, the mine search) complete on their own when their step is reached.
    /// </remarks>
    [ScriptFilterOwnerId(108)]
    public class OutpostM13EventScript : IPublicEventScript, IOwnedScript<IPublicEvent>
    {
        private const uint TalkToMilo        = 1441;
        private const uint GoToCargoHold     = 1442;
        private const uint KillMarauders     = 253;
        private const uint KillRorgh         = 254;
        private const uint ExitShip          = 262;
        private const uint FindKrause        = 1440;
        private const uint CollectDatachrons = 656;
        private const uint SearchMine        = 1439;
        private const uint MarauderWave1     = 4733;
        private const uint MarauderWave2     = 4734;
        private const uint KillMaraudersM13  = 4697;
        private const uint KillNovaburnLeader = 4700;
        private const uint GatherOutsideMine = 4701;
        private const uint ClearHive         = 256;
        private const uint KillHiveQueen     = 257;
        private const uint HeadToShuttle     = 5134;

        private static readonly HashSet<uint> Marauders = [23453u, 23454u];
        private const uint Rorgh = 23455;
        private static readonly HashSet<uint> HiveCreatures = [23514u, 23500u, 23536u, 29703u, 29612u];
        private const uint HiveQueen = 23513;

        private IPublicEvent publicEvent;
        private readonly HashSet<uint> hive = [];
        private readonly HashSet<uint> succeeded = [];

        public void OnLoad(IPublicEvent owner)
        {
            publicEvent = owner;
            publicEvent.ActivateObjective(TalkToMilo);
        }

        public void OnAddToMap(IGridEntity entity)
        {
            if (entity is IWorldEntity worldEntity && HiveCreatures.Contains(worldEntity.CreatureId))
                hive.Add(worldEntity.Guid);
        }

        public void OnRemoveFromMap(IGridEntity entity)
        {
            if (entity is not IUnitEntity unit || unit.IsAlive)
                return;

            if (Marauders.Contains(unit.CreatureId))
                publicEvent.UpdateObjective(KillMarauders, 1);
            else if (unit.CreatureId == Rorgh)
                publicEvent.UpdateObjective(KillRorgh, 1);
            else if (hive.Remove(unit.Guid))
                publicEvent.UpdateObjective(ClearHive, 1);
            else if (unit.CreatureId == HiveQueen)
                publicEvent.UpdateObjective(KillHiveQueen, 1);
        }

        public void OnPublicEventObjectiveStatus(IPublicEventObjective objective)
        {
            if (objective.Status != PublicEventStatus.Succeeded || !succeeded.Add(objective.Entry.Id))
                return;

            switch (objective.Entry.Id)
            {
                case TalkToMilo:
                    publicEvent.ActivateObjective(GoToCargoHold);
                    break;
                case GoToCargoHold:
                    publicEvent.ActivateObjective(KillMarauders);
                    publicEvent.ActivateObjective(KillRorgh, 1u);
                    break;
                case KillMarauders:
                case KillRorgh:
                    if (succeeded.Contains(KillMarauders) && succeeded.Contains(KillRorgh))
                        publicEvent.ActivateObjective(ExitShip);
                    break;
                case ExitShip:
                    publicEvent.ActivateObjective(FindKrause);
                    break;
                case FindKrause:
                    foreach (uint id in new[] { CollectDatachrons, SearchMine, MarauderWave1, MarauderWave2, KillMaraudersM13, KillNovaburnLeader })
                        Complete(id);
                    publicEvent.ActivateObjective(GatherOutsideMine);
                    break;
                case GatherOutsideMine:
                    if (hive.Count > 0)
                        publicEvent.ActivateObjective(ClearHive, (uint)hive.Count);
                    else
                        publicEvent.ActivateObjective(KillHiveQueen, 1u);
                    break;
                case ClearHive:
                    publicEvent.ActivateObjective(KillHiveQueen, 1u);
                    break;
                case KillHiveQueen:
                    publicEvent.ActivateObjective(HeadToShuttle);
                    break;
            }
        }

        private void Complete(uint objectiveId)
        {
            publicEvent.ActivateObjective(objectiveId, 1u);
            publicEvent.UpdateObjective(objectiveId, 1);
        }
    }
}
