using NexusForever.Game.Static.PVP;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Pvp;

namespace NexusForever.WorldServer.Network.Message.Handler.Pvp
{
    /// <summary>
    /// Turn PvP On/Off from the player portrait. A forced flag (battlegrounds, arenas) cannot be changed by the player.
    /// </summary>
    public class ClientPvpToggleFlagsHandler : IMessageHandler<IWorldSession, ClientPvpToggleFlags>
    {
        public void HandleMessage(IWorldSession session, ClientPvpToggleFlags toggle)
        {
            if (session.Player == null || (session.Player.PvPFlags & PvPFlag.Forced) != 0)
                return;

            PvPFlag flags = toggle.Value
                ? session.Player.PvPFlags | PvPFlag.Enabled
                : session.Player.PvPFlags & ~PvPFlag.Enabled;
            if (flags == session.Player.PvPFlags)
                return;

            session.Player.PvPFlags = flags;
            session.Player.SendSystemMessage(toggle.Value ? "You are now flagged for PvP." : "You are no longer flagged for PvP.");
        }
    }
}
