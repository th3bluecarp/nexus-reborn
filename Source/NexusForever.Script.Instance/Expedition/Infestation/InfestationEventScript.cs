using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.PublicEvent;
using NexusForever.Game.Static.PublicEvent;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Instance.Expedition.Infestation
{
    /// <summary>
    /// Expedition: Infestation (public event 95) aboard the X-82 cargo ship.
    /// </summary>
    /// <remarks>
    /// Stand in for the lost retail script, the objective order is read from the objective texts: board the ship, close the vents and
    /// seal the hull breaches, find the medical bay and its supplies, then clear the Lumbering Parasites and kill the Cyclopean Parasite.
    /// Healing the contaminated shiphands and the medbay attack complete on their own, the attack's Lashing Fiends exist only as level
    /// 50 veterans in the client data. Tagging cargo is optional and open from the start.
    /// </remarks>
    [ScriptFilterOwnerId(95)]
    public class InfestationEventScript : IPublicEventScript, IOwnedScript<IPublicEvent>
    {
        private const uint BoardShip          = 1005;
        private const uint CloseVents         = 228;
        private const uint RescueShiphands    = 1006;
        private const uint SealHullBreaches   = 1007;
        private const uint TagCargo           = 4699;
        private const uint FindMedicalBay     = 1438;
        private const uint FindMedicalSupplies = 230;
        private const uint HealShiphand       = 231;
        private const uint DefendMedbay       = 4698;
        private const uint KillParasites      = 229;
        private const uint KillCyclopean      = 1500;

        private const uint LumberingParasite  = 22669;
        private const uint CyclopeanParasite  = 26833;

        private IPublicEvent publicEvent;
        private readonly HashSet<uint> parasites = [];
        private readonly HashSet<uint> succeeded = [];

        public void OnLoad(IPublicEvent owner)
        {
            publicEvent = owner;
            publicEvent.ActivateObjective(BoardShip);
            publicEvent.ActivateObjective(TagCargo);
        }

        public void OnAddToMap(IGridEntity entity)
        {
            if (entity is IWorldEntity worldEntity && worldEntity.CreatureId == LumberingParasite)
                parasites.Add(worldEntity.Guid);
        }

        public void OnRemoveFromMap(IGridEntity entity)
        {
            if (entity is not IUnitEntity unit)
                return;

            if (unit.CreatureId == LumberingParasite && parasites.Remove(unit.Guid) && !unit.IsAlive)
                publicEvent.UpdateObjective(KillParasites, 1);
            else if (unit.CreatureId == CyclopeanParasite && !unit.IsAlive)
                publicEvent.UpdateObjective(KillCyclopean, 1);
        }

        public void OnPublicEventObjectiveStatus(IPublicEventObjective objective)
        {
            if (objective.Status != PublicEventStatus.Succeeded || !succeeded.Add(objective.Entry.Id))
                return;

            switch (objective.Entry.Id)
            {
                case BoardShip:
                    publicEvent.ActivateObjective(CloseVents);
                    publicEvent.ActivateObjective(SealHullBreaches);
                    publicEvent.ActivateObjective(RescueShiphands, 1u);
                    break;
                case SealHullBreaches:
                    // the shiphands are rescued from the hull breaches
                    publicEvent.UpdateObjective(RescueShiphands, 1);
                    TryOpenMedicalBay();
                    break;
                case CloseVents:
                    TryOpenMedicalBay();
                    break;
                case FindMedicalBay:
                    publicEvent.ActivateObjective(FindMedicalSupplies);
                    break;
                case FindMedicalSupplies:
                    publicEvent.ActivateObjective(HealShiphand, 1u);
                    publicEvent.UpdateObjective(HealShiphand, 1);
                    publicEvent.ActivateObjective(DefendMedbay);
                    publicEvent.UpdateObjective(DefendMedbay, 1);
                    break;
                case DefendMedbay:
                    if (parasites.Count > 0)
                        publicEvent.ActivateObjective(KillParasites, (uint)parasites.Count);
                    else
                        publicEvent.ActivateObjective(KillCyclopean, 1u);
                    break;
                case KillParasites:
                    publicEvent.ActivateObjective(KillCyclopean, 1u);
                    break;
            }
        }

        private void TryOpenMedicalBay()
        {
            if (succeeded.Contains(CloseVents) && succeeded.Contains(SealHullBreaches))
                publicEvent.ActivateObjective(FindMedicalBay);
        }
    }
}
