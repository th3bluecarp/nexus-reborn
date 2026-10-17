using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model
{
    /// <summary>
    /// The vendor window's Sell Junk button (SellJunkToVendor), read from WildStar64.exe 16042. The client writes a single zero byte.
    /// </summary>
    [Message(GameMessageOpcode.ClientSellJunkToVendor)]
    public class ClientSellJunkToVendor : IReadable
    {
        public void Read(GamePacketReader reader)
        {
        }
    }
}
