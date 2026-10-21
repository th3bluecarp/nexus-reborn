using NexusForever.Network.Message;
using NexusForever.Game.Static.Crafting;

namespace NexusForever.Network.World.Message.Model.Crafting
{
    [Message(GameMessageOpcode.ServerTradeskillSigilResult)]
    public class ServerTradeskillSigilResult : IWritable
    {
        public TradeskillResult TradeskillSigilResult { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(TradeskillSigilResult, 4u); // 4 bits, read from WildStar64.exe
        }
    }
}
