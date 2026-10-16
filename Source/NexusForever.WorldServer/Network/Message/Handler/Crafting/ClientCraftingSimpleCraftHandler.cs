using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Crafting;

namespace NexusForever.WorldServer.Network.Message.Handler.Crafting
{
    public class ClientCraftingSimpleCraftHandler : IMessageHandler<IWorldSession, ClientCraftingSimpleCraft>
    {
        public void HandleMessage(IWorldSession session, ClientCraftingSimpleCraft message)
        {
            session.Player.TradeskillManager.Craft(message.TradeskillSchematic2Id, 1u);
        }
    }
}
