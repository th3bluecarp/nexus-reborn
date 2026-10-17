using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model
{
    /// <summary>
    /// An item clicked in the bags while the bank (or shared realm bank) is open, read from WildStar64.exe 16042: the item guid, then
    /// a value telling the two banks apart that is not read here.
    /// </summary>
    [Message(GameMessageOpcode.ClientItemMoveToBank)]
    public class ClientItemMoveToBank : IReadable
    {
        public ulong ItemGuid { get; private set; }

        public void Read(GamePacketReader reader)
        {
            ItemGuid = reader.ReadULong();
            reader.ReadBytes(reader.BytesRemaining);
        }
    }
}
