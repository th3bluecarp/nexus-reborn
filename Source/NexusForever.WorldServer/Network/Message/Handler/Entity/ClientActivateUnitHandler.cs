using NexusForever.Game.Abstract.Entity;
using NexusForever.Network;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Spell;

namespace NexusForever.WorldServer.Network.Message.Handler.Entity
{
    public class ClientActivateUnitHandler : IMessageHandler<IWorldSession, ClientActivateUnit>
    {
        public void HandleMessage(IWorldSession session, ClientActivateUnit activateUnit)
        {
            IWorldEntity entity = session.Player.GetVisible<IWorldEntity>(activateUnit.UnitId);
            // the unit can despawn between the client's click and this packet (a looted corpse, a used node): a race, not a
            // bad packet; the exception disconnected the client (the quest bot lost a whole Algoroc leg to it)
            if (entity == null)
                return;

            // TODO: sanity check for range etc.

            entity.OnActivate(session.Player);
        }
    }
}
