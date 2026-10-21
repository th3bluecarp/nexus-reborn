using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.PublicEvent;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.Spell;

namespace NexusForever.WorldServer.Network.Message.Handler.Entity
{
    public class ClientEntityInteractionHandler : IMessageHandler<IWorldSession, ClientEntityInteract>
    {
        #region Dependency Injection

        private readonly ILogger<ClientEntityInteractionHandler> log;

        private readonly IAssetManager assetManager;
        private readonly IEntityFactory entityFactory;

        public ClientEntityInteractionHandler(
            ILogger<ClientEntityInteractionHandler> log,
            IAssetManager assetManager,
            IEntityFactory entityFactory)
        {
            this.log           = log;
            this.assetManager  = assetManager;
            this.entityFactory = entityFactory;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientEntityInteract entityInteraction)
        {
            IWorldEntity entity = session.Player.GetVisible<IWorldEntity>(entityInteraction.UnitId);
            if (entity != null)
            {
                session.Player.QuestManager.ObjectiveUpdate(QuestObjectiveType.ActivateEntity, entity.CreatureId, 1u);
                session.Player.QuestManager.ObjectiveUpdate(QuestObjectiveType.TalkTo, entity.CreatureId, 1u);

                foreach (uint targetGroupId in assetManager.GetTargetGroupsForCreatureId(entity.CreatureId) ?? Enumerable.Empty<uint>())
                {
                    session.Player.QuestManager.ObjectiveUpdate(QuestObjectiveType.TalkToTargetGroup, targetGroupId, 1u);
                    entity.Map.PublicEventManager.UpdateObjective(session.Player, PublicEventObjectiveType.TalkTo, targetGroupId, 1);
                }
            }

            switch (entityInteraction.Type)
            {
                case InteractionType.TalkTo: // Quest NPC
                {
                    session.EnqueueMessageEncrypted(new ServerDialogStart
                    {
                        DialogUnitId = entityInteraction.UnitId
                    });
                    break;
                }
                case InteractionType.Vendor: // Handle Vendor
                    HandleVendor(session, entity);
                    break;
                case InteractionType.Mail: // "MailboxActivate"
                    var mailboxEntity = session.Player.Map.GetEntity<IMailboxEntity>(entityInteraction.UnitId);
                    break;
                case InteractionType.Bank: // "ShowBank"
                case InteractionType.SharedRealmBank: // "ShowRealmBank"
                case InteractionType.Dye: // "ShowDye"
                case InteractionType.TradeskillTrainer: // "InvokeTradeskillTrainerWindow", the client fires it itself when it sends the interaction (event 43, read from WildStar64.exe)
                    // the client opens the window for the interaction unit it is told about
                    session.EnqueueMessageEncrypted(new ServerInteractionChange
                    {
                        InteractionUnitId = entityInteraction.UnitId
                    });
                    break;
                case InteractionType.ScientistActivate: // experiment consoles, the client only opens the window for this interaction
                    if (!session.Player.PathMissionManager.OpenExperimentation(entity))
                        log.LogWarning($"Received unhandled interaction event {entityInteraction.Type} from Entity {entityInteraction.UnitId}");
                    break;
                case InteractionType.SettlerImprovement: // settler depots, the client opens the build window on ServerPathSettlerBuildInvoke
                    if (!session.Player.PathMissionManager.OpenSettlerDepot(entity))
                        log.LogWarning($"Received unhandled interaction event {entityInteraction.Type} from Entity {entityInteraction.UnitId}");
                    break;
                case InteractionType.Vehicle: // placed vehicles (battle suits, Rage Logic speeders)
                    if (!BoardVehicle(session, entity))
                        log.LogWarning($"Received unhandled interaction event {entityInteraction.Type} from Entity {entityInteraction.UnitId}");
                    break;
                case InteractionType.HousingGuildBroker: // "HousingGuildNeighborhoodBrokerOpen"
                case InteractionType.ConvertItem:
                case InteractionType.ConvertRep: // "ResourceConversionOpen"
                case InteractionType.Trainer: // "ToggleAbilitiesWindow"
                case InteractionType.Shuttle: // "InvokeShuttlePrompt"
                case InteractionType.FlightPathSettler:
                case InteractionType.FlightPathNew:
                case InteractionType.FlightPath: // "InvokeTaxiWindow"
                case InteractionType.Mannequin: // "MannequinWindowOpen"
                case InteractionType.GuildRegistrar: // "GuildRegistrarOpen"
                case InteractionType.WarPartyRegistrar: // "WarPartyRegistrarOpen"
                case InteractionType.GuildBank: // "GuildBankerOpen"
                case InteractionType.WarPartyBank: // "WarPartyBankerOpen"
                case InteractionType.CommodityMarketplace: // "ToggleMarketplaceWindow"
                case InteractionType.ItemAuctionhouse: // "ToggleAuctionWindow"
                case InteractionType.EngravingStation: // "TradeskillEngravingStationOpen"
                case InteractionType.HousingMannequin: // "HousingMannequinOpen"
                case InteractionType.CityDirections: // "CityDirectionsList"
                case InteractionType.CREDDExchange: // "ToggleCREDDExchangeWindow"
                case InteractionType.CommunityRegistrar: // "CommunityRegistrarOpen"
                case InteractionType.ContractBoard: // "ContractBoardOpen"
                case InteractionType.Barber: // "BarberOpen"
                case InteractionType.MasterCraftsman: // "MasterCraftsmanOpen"
                default:
                    log.LogWarning($"Received unhandled interaction event {entityInteraction.Type} from Entity {entityInteraction.UnitId}");
                    break;
            }
        }

        /// <summary>
        /// Clicking a placed vehicle spawns a drivable copy where it stands and seats the player as its pilot; the copy goes away
        /// when the player gets out and the placed one stays for the next use.
        /// </summary>
        private bool BoardVehicle(IWorldSession session, IWorldEntity worldEntity)
        {
            if (worldEntity?.CreatureInfo?.Entry == null || worldEntity.CreatureInfo.Entry.UnitVehicleId == 0u)
                return false;

            if (!session.Player.CanMount() || session.Player.Map == null)
                return true;

            // the client only offers the interaction close by, a far click would be a free teleport
            if (System.Numerics.Vector3.Distance(session.Player.Position, worldEntity.Position) > 15f)
                return true;

            var vehicle = entityFactory.CreateEntity<IVehicleEntity>();
            vehicle.Initialise(worldEntity.CreatureInfo, worldEntity.CreatureInfo.Entry.UnitVehicleId, 0u);
            vehicle.Rotation = worldEntity.Rotation;
            vehicle.EnqueuePassengerAdd(session.Player, VehicleSeatType.Pilot, 0);
            vehicle.AddToMap(session.Player.Map, worldEntity.Position);
            return true;
        }

        private void HandleVendor(IWorldSession session, IWorldEntity worldEntity)
        {
            if (worldEntity is not INonPlayerEntity vendorEntity)
                throw new InvalidOperationException();

            if (vendorEntity.VendorInfo == null)
                throw new InvalidOperationException();

            session.Player.SelectedVendorInfo = vendorEntity.VendorInfo;

            ServerVendorItemsUpdated vendorItemsUpdated = vendorEntity.VendorInfo.Build();
            vendorItemsUpdated.Guid = vendorEntity.Guid;
            session.EnqueueMessageEncrypted(vendorItemsUpdated);
        }
    }
}
