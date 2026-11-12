using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusForever.Database;
using NexusForever.Database.Configuration.Model;
using NexusForever.Game;
using NexusForever.Game.Abstract.Chat.Format;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Matching.Match;
using NexusForever.Game.Abstract.Matching.Queue;
using NexusForever.Game.Abstract.PublicEvent;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Achievement;
using NexusForever.Game.Character;
using NexusForever.Game.Cinematic;
using NexusForever.Game.Customisation;
using NexusForever.Game.Entity;
using NexusForever.Game.Guild;
using NexusForever.Game.Housing;
using NexusForever.Game.Map;
using NexusForever.Game.Quest;
using NexusForever.Game.RBAC;
using NexusForever.Game.Reputation;
using NexusForever.Game.Server;
using NexusForever.Game.Storefront;
using NexusForever.GameTable;
using NexusForever.GameTable.Text.Filter;
using NexusForever.GameTable.Text.Search;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.Session;
using NexusForever.Network.World.Entity;
using NexusForever.Network.World.Message;
using NexusForever.Script;
using NexusForever.Shared;
using NexusForever.Shared.Configuration;
using NexusForever.WorldServer.Command;
using NexusForever.WorldServer.Network;

namespace NexusForever.WorldServer.Service
{
    public class HostedService : IHostedService
    {
        #region Dependency Injection

        private readonly ILogger log;

        private readonly IScriptManager scriptManager;
        private readonly ILoginQueueManager loginQueueManager;
        private readonly INetworkManager<IWorldSession> networkManager;
        private readonly IMessageManager messageManager;
        private readonly IMatchingManager matchingManager;
        private readonly IMatchManager matchManager;
        private readonly IPublicEventTemplateManager publicEventManager;
        private readonly ICreatureInfoManager creatureInfoManager;
        private readonly IChatFormatManager chatFormatManager;
        private readonly IWorldManager worldManager;

        // TODO: this really should be split into multiple HostedServices
        public HostedService(
            ILogger<IHostedService> log,
            IServiceProvider serviceProvider,
            IScriptManager scriptManager,
            ILoginQueueManager loginQueueManager,
            INetworkManager<IWorldSession> networkManager,
            IMessageManager messageManager,
            IMatchingManager matchingManager,
            IMatchManager matchManager,
            IPublicEventTemplateManager publicEventManager,
            ICreatureInfoManager creatureInfoManager,
            IChatFormatManager chatFormatManager,
            IWorldManager worldManager)
        {
            this.log = log;

            LegacyServiceProvider.Provider = serviceProvider;

            this.scriptManager       = scriptManager;
            this.loginQueueManager   = loginQueueManager;
            this.networkManager      = networkManager;
            this.messageManager      = messageManager;
            this.matchingManager     = matchingManager;
            this.matchManager        = matchManager;
            this.publicEventManager  = publicEventManager;
            this.creatureInfoManager = creatureInfoManager;
            this.chatFormatManager   = chatFormatManager;
            this.worldManager        = worldManager;
        }

        #endregion

        /// <summary>
        /// Start <see cref="WorldServer"/> and any related resources.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            log.LogInformation("Starting...");

            DatabaseManager.Instance.Initialise(SharedConfiguration.Instance.Get<DatabaseConfig>());
            DatabaseManager.Instance.Migrate();

            RealmContext.Instance.Initialise();

            // RBACManager must be initialised before CommandManager
            RBACManager.Instance.Initialise();

            DisableManager.Instance.Initialise();

            scriptManager.Initialise();

            await GameTableManager.Instance.Initialise();
            publicEventManager.Initialise();
            MapIOManager.Instance.Initialise();
            SearchManager.Instance.Initialise();
            EntityManager.Instance.Initialise();
            creatureInfoManager.Initialise();
            EntityCommandManager.Instance.Initialise();
            EntityCacheManager.Instance.Initialise();
            FactionManager.Instance.Initialise();

            GlobalCinematicManager.Instance.Initialise();
            chatFormatManager.Initialise();
            GlobalAchievementManager.Instance.Initialise(); // must be initialised before guilds
            GlobalGuildManager.Instance.Initialise(); // must be initialised before residences
            CharacterManager.Instance.Initialise(); // must be initialised before residences
            GlobalResidenceManager.Instance.Initialise();

            AssetManager.Instance.Initialise();
            ItemManager.Instance.Initialise();
            GlobalQuestManager.Instance.Initialise();

            GlobalStorefrontManager.Instance.Initialise();
            ServerManager.Instance.Initialise(RealmContext.Instance.RealmId);

            TextFilterManager.Instance.Initialise();

            CustomisationManager.Instance.Initialise();

            ShutdownManager.Instance.Initialise(WorldServer.Shutdown);

            matchingManager.Initialise();

            messageManager.RegisterNetworkManagerMessagesAndHandlers();
            messageManager.RegisterNetworkManagerWorldMessages();
            messageManager.RegisterNetworkManagerWorldHandlers();

            // initialise world after all assets have loaded but before any network or command handlers might be invoked
            worldManager.Initialise(lastTick =>
            {
                // NetworkManager must be first and MapManager must come before everything else
                networkManager.Update(lastTick);
                MapManager.Instance.Update(lastTick);

                BuybackManager.Instance.Update(lastTick);
                GlobalQuestManager.Instance.Update(lastTick);
                GlobalGuildManager.Instance.Update(lastTick);
                GlobalResidenceManager.Instance.Update(lastTick); // must be after guild update

                loginQueueManager.Update(lastTick);
                matchingManager.Update(lastTick);
                matchManager.Update(lastTick);

                scriptManager.Update(lastTick);

                ShutdownManager.Instance.Update(lastTick);

                UpdateGracefulStop(lastTick);

                // process commands after everything else in the tick has processed
                CommandManager.Instance.Update(lastTick);
            });

            // initialise network and command managers last to make sure the rest of the server is ready for invoked handlers
            networkManager.Initialise();
            networkManager.Start();

            CommandManager.Instance.Initialise();

            log.LogInformation("Started!");
        }

        /// <summary>
        /// File that asks the running world server to log every player out, wait for their saves and then stop (deploy scripts create it).
        /// </summary>
        public static readonly string GracefulStopFile = Path.Combine(AppContext.BaseDirectory, "shutdown.request");

        /// <summary>
        /// Seconds the logout saves of a graceful stop may take before the server stops anyway.
        /// </summary>
        private const double GracefulStopTimeout = 90d;

        private double gracefulStopPoll;
        private double gracefulStopElapsed;
        private bool gracefulStop;

        /// <summary>
        /// Invoked each world tick, starts and finishes a graceful stop.
        /// </summary>
        /// <remarks>
        /// A killed process loses every change not yet committed (a player who just left, the last minute of a periodic save), so a stop first sends
        /// every player through the normal logout, whose cleanup saves the character, and only stops once no session has a pending save.
        /// </remarks>
        private void UpdateGracefulStop(double lastTick)
        {
            if (!gracefulStop)
            {
                gracefulStopPoll += lastTick;
                if (gracefulStopPoll < 2d)
                    return;

                gracefulStopPoll = 0d;
                if (!File.Exists(GracefulStopFile))
                    return;

                try
                {
                    File.Delete(GracefulStopFile);
                }
                catch (IOException e)
                {
                    log.LogWarning(e, "Could not delete the graceful stop request file.");
                }

                gracefulStop = true;
                log.LogInformation("Graceful stop requested, logging out every player and waiting for their saves.");

                foreach (IWorldSession session in networkManager.ToList())
                {
                    if (session.Player?.LogoutManager.State is LogoutState.Logout or LogoutState.Finished)
                        continue;

                    session.Player?.LogoutManager.Finish(LogoutReason.WorldServerConnectionLost);
                }

                return;
            }

            gracefulStopElapsed += lastTick;

            bool pending = networkManager.Any(s => s.Player != null || s.Events.PendingEvents);
            if (pending && gracefulStopElapsed < GracefulStopTimeout)
                return;

            if (pending)
                log.LogError($"Graceful stop gave up after {gracefulStopElapsed:0} s with players or saves still pending.");
            else
                log.LogInformation($"Graceful stop: every player is saved and logged out after {gracefulStopElapsed:0.0} s, stopping.");

            // stops the host, StopAsync below saves anything still left
            WorldServer.Shutdown();
            gracefulStop = false;
            gracefulStopPoll = double.MinValue;
        }

        /// <summary>
        /// Stop <see cref="WorldServer"/> and any related resources.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            log.LogInformation("Stopping...");

            // stop network manager listening for incoming connections
            // it is still possible for incoming packets to be parsed though won't be handled once the world thread is stopped
            networkManager.Shutdown();

            // stop command manager listening for commands
            CommandManager.Instance.Shutdown();

            // stop server manager pinging other servers
            ServerManager.Instance.Shutdown();

            // stop world manager processing the world thread
            // at this point no incoming packets will be handled
            worldManager.Shutdown();

            // save residences, guilds and players to the database
            GlobalResidenceManager.Instance.Shutdown();
            GlobalGuildManager.Instance.Shutdown();

            // wait for the saves, the process ends as soon as this returns
            Task[] saves = networkManager.Where(s => s.Player != null).Select(s => s.Player.SaveDirect()).ToArray();
            if (!Task.WaitAll(saves, TimeSpan.FromSeconds(60)))
                log.LogError("Not every player save finished within 60 s of stopping.");

            log.LogInformation("Stopped!");
            return Task.CompletedTask;
        }
    }
}
