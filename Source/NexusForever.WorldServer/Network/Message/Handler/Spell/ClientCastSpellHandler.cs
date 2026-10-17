using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Entity;
using NexusForever.Network;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Spell;

namespace NexusForever.WorldServer.Network.Message.Handler.Spell
{
    public class ClientCastSpellHandler : IMessageHandler<IWorldSession, ClientCastSpell>
    {
        /// <summary>
        /// This Handler is ued when a player has disabled continuous casting in Settings > Controls
        /// </summary>
        public void HandleMessage(IWorldSession session, ClientCastSpell castSpell)
        {
            IItem item = session.Player.Inventory.GetItem(InventoryLocation.Ability, castSpell.BagIndex);
            if (item == null)
                throw new InvalidPacketValueException();

            ICharacterSpell characterSpell = session.Player.SpellManager.GetSpell(item.Id);
            if (characterSpell == null)
                throw new InvalidPacketValueException();

            // an AMP unlocked ability stays in the book after the AMP is removed but cannot be used
            if (!session.Player.SpellManager.IsSpellUsable(characterSpell.BaseInfo.Entry.Id))
                return;

            characterSpell.Cast();
        }
    }
}
