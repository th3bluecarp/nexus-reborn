using NexusForever.Game.Abstract.Entity;
using NexusForever.Network;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.Spell;

namespace NexusForever.WorldServer.Network.Message.Handler.Entity
{
    /// <remarks>
    /// Possibly only used by Bindpoint entities
    /// </remarks>
    public class ClientActivateUnitInteractionHandler : IMessageHandler<IWorldSession, ClientCastBindPointSpell>
    {
        public void HandleMessage(IWorldSession session, ClientCastBindPointSpell activateUnitInteraction)
        {
            IWorldEntity entity = session.Player.GetVisible<IWorldEntity>(activateUnitInteraction.UnitId);
            // a unit that despawned on the way: ignore rather than disconnect the client
            if (entity == null)
                return;

            entity.OnActivateCast(session.Player, activateUnitInteraction.ClientSpellCastUniqueId);
        }
    }
}
