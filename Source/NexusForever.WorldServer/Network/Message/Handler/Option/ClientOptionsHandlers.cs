using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Option;

namespace NexusForever.WorldServer.Network.Message.Handler.Option
{
    /// <summary>
    /// Casting and combat log options only change client behaviour here, they are accepted without being stored.
    /// </summary>
    public class ClientCombatOptionsHandler : IMessageHandler<IWorldSession, ClientCombatOptions>
    {
        public void HandleMessage(IWorldSession session, ClientCombatOptions message)
        {
        }
    }

    /// <summary>
    /// Casting and shared challenge options, accepted without being stored.
    /// </summary>
    public class ClientOptionsHandler : IMessageHandler<IWorldSession, ClientOptions>
    {
        public void HandleMessage(IWorldSession session, ClientOptions message)
        {
        }
    }

    /// <summary>
    /// Combat log filter checkboxes: the server stops sending the disabled kinds of entries to this player.
    /// </summary>
    public class ClientCombatLogDisablesHandler : IMessageHandler<IWorldSession, ClientCombatLogDisables>
    {
        public void HandleMessage(IWorldSession session, ClientCombatLogDisables message)
        {
            if (session.Player != null)
                session.Player.CombatLogDisables = message.DisableFlags;
        }
    }

    /// <summary>
    /// "Hide other players" combat log option: entries broadcast by other players are not sent.
    /// </summary>
    public class ClientCombatLogDisableOthersHandler : IMessageHandler<IWorldSession, ClientCombatLogDisableOthers>
    {
        public void HandleMessage(IWorldSession session, ClientCombatLogDisableOthers message)
        {
            if (session.Player != null)
                session.Player.CombatLogDisableOthers = message.DisableOtherPlayers;
        }
    }
}
