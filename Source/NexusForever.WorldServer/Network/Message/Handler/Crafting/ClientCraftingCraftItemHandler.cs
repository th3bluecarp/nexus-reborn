using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Crafting;

namespace NexusForever.WorldServer.Network.Message.Handler.Crafting
{
    public class ClientCraftingCraftItemHandler : IMessageHandler<IWorldSession, ClientCraftingCraftItem>
    {
        public void HandleMessage(IWorldSession session, ClientCraftingCraftItem message)
        {
            session.Player.TradeskillManager.Craft(message.TradeskillSchematic2Id, message.SchematicCount);
        }
    }
}
