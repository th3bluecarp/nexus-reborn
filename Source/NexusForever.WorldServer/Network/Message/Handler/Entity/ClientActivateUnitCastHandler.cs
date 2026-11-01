using NexusForever.Game.Abstract.Entity;
using NexusForever.Network;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Spell;

namespace NexusForever.WorldServer.Network.Message.Handler.Entity
{
    public class ClientActivateUnitCastHandler : IMessageHandler<IWorldSession, ClientActivateUnitCast>
    {
        public void HandleMessage(IWorldSession session, ClientActivateUnitCast activateUnitCast)
        {
            IWorldEntity entity = session.Player.GetVisible<IWorldEntity>(activateUnitCast.UnitId);
            // the unit may have left view between the click and this packet (a portal that just teleported the player):
            // ignore the stale activation instead of dropping the session
            if (entity == null)
                return;

            entity.OnActivateCast(session.Player, activateUnitCast.ClientSpellCastUniqueId);
        }
    }
}
