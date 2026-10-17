using System.Linq;
using NexusForever.Game.Abstract.PublicEvent;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.PublicEvent;

namespace NexusForever.WorldServer.Network.Message.Handler.Event
{
    /// <summary>
    /// The scoreboard of an event (the expedition end screen, the live stats window) asks for the event's stats.
    /// </summary>
    public class ClientPublicEventRequestScoreboardHandler : IMessageHandler<IWorldSession, ClientPublicEventRequestScoreboard>
    {
        public void HandleMessage(IWorldSession session, ClientPublicEventRequestScoreboard requestScoreboard)
        {
            if (!requestScoreboard.Subscribe || session.Player.Map == null)
                return;

            IPublicEvent publicEvent = session.Player.Map.PublicEventManager.GetEvents()
                .FirstOrDefault(e => e.Id == requestScoreboard.EventId);
            publicEvent?.SendStatsUpdate();
        }
    }
}
