using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Abstract.PublicEvent;
using NexusForever.Game.Map.Search;
using NexusForever.Game.Static.Reputation;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NLog;
using EventTeam = NexusForever.Game.Static.PublicEvent.PublicEventTeam;

namespace NexusForever.Game.PublicEvent
{
    /// <summary>
    /// Starts open world public events (type 0 with a world location) when a player of the minimum level comes close.
    /// </summary>
    /// <remarks>
    /// Retail started these from server scripts that are lost. Players within range join automatically, on the team of their
    /// faction when the event has one, otherwise the first team. A running event ends after its failure time, and an event
    /// waits <see cref="RestartDelay"/> before it can start again.
    /// </remarks>
    public class OpenWorldEventStarter
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2d);
        private static readonly TimeSpan RestartDelay  = TimeSpan.FromMinutes(5d);
        private const float MinStartRadius = 40f;

        private class EventState
        {
            public PublicEventEntry Entry { get; init; }
            public WorldLocation2Entry Location { get; init; }
            public DateTime? StartedAt { get; set; }
            public DateTime NextStart { get; set; } = DateTime.MinValue;
        }

        private readonly IBaseMap map;
        private readonly List<EventState> events;

        private OpenWorldEventStarter(IBaseMap map, List<EventState> events)
        {
            this.map    = map;
            this.events = events;
        }

        /// <summary>
        /// Attach a starter to the supplied open world map, does nothing when the map has no open world events.
        /// </summary>
        public static void Attach(IBaseMap map)
        {
            var events = GameTableManager.Instance.PublicEvent.Entries
                .Where(e => e.WorldId == map.Entry.Id && e.PublicEventTypeEnum == 0u && e.WorldLocation2Id != 0u && e.PublicEventIdParent == 0u)
                .Select(e => new EventState { Entry = e, Location = GameTableManager.Instance.WorldLocation2.GetEntry(e.WorldLocation2Id) })
                .Where(s => s.Location != null)
                .ToList();
            if (events.Count == 0)
                return;

            var starter = new OpenWorldEventStarter(map, events);
            map.AddTimer(CheckInterval, starter.Tick);
            log.Trace($"Open world event starter watching {events.Count} event(s) on map {map.Entry.Id}.");
        }

        private bool Tick()
        {
            DateTime now = DateTime.UtcNow;
            foreach (EventState state in events)
            {
                IPublicEvent publicEvent = map.PublicEventManager.GetEvent(state.Entry.Id);
                if (state.StartedAt.HasValue && (publicEvent == null || publicEvent.HasFinished))
                {
                    state.StartedAt = null;
                    state.NextStart = now + RestartDelay;
                }

                // a finished event stays registered until the public event manager removes it
                if (publicEvent != null && publicEvent.HasFinished)
                    continue;

                List<IPlayer> players = GetPlayersInRange(state);
                if (publicEvent == null)
                {
                    if (now < state.NextStart || players.Count == 0)
                        continue;

                    publicEvent = map.PublicEventManager.CreateEvent(state.Entry.Id);
                    if (publicEvent == null)
                    {
                        state.NextStart = now + RestartDelay;
                        continue;
                    }

                    state.StartedAt = now;
                    log.Trace($"Started open world public event {state.Entry.Id} on map {map.Entry.Id}.");
                }
                else if (state.Entry.FailureTimeMs > 0u && state.StartedAt.HasValue
                    && now - state.StartedAt.Value > TimeSpan.FromMilliseconds(state.Entry.FailureTimeMs))
                {
                    publicEvent.Finish(null);
                    continue;
                }

                foreach (IPlayer player in players)
                    JoinEvent(publicEvent, player);
            }

            return true;
        }

        private List<IPlayer> GetPlayersInRange(EventState state)
        {
            var position = new System.Numerics.Vector3(state.Location.Position0, state.Location.Position1, state.Location.Position2);
            float radius = Math.Max(state.Location.Radius, MinStartRadius);

            var check = new SearchCheckRange<IPlayer>();
            check.Initialise(position, radius);

            return map.Search(position, radius, check)
                .Where(p => p.IsAlive && p.Level >= state.Entry.MinPlayerLevel)
                .ToList();
        }

        private static void JoinEvent(IPublicEvent publicEvent, IPlayer player)
        {
            var teams = publicEvent.GetTeams().ToList();
            if (teams.Count == 0 || teams.Any(t => t.GetMembers().Any(m => m.CharacterId == player.CharacterId)))
                return;

            EventTeam factionTeam = player.Faction1 == Faction.Exile ? EventTeam.Exiles : EventTeam.Dominion;
            IPublicEventTeam team = teams.FirstOrDefault(t => t.Team == factionTeam)
                ?? teams.FirstOrDefault(t => t.Team == EventTeam.PublicTeam)
                ?? teams[0];

            publicEvent.JoinEvent(player, team.Team);
        }
    }
}
